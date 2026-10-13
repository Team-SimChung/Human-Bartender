using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>기준 타겟 이동량을 배경별로 누적하고 최종 표시 좌표만 픽셀 격자에 맞춘다.</summary>
[DisallowMultipleComponent]
public sealed class PixelPerfectParallax : MonoBehaviour
{
    [Serializable]
    public sealed class Layer
    {
        public Transform target;

        [Tooltip("0: 월드 고정, 1: 카메라와 함께 이동하여 화면 고정. 먼 배경일수록 1에 가깝게 설정한다.")]
        [Range(0f, 1f)] public float followRatio = 0.5f;

        [NonSerialized] internal Vector3 accumulatedPosition;
    }

    [SerializeField] private Camera targetCamera;
    [Tooltip("이동량을 읽을 기준 Transform. 비워두면 Target Camera의 Transform을 사용한다.")]
    [SerializeField] private Transform movementTarget;
    [Tooltip("지정하면 해당 카메라의 Assets PPU를 사용한다. 비워두면 targetCamera에서 찾는다.")]
    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;
    [Min(1)] [SerializeField] private int fallbackPixelsPerUnit = 100;
    [SerializeField] private bool moveY;
    [Tooltip("한 렌더 사이에 이 거리보다 크게 이동하면 순간이동으로 보고 누적하지 않는다. 0이면 자동 감지를 끈다.")]
    [Min(0f)] [SerializeField] private float teleportDistance = 5f;
    [SerializeField] private Layer[] layers = Array.Empty<Layer>();

    private Vector3 previousTargetPosition;
    private Transform previousMovementTarget;
    private bool hasReference;
    private bool skipNextMovement;

    private void OnEnable()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (pixelPerfectCamera == null && targetCamera != null)
            pixelPerfectCamera = targetCamera.GetComponent<PixelPerfectCamera>();

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        // 비활성화 중 카메라 이동을 복귀 프레임에 누적하지 않는다.
        skipNextMovement = true;
    }

    /// <summary>다음 카메라 렌더 직전의 위치를 새 기준으로 사용한다. 순간이동/배경 재배치 후 호출한다.</summary>
    [ContextMenu("Reset Parallax Reference")]
    public void ResetReference()
    {
        hasReference = false;
    }

    /// <summary>카메라 순간이동 후 다음 렌더 전에 호출한다. 배경 누적값은 유지하고 카메라 이동만 건너뛴다.</summary>
    public void NotifyCameraTeleport() => skipNextMovement = true;

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != targetCamera) return;

        Transform source = movementTarget != null ? movementTarget : camera.transform;

        // Cinemachine의 최종 위치가 반영된 첫 렌더 프레임에서 기준점을 잡는다.
        if (!hasReference)
        {
            previousTargetPosition = source.position;
            previousMovementTarget = source;
            foreach (var layer in layers)
                if (layer != null && layer.target != null)
                    layer.accumulatedPosition = layer.target.position;
            hasReference = true;
        }

        // 실행 중 기준 타겟을 교체해도 두 타겟의 위치 차이를 이동량으로 누적하지 않는다.
        Vector3 delta = source == previousMovementTarget
            ? source.position - previousTargetPosition : Vector3.zero;
        previousTargetPosition = source.position;
        previousMovementTarget = source;
        delta.z = 0f;
        if (!moveY) delta.y = 0f;
        if (skipNextMovement || teleportDistance > 0f && delta.sqrMagnitude > teleportDistance * teleportDistance)
            delta = Vector3.zero;
        skipNextMovement = false;
        int ppu = Mathf.Max(1, pixelPerfectCamera != null
            ? pixelPerfectCamera.assetsPPU : fallbackPixelsPerUnit);

        foreach (var layer in layers)
        {
            if (layer == null || layer.target == null) continue;

            // 반올림 전 좌표에 누적하여 1픽셀보다 작은 이동도 보존한다.
            layer.accumulatedPosition += delta * layer.followRatio;
            Vector3 position = layer.accumulatedPosition;
            position.x = Snap(position.x, ppu);
            if (moveY)
                position.y = Snap(position.y, ppu);
            layer.target.position = position;
        }
    }

    private static float Snap(float value, int ppu) => Mathf.Round(value * ppu) / ppu;
}
