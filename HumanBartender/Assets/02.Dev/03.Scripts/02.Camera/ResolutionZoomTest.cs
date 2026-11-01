using UnityEngine;
using UnityEngine.Rendering.Universal;
[RequireComponent(typeof(Camera))]
public class ResolutionZoomTest : MonoBehaviour
{
    [Header("References")]
    public PixelPerfectCamera pixelPerfectCamera;

    [Header("Resolutions")]
    public Vector2Int baseResolution = new Vector2Int(1280, 720);
    public Vector2Int targetResolution = new Vector2Int(960, 540);

    [Header("Transition")]
    public float transitionDuration = 0.5f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Test Input")]
    public KeyCode toggleKey = KeyCode.Space;

    private Camera cam;
    private bool isAtTarget = false;
    private Coroutine transitionRoutine;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (pixelPerfectCamera == null)
            pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
    }

    void Start()
    {
        ApplyResolutionImmediate(baseResolution);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    [ContextMenu("Toggle Zoom")]
    public void Toggle()
    {
        Vector2Int to = isAtTarget ? baseResolution : targetResolution;
        isAtTarget = !isAtTarget;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionRoutine(to));
    }

    private System.Collections.IEnumerator TransitionRoutine(Vector2Int to)
    {
        // 1. 현재 orthoSize를 기억하고 PPC 끄기
        float fromSize = cam.orthographicSize;
        float toSize = ResToOrthoSize(to);
        pixelPerfectCamera.enabled = false;

        // 2. orthoSize를 부드럽게 보간
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / transitionDuration));
            cam.orthographicSize = Mathf.Lerp(fromSize, toSize, k);
            yield return null;
        }

        // 3. 전환 완료: PPC의 Reference Resolution을 최종값으로 설정하고 다시 켜기
        ApplyResolutionImmediate(to);
        transitionRoutine = null;
    }

    private void ApplyResolutionImmediate(Vector2Int res)
    {
        pixelPerfectCamera.refResolutionX = res.x;
        pixelPerfectCamera.refResolutionY = res.y;
        pixelPerfectCamera.enabled = true;
        // PPC가 enable 되는 순간 스스로 orthographicSize를 올바르게 재계산함
    }

    private float ResToOrthoSize(Vector2Int res)
    {
        // PPC 내부 계산과 동일한 공식
        return res.y / (2f * pixelPerfectCamera.assetsPPU);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 320, 20),
            $"State: {(isAtTarget ? "TARGET" : "BASE")}");
        GUI.Label(new Rect(10, 30, 320, 20),
            $"RefRes: {pixelPerfectCamera.refResolutionX}x{pixelPerfectCamera.refResolutionY}");
        GUI.Label(new Rect(10, 50, 320, 20),
            $"OrthoSize: {cam.orthographicSize:F3}");
        GUI.Label(new Rect(10, 70, 320, 20),
            $"PPC: {(pixelPerfectCamera.enabled ? "ON" : "OFF")}");

        if (GUI.Button(new Rect(10, 100, 120, 30), "Toggle Zoom"))
            Toggle();
    }
}
