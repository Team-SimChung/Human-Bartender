using System;
using UnityEngine;

/// <summary>
/// 캐릭터가 앞 그림(기둥·간판·파이프) 뒤로 지나가면 그 조각만 흐리게 한다(QA-54, 웹 outside-art.js paintCorridorForeground).
/// 조각마다 불투명한 부분과 캐릭터 몸 범위가 겹친 넓이로 흐림 정도를 정한다. 조각 사이·간판 다리 사이의 빈 공간은 세지 않는다.
/// 불투명한 부분은 셋업이 원본 PNG에서 미리 구워 두므로 실행 중에 텍스처를 읽지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ForegroundOccluderFade : MonoBehaviour
{
    [Serializable]
    public sealed class Part
    {
        [Tooltip("흐려지는 조각 그림.")]
        public SpriteRenderer renderer;
        [Tooltip("이 조각의 불투명한 부분(이 오브젝트 기준 좌표, 월드 단위). Setup Foreground Occluder Fade 메뉴가 원본 PNG에서 굽는다.")]
        public Rect[] opaqueRects = Array.Empty<Rect>();
    }

    [Header("대상")]
    [Tooltip("앞 그림 뒤로 지나가는 캐릭터(루나)의 SpriteRenderer.")]
    [SerializeField] SpriteRenderer target;
    [Tooltip("대상의 몸 범위(대상 피벗 기준, 반전하지 않은 그림 기준, 월드 단위). 셋업이 서 있는 프레임의 불투명 범위로 채운다.")]
    [SerializeField] Rect body = new(-.15f, -.27f, .3f, .78f);

    [Header("조각")]
    [SerializeField] Part[] parts = Array.Empty<Part>();

    [Header("페이드")]
    [Tooltip("몸 넓이 중 이 비율이 가려지면 가장 옅어진다. 웹 25%.")]
    [SerializeField, Range(.05f, 1f)] float fullFadeCoverage = .25f;
    [Tooltip("가장 옅을 때의 불투명도. 웹 1 - 0.76 = 0.24.")]
    [SerializeField, Range(0f, 1f)] float minimumAlpha = .24f;
    [Tooltip("목표 흐림의 90%까지 따라가는 시간(초). 웹 80ms.")]
    [SerializeField, Min(.001f)] float responseSeconds = .08f;
    [Tooltip("남은 차이가 이보다 작으면 목표 값에 바로 맞춘다.")]
    [SerializeField, Range(0f, .05f)] float settleThreshold = .005f;

    float[] fades = Array.Empty<float>();
    float[] restAlpha = Array.Empty<float>();

    public SpriteRenderer Target => target;
    public int PartCount => parts.Length;
    public float FadeOf(int part) => part >= 0 && part < fades.Length ? fades[part] : 0f;

    void OnEnable()
    {
        fades = new float[parts.Length];
        restAlpha = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++) restAlpha[i] = parts[i].renderer != null ? parts[i].renderer.color.a : 1f;
    }

    void OnDisable()
    {
        for (int i = 0; i < parts.Length; i++) SetAlpha(i, 0f);
    }

    void LateUpdate() => Step(Time.deltaTime);

    /// <summary>한 프레임 진행한다. 테스트에서도 쓴다.</summary>
    public void Step(float deltaTime)
    {
        if (fades.Length != parts.Length) OnEnable();
        Rect local = TargetBodyInLocalSpace();
        bool present = target != null && target.enabled && target.gameObject.activeInHierarchy;
        for (int i = 0; i < parts.Length; i++)
        {
            float goal = present ? CoveredFraction(local, parts[i].opaqueRects, fullFadeCoverage) : 0f;
            fades[i] = Approach(fades[i], goal, deltaTime, responseSeconds, settleThreshold);
            SetAlpha(i, fades[i]);
        }
    }

    /// <summary>대상 몸 범위를 이 오브젝트 기준 좌표로 바꾼다. 그림이 반전되면 몸 범위도 좌우를 뒤집는다.</summary>
    Rect TargetBodyInLocalSpace()
    {
        if (target == null) return default;
        var box = body;
        if (target.flipX) box.x = -box.xMax;
        var t = target.transform;
        Vector3 a = transform.InverseTransformPoint(t.TransformPoint(new Vector3(box.xMin, box.yMin)));
        Vector3 b = transform.InverseTransformPoint(t.TransformPoint(new Vector3(box.xMax, box.yMax)));
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    /// <summary>몸 넓이 중 불투명한 부분에 가려진 비율을 fullCoverage로 나눈 값(0~1).</summary>
    public static float CoveredFraction(Rect body, Rect[] opaque, float fullCoverage)
    {
        float area = body.width * body.height;
        if (area <= 0f || opaque == null) return 0f;
        float covered = 0f;
        foreach (var r in opaque)
        {
            float w = Mathf.Min(body.xMax, r.xMax) - Mathf.Max(body.xMin, r.xMin);
            float h = Mathf.Min(body.yMax, r.yMax) - Mathf.Max(body.yMin, r.yMin);
            if (w > 0f && h > 0f) covered += w * h;
        }
        return Mathf.Clamp01(covered / (area * Mathf.Max(.0001f, fullCoverage)));
    }

    /// <summary>responseSeconds 동안 차이의 90%를 줄이는 지수 감쇠. 남은 차이가 settle보다 작으면 목표에 맞춘다.</summary>
    public static float Approach(float current, float goal, float deltaTime, float responseSeconds, float settle)
    {
        if (deltaTime <= 0f) return current;
        float keep = Mathf.Exp(-deltaTime * Mathf.Log(10f) / Mathf.Max(.0001f, responseSeconds));
        float residual = (current - goal) * keep;
        return Mathf.Abs(residual) < settle ? goal : goal + residual;
    }

    void SetAlpha(int part, float fade)
    {
        var renderer = parts[part].renderer;
        if (renderer == null || part >= restAlpha.Length) return;
        var color = renderer.color;
        color.a = restAlpha[part] * (1f - (1f - minimumAlpha) * fade);
        renderer.color = color;
    }
}
