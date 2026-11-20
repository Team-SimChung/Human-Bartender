using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


/// <summary>슬롯 타입(Left/Right/Middle 등)별로 카메라가 이동할 목표 Transform을 매핑하는 데이터.</summary>
[System.Serializable]
public class CameraPostion
{
    public ESlotType slotType = ESlotType.Middle;
    public Transform pos;
}

/// <summary>카메라 줌(해상도) 프리셋 종류.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ECameraZoomType
{
    None = 0,
    [EnumMember(Value = "base")]
    Base,
    [EnumMember(Value = "sub")]
    Sub,
    [EnumMember(Value = "outside")]
    OutSide,
}


/// <summary>
/// Cinemachine 기반 카메라 컨트롤러(CameraController의 후속 구현).
/// 카메라 자체를 옮기지 않고 vcam이 추적하는 cameraAnchor를 이동시키며, 해상도 전환도
/// Pixel Perfect Camera 대신 Cinemachine Lens의 OrthographicSize를 직접 보간한다.
/// </summary>
public class CameraControllerNew : MonoBehaviour, ICameraControlNew
{
    [Header("References")]
    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private CinemachineConfiner2D confiner;

    [Tooltip("vcam의 Tracking Target. 카메라 이동 시 이 Transform을 옮김")]
    [SerializeField] private Transform cameraAnchor;

    [Header("Resolutions")]
    [SerializeField] private Vector2Int baseResolution = new Vector2Int(1280, 720);
    [SerializeField] private Vector2Int targetResolution = new Vector2Int(960, 540);
    [SerializeField] private Vector2Int outSideResolution = new Vector2Int(480, 270);

    [Header("Transition")]
    [SerializeField] private float defaultTransitionDuration = 1f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("줌 도중 Pixel Perfect 렌더 크기를 고정하고 Cinemachine 렌즈만 움직인다.")]
    [SerializeField] private bool keepRenderResolutionDuringZoom;

    // 캐릭터 리소스 로드 직후 한 프레임이 길어져도 0.8초 이동이 한두 프레임에 끝나지 않게 한다.
    const float MaxTransitionStep = 1f / 30f;

    CancellationTokenSource resolution;
    CancellationTokenSource offset;
    public bool IsMoving => offset != null;
    System.Action restoreResolution;
    CinemachinePixelPerfect suspendedPixelPerfect;
    FrameOverride frameOverride;
    void Awake() => SyncLensToPPC();
    void OnDisable() { frameOverride?.Dispose(); CancelResolution(); offset?.Cancel(); ResumePixelPerfectCorrection(); }

    /// <summary>대화 연출이 카메라 구도를 잠시 소유한다. Dispose하면 추적 대상·렌즈·픽셀 설정을 복구한다.</summary>
    public FrameOverride BeginFrameOverride()
    {
        EnsureCamera();
        if (cameraAnchor == null) throw new System.InvalidOperationException("Camera anchor is missing.");
        if (frameOverride != null) throw new System.InvalidOperationException("Another scene presentation owns this camera.");
        offset?.Cancel();
        CancelResolution();
        ResumePixelPerfectCorrection();
        return frameOverride = new FrameOverride(this);
    }

    /// <summary>씬에 배치된 카메라의 기존 추적 구도를 보존하는 일시적 소유권.</summary>
    public sealed class FrameOverride : System.IDisposable
    {
        readonly CameraControllerNew owner;
        readonly Transform parent;
        readonly Vector3 localPosition, localScale, worldPosition;
        readonly Quaternion localRotation;
        readonly LensSettings lens;
        readonly bool pixelEnabled, correctionEnabled;
        readonly CinemachinePixelPerfect correction;
        readonly CinemachinePositionComposer composer;
        readonly float aspect;
        bool disposed;

        internal FrameOverride(CameraControllerNew value)
        {
            owner = value;
            var anchor = owner.cameraAnchor;
            parent = anchor.parent;
            localPosition = anchor.localPosition;
            localRotation = anchor.localRotation;
            localScale = anchor.localScale;
            worldPosition = anchor.position;
            lens = owner.vcam.Lens;
            pixelEnabled = owner.pixelPerfectCamera.enabled;
            correction = owner.vcam.GetComponent<CinemachinePixelPerfect>();
            correctionEnabled = correction != null && correction.enabled;
            composer = owner.vcam.GetComponent<CinemachinePositionComposer>();
            aspect = owner.mainCamera != null && owner.mainCamera.aspect > 0 ? owner.mainCamera.aspect : 16f / 9f;
            anchor.SetParent(null, true);
            // The render target is unchanged; only the lens is animated while its automatic correction is suspended.
            owner.pixelPerfectCamera.enabled = false;
            if (correction != null) correction.enabled = false;
        }

        Vector3 TargetOffset => composer != null ? owner.cameraAnchor.rotation * composer.TargetOffset : Vector3.zero;
        public Vector3 Center => owner.cameraAnchor.position + TargetOffset;
        public float Width => owner.vcam.Lens.OrthographicSize * 2 * aspect;
        public Vector3 FollowCenter => (parent != null ? parent.TransformPoint(localPosition) : worldPosition) + TargetOffset;
        public float FollowWidth => lens.OrthographicSize * 2 * aspect;
        public bool IsValid => !disposed && owner != null && owner.cameraAnchor != null && owner.vcam != null;

        public Vector3 Confine(Vector3 center, float width)
        {
            if (owner.confiner == null || owner.confiner.BoundingShape2D == null) return center;
            // Home and OutSide use rectangular confiners; Cinemachine still performs the final shape confinement.
            var bounds = owner.confiner.BoundingShape2D.bounds;
            float halfWidth = width * .5f, halfHeight = halfWidth / aspect;
            center.x = bounds.size.x <= width ? bounds.center.x : Mathf.Clamp(center.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
            center.y = bounds.size.y <= halfHeight * 2 ? bounds.center.y : Mathf.Clamp(center.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
            return center;
        }

        public void SetFrame(Vector3 center, float width)
        {
            if (!IsValid) throw new System.InvalidOperationException("The camera presentation has ended.");
            if (!float.IsFinite(width) || width <= 0 || !float.IsFinite(center.x) || !float.IsFinite(center.y))
                throw new System.ArgumentOutOfRangeException(nameof(width));
            center.z = Center.z;
            owner.cameraAnchor.position = center - TargetOffset;
            var next = owner.vcam.Lens;
            next.OrthographicSize = width / (2 * aspect);
            owner.vcam.Lens = next;
            owner.InvalidateConfinerLensCache();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (owner == null) return;
            if (owner.cameraAnchor != null)
            {
                owner.cameraAnchor.SetParent(parent, false);
                owner.cameraAnchor.localPosition = parent != null ? localPosition : worldPosition;
                owner.cameraAnchor.localRotation = localRotation;
                owner.cameraAnchor.localScale = localScale;
            }
            if (owner.vcam != null) owner.vcam.Lens = lens;
            if (owner.pixelPerfectCamera != null) owner.pixelPerfectCamera.enabled = pixelEnabled;
            if (correction != null) correction.enabled = correctionEnabled;
            owner.InvalidateConfinerLensCache();
            if (ReferenceEquals(owner.frameOverride, this)) owner.frameOverride = null;
        }
    }

    public void FollowTarget(Transform target, Vector3 localOffset = default)
    {
        if (cameraAnchor == null || target == null) return;
        offset?.Cancel();
        cameraAnchor.SetParent(target, true);
        if (localOffset != default) cameraAnchor.localPosition = localOffset;
    }
    public void Unfollow()
    {
        offset?.Cancel();
        if (cameraAnchor != null) cameraAnchor.SetParent(null, true);
    }
    public void SetFollowOffset(Vector3 value)
    {
        offset?.Cancel();
        if (cameraAnchor != null) cameraAnchor.localPosition = value;
    }

    /// <summary>암전 중 추적 구도를 즉시 적용하고 다음 카메라 갱신에서 이전 위치의 보간을 건너뛴다.</summary>
    public void SnapToFollow(Transform target, Vector3 localOffset, ECameraZoomType? zoomType = null)
    {
        EnsureCamera();
        if (cameraAnchor == null || target == null)
            throw new System.InvalidOperationException("순간 이동에 필요한 카메라 기준점과 추적 대상이 없습니다.");
        if (frameOverride != null)
            throw new System.InvalidOperationException("다른 연출이 카메라를 사용하는 동안에는 순간 이동할 수 없습니다.");

        FollowTarget(target);
        SetFollowOffset(localOffset);
        if (zoomType.HasValue) ActionZoom(zoomType.Value);
        else CancelResolution();

        // 위치 합성과 경계 보정이 출발 층의 이력을 사용하지 않도록 초기화한다.
        vcam.PreviousStateIsValid = false;
        if (mainCamera != null && mainCamera.TryGetComponent<CinemachineBrain>(out var brain))
            brain.ResetState();
    }

    public void TransitionFollowOffset(Vector3 targetOffset, float duration = 1f, AnimationCurve curve = null) =>
        TransitionFollowOffsetAsync(targetOffset, duration, curve).Forget(Report);
    public async UniTask TransitionFollowOffsetAsync(Vector3 targetOffset, float duration = 1f,
        AnimationCurve curve = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (cameraAnchor == null) throw new System.InvalidOperationException("Camera anchor is missing.");
        offset?.Cancel();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        offset = source;
        var start = cameraAnchor.localPosition;
        try
        {
            for (float elapsed = 0; elapsed < duration;)
            {
                source.Token.ThrowIfCancellationRequested();
                elapsed += Mathf.Min(Time.deltaTime, MaxTransitionStep);
                cameraAnchor.localPosition = Vector3.Lerp(start, targetOffset, SafeCurve(curve).Evaluate(Mathf.Clamp01(elapsed / duration)));
                await UniTask.Yield(source.Token);
            }
            source.Token.ThrowIfCancellationRequested();
            cameraAnchor.localPosition = targetOffset;
        }
        finally { if (ReferenceEquals(offset, source)) offset = null; }
    }

    public void ActionZoom(ECameraZoomType zoomType = ECameraZoomType.Base) => ApplyResolutionImmediate(GetResolution(zoomType));
    public void ActionZoomAndBack(ECameraZoomType zoomType = ECameraZoomType.Base, UniTaskCompletionSource tcs = null) =>
        ActionZoomAndBackAsync(zoomType, tcs).Forget(Report);
    public async UniTask ActionZoomAndBackAsync(ECameraZoomType zoomType, UniTaskCompletionSource completion,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        EnsureCamera();
        CancelResolution();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        resolution = source;
        CaptureResolution();
        try
        {
            SetResolution(GetResolution(zoomType));
            if (completion != null) await completion.Task.AttachExternalCancellation(source.Token);
            source.Token.ThrowIfCancellationRequested();
        }
        finally { FinishResolution(source, true); }
    }
    public void ApplyResolutionImmediate(Vector2Int value)
    {
        EnsureCamera();
        CancelResolution();
        SetResolution(value);
    }
    public void TransitionCameraZoom(ECameraZoomType zoomType = ECameraZoomType.Base, float dur = 1f, AnimationCurve curve = null) =>
        TransitionCameraZoomAsync(zoomType, dur, curve).Forget(Report);
    public async UniTask TransitionCameraZoomAsync(ECameraZoomType zoomType = ECameraZoomType.Base,
        float duration = 1f, AnimationCurve curve = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        EnsureCamera();
        var target = GetResolution(zoomType);
        float targetSize = ResToOrthoSize(target);
        if (resolution == null && pixelPerfectCamera.enabled &&
            (keepRenderResolutionDuringZoom ||
             pixelPerfectCamera.refResolutionX == target.x && pixelPerfectCamera.refResolutionY == target.y) &&
            Mathf.Approximately(vcam.Lens.OrthographicSize, targetSize))
            return;
        CancelResolution();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        resolution = source;
        CaptureResolution();
        float start = vcam.Lens.OrthographicSize;
        bool completed = false;
        try
        {
            if (keepRenderResolutionDuringZoom)
                pixelPerfectCamera.CorrectCinemachineOrthoSize(start);
            else
                pixelPerfectCamera.enabled = false;
            for (float elapsed = 0; elapsed < duration;)
            {
                source.Token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var lens = vcam.Lens;
                lens.OrthographicSize = Mathf.Lerp(start, targetSize, SafeCurve(curve).Evaluate(Mathf.Clamp01(elapsed / duration)));
                vcam.Lens = lens;
                InvalidateConfinerLensCache();
                await UniTask.Yield(source.Token);
            }
            source.Token.ThrowIfCancellationRequested();
            if (keepRenderResolutionDuringZoom)
            {
                var lens = vcam.Lens;
                lens.OrthographicSize = targetSize;
                vcam.Lens = lens;
                InvalidateConfinerLensCache();
            }
            else
                SetResolution(target);
            completed = true;
        }
        finally { FinishResolution(source, !completed); }
    }

    void CaptureResolution()
    {
        var previous = new Vector2Int(pixelPerfectCamera.refResolutionX, pixelPerfectCamera.refResolutionY);
        bool enabled = pixelPerfectCamera.enabled;
        var lens = vcam.Lens;
        restoreResolution = () =>
        {
            if (pixelPerfectCamera != null)
            {
                pixelPerfectCamera.enabled = false;
                pixelPerfectCamera.refResolutionX = previous.x;
                pixelPerfectCamera.refResolutionY = previous.y;
                if (enabled) SuspendPixelPerfectCorrectionUntilRendered();
                pixelPerfectCamera.enabled = enabled;
            }
            if (vcam != null) vcam.Lens = lens;
            InvalidateConfinerLensCache();
        };
    }
    // 이전 전환을 복원한 뒤 다음 전환이 화면을 소유한다.
    void CancelResolution()
    {
        resolution?.Cancel();
        restoreResolution?.Invoke();
        restoreResolution = null;
        resolution = null;
    }
    void FinishResolution(CancellationTokenSource source, bool restore)
    {
        if (!ReferenceEquals(resolution, source)) return;
        if (restore) restoreResolution?.Invoke();
        restoreResolution = null;
        resolution = null;
    }
    void SetResolution(Vector2Int value)
    {
        if (value.x <= 0 || value.y <= 0) throw new System.ArgumentOutOfRangeException(nameof(value));
        pixelPerfectCamera.enabled = false;
        pixelPerfectCamera.refResolutionX = value.x;
        pixelPerfectCamera.refResolutionY = value.y;
        SuspendPixelPerfectCorrectionUntilRendered();
        pixelPerfectCamera.enabled = true;
        SyncLensToPPC();
        InvalidateConfinerLensCache();
    }
    // PPC refreshes its cached zoom/orthoSize only when its camera starts rendering.
    // Do not let Cinemachine correct the new lens with the previous resolution's cache.
    // Keep PPC itself active so that this render refreshes the cache normally.
    void SuspendPixelPerfectCorrectionUntilRendered()
    {
        if (suspendedPixelPerfect != null || vcam == null) return;
        var correction = vcam.GetComponent<CinemachinePixelPerfect>();
        if (correction == null || !correction.enabled) return;
        suspendedPixelPerfect = correction;
        correction.enabled = false;
        RenderPipelineManager.endCameraRendering += OnPixelPerfectCameraRendered;
    }
    void OnPixelPerfectCameraRendered(ScriptableRenderContext context, Camera camera)
    {
        if (pixelPerfectCamera != null && pixelPerfectCamera.isActiveAndEnabled &&
            camera == pixelPerfectCamera.GetComponent<Camera>())
            ResumePixelPerfectCorrection();
    }
    void ResumePixelPerfectCorrection()
    {
        RenderPipelineManager.endCameraRendering -= OnPixelPerfectCameraRendered;
        if (suspendedPixelPerfect != null) suspendedPixelPerfect.enabled = true;
        suspendedPixelPerfect = null;
    }
    void EnsureCamera()
    {
        if (pixelPerfectCamera == null || vcam == null || pixelPerfectCamera.assetsPPU <= 0)
            throw new System.InvalidOperationException("Camera/PPC references or PPU are invalid.");
    }
    static void Report(System.Exception e) { if (e is not System.OperationCanceledException) Debug.LogException(e); }
    static readonly AnimationCurve LinearCurve = AnimationCurve.Linear(0, 0, 1, 1);
    static AnimationCurve SafeCurve(AnimationCurve curve) => curve ?? LinearCurve;
    Vector2Int GetResolution(ECameraZoomType type) => type switch
    {
        ECameraZoomType.Base => baseResolution,
        ECameraZoomType.Sub => targetResolution,
        ECameraZoomType.OutSide => outSideResolution,
        _ => baseResolution
    };
    float ResToOrthoSize(Vector2Int value) => value.y / (2f * pixelPerfectCamera.assetsPPU);
    void SyncLensToPPC()
    {
        if (vcam == null || pixelPerfectCamera == null || pixelPerfectCamera.assetsPPU <= 0) return;
        var lens = vcam.Lens;
        lens.OrthographicSize = ResToOrthoSize(new Vector2Int(pixelPerfectCamera.refResolutionX, pixelPerfectCamera.refResolutionY));
        vcam.Lens = lens;
    }
    void InvalidateConfinerLensCache() { if (confiner != null) confiner.InvalidateLensCache(); }
}
