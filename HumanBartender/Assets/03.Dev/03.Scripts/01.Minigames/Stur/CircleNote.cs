using UnityEngine;

public enum NoteType { Short, Long }
public enum NoteState { Idle, Active, Holding }
public enum Judgement { Perfect, Good, Miss }

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
/// <summary>
/// 원형 경로 위에 호(arc) 형태로 렌더링되는 노트 메시 컴포넌트.
/// BuildArcMesh()로 동적 메시를 생성하며, 양 끝이 뾰족하게 테이퍼 처리된다.
/// </summary>
public class CircleNote : PooledObject
{
    [SerializeField] Material noteMaterial;
    [SerializeField] float arcLengthDeg = 10f;
    [SerializeField] int segments = 16;
    [SerializeField] float thickness = 0.3f;

    [SerializeField] float radius = 2f;
    [SerializeField] Vector3 centerPos;

    public NoteType type;
    public NoteState state = NoteState.Idle;
    public float centerAngleDeg;
    public Judgement startJudge;

    public bool clockwise = true;

    public Vector3 HeadPosition => AngleToPos(HeadAngleDeg);
    public Vector3 TailPosition => AngleToPos(TailAngleDeg);

    public float HeadAngleDeg => centerAngleDeg + (clockwise ? +arcLengthDeg * 0.5f : -arcLengthDeg * 0.5f);
    public float TailAngleDeg => centerAngleDeg + (clockwise ? -arcLengthDeg * 0.5f : +arcLengthDeg * 0.5f);

    public Color curColor;
    public float spawnTime = 0;
    public float lifeTime = 0;

    public string Category;

    public void SetNodeColor(Color color)
    {
        curColor = color;
        GetComponent<MeshRenderer>().material.color = curColor;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.localPosition = centerPos;
        BuildArcMesh();
        GetComponent<MeshRenderer>().material = new Material(noteMaterial);
        
        var mr = GetComponent<MeshRenderer>();
        mr.sortingLayerName = "Default";
        mr.sortingOrder = 20;
    }



    public void UpdateRotation()
    {
        transform.rotation = Quaternion.Euler(0, 0, centerAngleDeg);
    }

    void BuildArcMesh()
    {
        var mesh = new Mesh { name = "NoteArc" };
        int vertCount = (segments + 1) * 2;
        var verts = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var tris = new int[segments * 6];

        float arcRad = arcLengthDeg * Mathf.Deg2Rad;
        float halfRad = arcRad * 0.5f;

        // 호 길이 (외경 기준) — UV 타일링용
        float arcArcLength = arcRad * radius;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = -halfRad + t * arcRad;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);

            // 양 끝 tipRatio 구간에서만 두께 변화, 가운데는 1로 평평
            float tipRatio = 0.1f;   // 양 끝 각각 전체 길이의 15%가 뾰족 구간
            float taper;
            if (t < tipRatio) taper = t / tipRatio;
            else if (t > 1f - tipRatio) taper = (1f - t) / tipRatio;
            else taper = 1f;

            float halfThick = thickness * 0.5f * taper;
            float innerRR = radius - halfThick;
            float outerRR = radius + halfThick;

            verts[i * 2] = dir * innerRR;
            verts[i * 2 + 1] = dir * outerRR;

            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int b = i * 2;
            tris[i * 6 + 0] = b;
            tris[i * 6 + 1] = b + 1;
            tris[i * 6 + 2] = b + 2;
            tris[i * 6 + 3] = b + 1;
            tris[i * 6 + 4] = b + 3;
            tris[i * 6 + 5] = b + 2;
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    Vector3 AngleToPos(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return centerPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
    }
    public void Activate(NoteType t, float centerDeg, float arcDeg)
    {
        type = t;
        centerAngleDeg = centerDeg;
        arcLengthDeg = arcDeg;
        state = NoteState.Active;
        gameObject.SetActive(true);
        BuildArcMesh();
        UpdateRotation();
    }

    public void Deactivate()
    {
        state = NoteState.Idle;
        gameObject.SetActive(false);
    }
}
