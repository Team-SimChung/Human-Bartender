using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// SPH 파티클을 메타볼 액체로 그린다. 두 패스로 나눠 그린다.
///
///  1패스: 파티클마다 자기 크기만 한 사각형을 RenderTexture에 가산 합성 (LiquidBlobSplat)
///  2패스: 누적된 필드에 threshold를 적용해 액체 색으로 출력 (LiquidComposite)
///
/// 이전에는 화면 전체를 덮는 쿼드 하나에서 픽셀마다 모든 블롭을 검사했는데, 비용이
/// (화면 픽셀 수 x 파티클 수)라 파티클을 조금만 늘려도 급격히 무거워졌다(384개가 한계였다).
/// 지금은 각 블롭이 자기 주변만 칠하므로 비용이 (파티클 수 x 블롭 픽셀)이 되어, 파티클을
/// 몇 배로 늘려도 거의 그대로다 — 알갱이 크기를 성능 걱정 없이 정할 수 있다.
///
/// URP에서도 별도 렌더러 기능 없이 동작하도록 RenderPipelineManager 콜백에서 CommandBuffer로
/// 1패스를 실행한다(레이어나 프로젝트 설정을 건드리지 않는다).
/// </summary>
public class SphLiquidRenderer : MonoBehaviour
{
    [SerializeField] int sortingOrder = 25;

    [Tooltip("필드 텍스처 해상도 배율. 1이면 화면과 같은 해상도, 0.5면 절반(가볍지만 가장자리가 거칠어진다).")]
    [Range(0.25f, 1f)]
    [SerializeField] float fieldResolutionScale = 0.5f;

    [Tooltip("액체를 그릴 카메라. 비워두면 Camera.main을 쓴다.")]
    [SerializeField] Camera targetCamera;

    Material splatMaterial;
    Material compositeMaterial;

    MeshRenderer compositeRenderer;
    Mesh compositeMesh;

    Mesh blobMesh;
    RenderTexture fieldTexture;
    CommandBuffer commandBuffer;

    // 블롭 하나당 사각형 하나(정점 4 + 삼각형 2).
    Vector3[] vertices;
    Vector2[] uvs;
    int[] triangles;
    int meshCapacity;

    float blobRadiusScale = 1.8f;
    float stretchPerSpeed = 0.45f;
    float maxStretch = 4f;
    float streamThinning = 0.35f;
    float threshold = 0.5f;
    float edgeSmoothness = 0.08f;
    float blobRadius;

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    void Awake()
    {
        splatMaterial = new Material(Shader.Find("Pour/LiquidBlobSplat"));
        compositeMaterial = new Material(Shader.Find("Pour/LiquidComposite"));

        blobMesh = new Mesh { name = "LiquidBlobs" };
        blobMesh.MarkDynamic();
        blobMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 파티클이 많아지면 65535 정점을 넘는다

        commandBuffer = new CommandBuffer { name = "Liquid Blob Field" };

        CreateCompositeQuad();
        EnsureMeshCapacity(256);
    }

    void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

    /// <summary>합성 결과를 보여줄 화면 크기 쿼드. 카메라 앞에 붙여 항상 화면을 덮게 한다.</summary>
    void CreateCompositeQuad()
    {
        var go = new GameObject("Liquid Composite");
        go.transform.SetParent(transform, false);

        var meshFilter = go.AddComponent<MeshFilter>();
        compositeRenderer = go.AddComponent<MeshRenderer>();
        compositeRenderer.sortingOrder = sortingOrder;
        compositeRenderer.sharedMaterial = compositeMaterial;

        compositeMesh = new Mesh { name = "LiquidCompositeQuad" };
        compositeMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
        };
        compositeMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        compositeMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        compositeMesh.RecalculateBounds();

        meshFilter.mesh = compositeMesh;
    }

    public void Init(LiquidProfile profile, Color liquidColor)
    {
        blobRadiusScale = profile.blobRadiusScale;
        stretchPerSpeed = profile.stretchPerSpeed;
        maxStretch = profile.maxStretch;
        streamThinning = profile.streamThinning;
        threshold = profile.threshold;
        edgeSmoothness = profile.edgeSmoothness;

        blobRadius = profile.physics.spacing * blobRadiusScale;

        compositeMaterial.SetColor(ColorId, liquidColor);
        compositeMaterial.SetFloat(ThresholdId, threshold);
        compositeMaterial.SetFloat(SmoothnessId, edgeSmoothness);
    }

    /// <summary>파티클 위치/속도로 블롭 사각형 메시를 다시 만든다. 파티클 수에 상한이 없다.</summary>
    public void UpdateBlobs(IReadOnlyList<SphParticle> particles)
    {
        int count = particles.Count;
        EnsureMeshCapacity(count);

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = particles[i].velocity;
            float speed = velocity.magnitude;

            Vector2 dir = speed > 0.01f ? velocity / speed : Vector2.right;

            // 늘임은 속도에 직접 비례시킨다. maxVelocity로 정규화하지 않는 이유는 SphParticle.Integrate가
            // CFL 조건(한 스텝에 spacing의 40%)으로 속도를 그보다 낮게 잘라서다 — spacing을 줄이면
            // 그 상한이 같이 내려가 speed01이 1에 영영 못 닿고, 줄기가 덜 늘어나 알갱이로 보인다.
            float stretch = Mathf.Clamp(1f + speed * stretchPerSpeed, 1f, maxStretch);

            // 가늘어짐은 늘임과 같은 속도 곡선을 탄다. stretch에서 되뽑으면 정규화 기준을 새로 두지
            // 않아도 되고, 두 값이 늘 같은 지점에서 최대가 된다(고인 액체는 둘 다 1배 = 원래 굵기).
            float speed01 = maxStretch > 1f ? (stretch - 1f) / (maxStretch - 1f) : 0f;
            float thinFactor = Mathf.Lerp(1f, 1f - streamThinning, speed01);

            // 사각형의 두 축을 진행 방향과 그 수직으로 잡는다. uv 공간의 원이 곧 월드의 타원이 되므로
            // 셰이더는 uv 길이만 보면 되고, 두 축의 크기를 따로 줄 수 있다.
            //
            // 길이(진행 방향)는 늘임만, 두께(수직)는 가늘어짐만 적용한다. 예전처럼 두 축에 모두
            // 가늘어짐을 걸면 줄기를 가늘게 만들수록 길이도 같이 줄어 파티클 사이가 끊어졌다 —
            // 그래서 "흐르게 하려면 두껍게" 둘 수밖에 없었다.
            Vector2 along = dir * (blobRadius * stretch);
            Vector2 perp = new Vector2(-dir.y, dir.x) * (blobRadius * thinFactor);
            Vector2 center = particles[i].pos;

            int v = i * 4;
            vertices[v + 0] = center - along - perp;
            vertices[v + 1] = center + along - perp;
            vertices[v + 2] = center + along + perp;
            vertices[v + 3] = center - along + perp;

            uvs[v + 0] = new Vector2(-1f, -1f);
            uvs[v + 1] = new Vector2(1f, -1f);
            uvs[v + 2] = new Vector2(1f, 1f);
            uvs[v + 3] = new Vector2(-1f, 1f);
        }

        // 남는 자리는 한 점으로 접어 그려지지 않게 한다.
        for (int v = count * 4; v < vertices.Length; v++)
        {
            vertices[v] = Vector3.zero;
            uvs[v] = Vector2.zero;
        }

        // 정점/uv만 올린다. 삼각형은 용량이 바뀔 때만 바뀌므로 매 프레임 다시 올리면 그만큼 순수한 낭비다.
        blobMesh.SetVertices(vertices);
        blobMesh.SetUVs(0, uvs);
    }

    void EnsureMeshCapacity(int particleCount)
    {
        if (particleCount <= meshCapacity && vertices != null) return;

        meshCapacity = Mathf.Max(256, Mathf.NextPowerOfTwo(particleCount));

        vertices = new Vector3[meshCapacity * 4];
        uvs = new Vector2[meshCapacity * 4];
        triangles = new int[meshCapacity * 6];

        for (int i = 0; i < meshCapacity; i++)
        {
            int v = i * 4;
            int t = i * 6;

            triangles[t + 0] = v + 0;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 0;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }

        // 정점을 먼저 올려야 삼각형 인덱스가 범위를 벗어나지 않는다.
        blobMesh.Clear();
        blobMesh.SetVertices(vertices);
        blobMesh.SetUVs(0, uvs);
        blobMesh.SetTriangles(triangles, 0);

        // CommandBuffer.DrawMesh는 컬링을 하지 않으므로 경계를 한 번 크게 잡아두면 된다.
        // 이러면 매 프레임 RecalculateBounds로 전 정점을 훑는 비용이 사라진다.
        blobMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
    }

    Camera ResolveCamera() => targetCamera != null ? targetCamera : Camera.main;

    /// <summary>
    /// 액체를 그릴 카메라를 정한다.
    ///
    /// 이 렌더러는 화면을 덮는 쿼드에 합성 결과를 얹는 방식이라 어느 카메라의 화면인지가 중요하다.
    /// 기믹 큐가 돌릴 때는 바에서 멀리 떨어진 무대를 전용 카메라가 비추므로, 메인 카메라에 그리면
    /// 액체가 바 쪽 화면에 그려져 무대에서는 아무것도 안 보인다.
    ///
    /// 프리팹에 남은 카메라 참조는 씬 밖 오브젝트라 어차피 끊겨 있어서, 실행 중에 다시 이어 준다.
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    /// <summary>
    /// 합성 쿼드 맞추기는 렌더링 콜백이 아니라 여기서 한다 — 렌더 도중에 Transform을 바꾸면
    /// 그 프레임의 컬링 결과와 어긋나 아무것도 안 보일 수 있다.
    /// </summary>
    void LateUpdate()
    {
        Camera camera = ResolveCamera();
        if (camera == null) return;

        EnsureFieldTexture(camera);
        FitCompositeQuad(camera);
    }

    /// <summary>1패스: 블롭 메시를 필드 텍스처에 가산 합성한다.</summary>
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != ResolveCamera() || blobMesh == null || fieldTexture == null) return;

        commandBuffer.Clear();

        commandBuffer.SetRenderTarget(fieldTexture);
        commandBuffer.ClearRenderTarget(true, true, Color.clear);

        // CommandBuffer로 직접 그릴 때는 카메라의 뷰/투영 행렬이 자동으로 잡히지 않는다.
        // 넣어주지 않으면 블롭이 엉뚱한 좌표에 그려져 화면에 아무것도 안 나온다.
        // (플랫폼별 Y 뒤집힘 변환은 Unity가 알아서 하므로 카메라 원본 행렬을 그대로 넘긴다.)
        commandBuffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

        commandBuffer.DrawMesh(blobMesh, Matrix4x4.identity, splatMaterial);

        context.ExecuteCommandBuffer(commandBuffer);
    }

    void EnsureFieldTexture(Camera camera)
    {
        int width = Mathf.Max(64, Mathf.RoundToInt(camera.pixelWidth * fieldResolutionScale));
        int height = Mathf.Max(64, Mathf.RoundToInt(camera.pixelHeight * fieldResolutionScale));

        if (fieldTexture != null && fieldTexture.width == width && fieldTexture.height == height) return;

        if (fieldTexture != null) fieldTexture.Release();

        // 필드값은 여러 블롭이 더해져 1을 넘으므로 8비트로는 잘린다 — 부동소수점 포맷을 쓴다.
        fieldTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf)
        {
            name = "LiquidField",
            filterMode = FilterMode.Bilinear,
        };
        fieldTexture.Create();

        compositeMaterial.SetTexture(MainTexId, fieldTexture);
    }

    /// <summary>합성 쿼드를 카메라 화면에 정확히 맞춘다(필드 텍스처와 화면 좌표가 1:1로 대응해야 한다).</summary>
    void FitCompositeQuad(Camera camera)
    {
        Transform quad = compositeRenderer.transform;

        float height = camera.orthographicSize * 2f;
        float width = height * camera.aspect;

        quad.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
        quad.rotation = camera.transform.rotation;
        quad.localScale = new Vector3(width, height, 1f);
    }

    void OnDestroy()
    {
        if (splatMaterial != null) Destroy(splatMaterial);
        if (compositeMaterial != null) Destroy(compositeMaterial);
        if (blobMesh != null) Destroy(blobMesh);
        if (compositeMesh != null) Destroy(compositeMesh);

        if (fieldTexture != null) fieldTexture.Release();
        commandBuffer?.Release();
    }
}
