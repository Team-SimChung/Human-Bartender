using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 왼쪽 루나 모니터. 웹 session.css(대기 노이즈·열림/닫힘·알림 상자)와 luna.js(셀·구도·강조광)를 옮겼다.
///
/// 노이즈·바 배경·루나·빛은 월드(SpriteRenderer)이고, 열리고 닫히는 범위는 Sprite Mask(FeedMask)의 크기로 정한다.
/// 알림 상자와 단색 섬광·빛은 캔버스(UI)에 있고, 섬광·빛의 크기를 같은 열림 범위로 맞춘다.
/// 대기 중에는 어두운 모니터와 노이즈만 보이고, 결합 0.6초부터 가운데 가로선에서 바 배경과 루나가 열린다.
/// 파손 때는 같은 화면이 0.32초에 접히고 빨간 SHAKE FAILED / SEAL BROKEN으로 바뀐다.
/// </summary>
public sealed class ShakeV2MonitorView : MonoBehaviour
{
    [Serializable]
    public sealed class Look
    {
        [Header("배치")]
        [Tooltip("모니터 영상 영역의 월드 크기(가운데 = 이 Transform). 캔버스 483×518 × 0.01.")]
        public Vector2 feedWorldSize = new Vector2(4.83f, 5.18f);
        [Tooltip("모니터 영상 영역의 캔버스 크기. 단색 섬광·빛을 같은 범위로 맞춘다.")]
        public Vector2 feedCanvasSize = new Vector2(483f, 518f);
        [Tooltip("캔버스 1단위의 월드 크기(직교 크기 2.7·캔버스 높이 540 기준). 노이즈가 뛰는 거리를 바꾸는 데 쓴다.")]
        [Min(.0001f)] public float worldPerCanvasUnit = .01f;

        [Header("대기 노이즈")]
        [Tooltip("노이즈가 한 바퀴 위치를 바꾸는 시간(초). 4단계로 뛴다.")]
        [Min(.01f)] public float grainPeriod = .8f;
        [Tooltip("노이즈가 뛰는 위치(캔버스 단위). 웹 standby-grain 키프레임 × 0.75.")]
        public Vector2[] grainSteps = { Vector2.zero, new Vector2(3f, 2f), new Vector2(-2f, 4f), new Vector2(-2f, -2f) };

        [Header("열림 · 닫힘")]
        [Tooltip("결합 뒤 모니터가 열리기 시작하는 시각(초).")]
        [Min(0f)] public float openAtSec = .6f;
        [Tooltip("열리거나 접히는 데 걸리는 시간(초).")]
        [Min(.01f)] public float openSec = .32f;
        [Tooltip("열릴 때 지나가는 빛의 시간(초)과 색.")]
        [Min(.01f)] public float beamOpenSec = .25f;
        public Color beamOpenColor = new Color32(0xB2, 0xF6, 0xFF, 0xFF);
        [Tooltip("접힐 때 지나가는 빛의 색. 접히는 시간 동안 거꾸로 재생한다.")]
        public Color beamCloseColor = new Color32(0xFF, 0x88, 0x91, 0xFF);
        [Tooltip("가운데 가는 선 두께를 캔버스 단위로 바꾸는 배율(웹 1px × 0.75).")]
        public float pixelToCanvas = .75f;

        [Header("알림 상자")]
        [Tooltip("열리면서 알림이 사라지는 시간(초)과 알림을 정리하는 시각(초, 결합 기준).")]
        [Min(.01f)] public float noticeFadeSec = .26f;
        public float noticeHideSec = .94f;
        public Color signalColor = new Color32(0x74, 0xE3, 0xF2, 0xFF);
        [Tooltip("내구도 1칸(약한 결합)일 때의 색.")]
        public Color weakColor = new Color32(0xF3, 0xC9, 0x85, 0xFF);
        public Color failedColor = new Color32(0xFF, 0x69, 0x79, 0xFF);
        public Color subColor = new Color32(0xD2, 0xE6, 0xEB, 0xFF);
        public Color failedSubColor = new Color32(0xFF, 0xB7, 0xBE, 0xFF);
        public string assembledTitle = "SHAKER ASSEMBLED";
        public string assembledSub = "START...";
        public string failedTitle = "SHAKE FAILED";
        public string failedSub = "SEAL BROKEN";

        [Header("루나")]
        [Tooltip("셀별 쉐이커 광원 중심(원본 px, 셀 왼쪽 위 기준). 0→7번.")]
        public Vector2[] tinCenters =
        {
            new Vector2(710, 270), new Vector2(786, 229), new Vector2(807, 213), new Vector2(786, 230),
            new Vector2(710, 270), new Vector2(793, 354), new Vector2(830, 364), new Vector2(800, 348),
        };
        [Tooltip("루나 셀 스프라이트의 피벗(원본 px). 구도 기준 = 원본 중심 X 675, 위 Y 0.")]
        public Vector2 lunaPivot = new Vector2(675f, 0f);
        [Tooltip("손 강조광 반지름(원본 px)과 색.")]
        public float handGlowRadius = 100f;
        public Color handGlowColor = new Color32(255, 244, 214, 255);
        [Tooltip("냉기 빛 반지름(원본 px), 색, 냉각도 100일 때 불투명도.")]
        public float chillGlowRadius = 150f;
        public Color chillGlowColor = new Color32(160, 228, 246, 255);
        [Range(0f, 1f)] public float chillGlowAlpha = .28f;
        [Tooltip("MISS 붉은 섬광 색(최대 불투명도 포함)과 시간(초).")]
        public Color missFlashColor = new Color(1f, 70f / 255f, 70f / 255f, .18f);
        [Min(.01f)] public float missFlashSec = .3f;
        [Tooltip("파손 때 붉은 기운의 처음 불투명도와 1초에 줄어드는 양.")]
        [Range(0f, 1f)] public float brokenFlashAlpha = .35f;
        public float brokenFlashFadePerSec = .25f;
    }

    [Header("World")]
    [Tooltip("대기 노이즈(03.World/MonitorGrain). 영상이 보이지 않는 동안 위치가 뛴다.")]
    [SerializeField] Transform grain;
    [Tooltip("모니터가 열리고 닫히는 범위(Sprite Mask). 크기만 바뀐다.")]
    [SerializeField] Transform feedMask;
    [SerializeField] SpriteRenderer luna;
    [Tooltip("루나 8셀(1280×720씩, 0번부터).")]
    [SerializeField] Sprite[] lunaCells;
    [SerializeField] SpriteRenderer handGlow;
    [SerializeField] SpriteRenderer chillGlow;

    [Header("UI")]
    [Tooltip("MISS·파손 붉은 섬광(단색). 크기를 열림 범위에 맞춘다.")]
    [SerializeField] Image missFlash;
    [Tooltip("열림·닫힘 때 지나가는 빛(임시 단색). 크기를 열림 범위에 맞춘다.")]
    [SerializeField] Image beam;
    [SerializeField] RectTransform notice;
    [SerializeField] CanvasGroup noticeGroup;
    [SerializeField] Image noticeBrackets;
    [SerializeField] Image noticeBorder;
    [Tooltip("파손 알림 바탕(임시 단색).")]
    [SerializeField] Image noticeFailedBody;
    [Tooltip("알림 바탕이 위아래로 열리는 자르기 영역(RectMask2D).")]
    [SerializeField] RectTransform noticeClip;
    [Tooltip("알림 위쪽 스캔선(임시 단색).")]
    [SerializeField] Image noticeScan;
    [SerializeField] TMP_Text noticeTitle;
    [SerializeField] TMP_Text noticeSub;

    [SerializeField] Look look = new Look();

    float grainClock;
    Vector2 bracketSize, noticeClipSize;
    Vector3 grainBase;
    bool captured;

    void CaptureLayout()
    {
        if (captured) return;
        captured = true;
        if (noticeBrackets != null) bracketSize = noticeBrackets.rectTransform.sizeDelta;
        if (noticeClip != null) noticeClipSize = noticeClip.sizeDelta;
        if (grain != null) grainBase = grain.localPosition;
    }

    public void ResetState()
    {
        CaptureLayout();
        grainClock = 0f;
        if (grain != null) grain.localPosition = grainBase;
        if (notice != null) notice.gameObject.SetActive(false);
        SetFeedVisible(false);
    }

    void SetFeedVisible(bool visible)
    {
        if (feedMask != null && feedMask.gameObject.activeSelf != visible) feedMask.gameObject.SetActive(visible);
        if (luna != null && luna.enabled != visible) luna.enabled = visible;
        if (!visible)
        {
            if (handGlow != null) handGlow.enabled = false;
            if (chillGlow != null) chillGlow.enabled = false;
            if (missFlash != null) missFlash.enabled = false;
            if (beam != null) beam.enabled = false;
        }
    }

    /// <summary>
    /// 매 프레임 그린다. lastMissAt은 내구도를 잃은(MISS 표시) 게임 시각, realDt는 일시정지가 아닐 때의 프레임 시간이다.
    /// </summary>
    public void Render(ShakeV2Rules rules, ShakeV2LunaMotion motion, float lastMissAt, float realDt)
    {
        CaptureLayout();
        float now = rules.GameTime;
        bool sealed_ = rules.SealQuality != EShakeV2SealQuality.None;
        bool broken = rules.WasBroken;
        bool feedVisible = sealed_ && rules.SealAge >= look.openAtSec && !(broken && rules.BrokenAge >= look.openSec);

        RenderGrain(feedVisible, realDt);
        SetFeedVisible(feedVisible);
        if (feedVisible) RenderFeed(rules, motion, lastMissAt, now);
        RenderNotice(rules, broken);
    }

    void RenderGrain(bool feedVisible, float realDt)
    {
        if (grain == null) return;
        // 영상이 보이는 동안은 노이즈를 멈춘다(웹 .luna:has(.luna-feed:not([aria-hidden]))::before{animation:none}).
        Vector2 step = Vector2.zero;
        if (!feedVisible && look.grainSteps.Length > 0)
        {
            grainClock += realDt;
            int steps = look.grainSteps.Length;
            step = look.grainSteps[Mathf.FloorToInt(Mathf.Repeat(grainClock, look.grainPeriod) / look.grainPeriod * steps) % steps];
        }
        grain.localPosition = grainBase + new Vector3(step.x, step.y, 0f) * look.worldPerCanvasUnit;
    }

    void RenderFeed(ShakeV2Rules rules, ShakeV2LunaMotion motion, float lastMissAt, float now)
    {
        // 열림(결합 0.6초부터) 또는 닫힘(파손 순간부터)의 진행.
        float open = 1f, beamAlpha = 0f;
        Color beamColor = look.beamOpenColor;
        if (rules.WasBroken)
        {
            open = 1f - rules.BrokenAge / look.openSec;
            beamAlpha = ShakeV2Presentation.MonitorBeam(open);
            beamColor = look.beamCloseColor;
        }
        else
        {
            float age = rules.SealAge - look.openAtSec;
            if (age < look.openSec) open = age / look.openSec;
            if (age < look.beamOpenSec) beamAlpha = ShakeV2Presentation.MonitorBeam(age / look.beamOpenSec);
        }
        Vector3 visible = ShakeV2Presentation.MonitorOpening(open);
        float lineCanvas = visible.z * look.pixelToCanvas;
        Vector2 canvasSize = new Vector2(look.feedCanvasSize.x * visible.x, Mathf.Min(look.feedCanvasSize.y, look.feedCanvasSize.y * visible.y + lineCanvas));
        Vector2 worldSize = new Vector2(look.feedWorldSize.x * visible.x,
            Mathf.Min(look.feedWorldSize.y, look.feedWorldSize.y * visible.y + lineCanvas * look.worldPerCanvasUnit));
        if (feedMask != null) feedMask.localScale = new Vector3(worldSize.x, worldSize.y, 1f);

        if (beam != null)
        {
            beam.enabled = beamAlpha > 0f;
            beam.rectTransform.sizeDelta = canvasSize;
            beamColor.a = beamAlpha;
            beam.color = beamColor;
        }

        // 루나 셀: 파손이면 파손 순간의 셀, 왕복·분리 중이면 지금 셀, 결합 직후 83ms는 1번 셀.
        int frame = rules.WasBroken ? motion.FrameAt(rules.GameTime - rules.BrokenAge)
            : rules.Phase is EShakeV2Phase.Shake or EShakeV2Phase.Unseal or EShakeV2Phase.Done ? motion.FrameAt(now)
            : rules.SealAge < motion.FrameSec ? 1 : 0;
        if (luna != null && lunaCells != null && frame < lunaCells.Length) luna.sprite = lunaCells[frame];

        // 광원 중심: 셀 좌표(원본 px) → 루나 피벗 기준 월드 좌표.
        float lunaScale = luna != null ? luna.transform.localScale.y * (luna.sprite != null ? 1f / luna.sprite.pixelsPerUnit : .01f) : .01f;
        Vector3 lunaOrigin = luna != null ? luna.transform.localPosition : Vector3.zero;
        Vector2 center = look.tinCenters.Length > frame ? look.tinCenters[frame] : Vector2.zero;
        var glowPoint = new Vector3(lunaOrigin.x + (center.x - look.lunaPivot.x) * lunaScale, lunaOrigin.y - (center.y - look.lunaPivot.y) * lunaScale, 0f);

        bool striking = rules.Phase is EShakeV2Phase.Shake or EShakeV2Phase.Unseal;
        float pulse = striking && !(lastMissAt >= motion.StartedAt) ? motion.PulseAt(now) : 0f;
        Glow(handGlow, glowPoint, look.handGlowRadius * lunaScale, look.handGlowColor, pulse);
        float chill = rules.Chill > 5f ? rules.Chill / 100f * look.chillGlowAlpha : 0f;
        Glow(chillGlow, glowPoint, look.chillGlowRadius * lunaScale, look.chillGlowColor, chill);

        if (missFlash != null)
        {
            float alpha = 0f;
            if (rules.WasBroken) alpha = Mathf.Max(0f, look.brokenFlashAlpha - rules.BrokenAge * look.brokenFlashFadePerSec);
            else if (now - lastMissAt < look.missFlashSec) alpha = look.missFlashColor.a * (1f - (now - lastMissAt) / look.missFlashSec);
            missFlash.enabled = alpha > 0f;
            missFlash.rectTransform.sizeDelta = canvasSize;
            var color = look.missFlashColor;
            color.a = alpha;
            missFlash.color = color;
        }
    }

    static void Glow(SpriteRenderer glow, Vector3 point, float radius, Color color, float alpha)
    {
        if (glow == null) return;
        bool visible = alpha > 0f;
        if (glow.enabled != visible) glow.enabled = visible;
        if (!visible) return;
        glow.transform.localPosition = point;
        float size = glow.sprite != null ? Mathf.Max(.0001f, glow.sprite.bounds.size.x) : 1f;
        float scale = 2f * radius / size;
        glow.transform.localScale = new Vector3(scale, scale, 1f);
        color.a = alpha;
        glow.color = color;
    }

    void RenderNotice(ShakeV2Rules rules, bool broken)
    {
        if (notice == null) return;
        float age;
        bool visible;
        if (broken)
        {
            age = rules.BrokenAge - look.openSec;
            visible = age >= 0f && !rules.Finished;
        }
        else
        {
            age = rules.SealAge;
            visible = rules.SealQuality != EShakeV2SealQuality.None && age < look.noticeHideSec;
        }
        if (notice.gameObject.activeSelf != visible) notice.gameObject.SetActive(visible);
        if (!visible) return;

        Color signal = broken ? look.failedColor : rules.StartDurability == 1 ? look.weakColor : look.signalColor;
        if (noticeBrackets != null) noticeBrackets.color = signal;
        if (noticeBorder != null) noticeBorder.color = signal;
        if (noticeFailedBody != null) noticeFailedBody.enabled = broken;
        if (noticeTitle != null)
        {
            noticeTitle.text = broken ? look.failedTitle : look.assembledTitle;
            noticeTitle.color = signal;
            noticeTitle.maxVisibleCharacters = Typed(age, .10f, .24f, 16, noticeTitle.text.Length);
        }
        if (noticeSub != null)
        {
            noticeSub.text = broken ? look.failedSub : look.assembledSub;
            noticeSub.color = broken ? look.failedSubColor : look.subColor;
            noticeSub.maxVisibleCharacters = Typed(age, .27f, .14f, 8, noticeSub.text.Length);
        }

        // 괄호가 1.2·1.5배에서 가라앉고(0.18초), 바탕이 가운데서 위아래로 열린다(0.14초). 스캔선은 0.3초에 내려간다.
        float bracket = ShakeV2Presentation.CssEaseOut(age / .18f);
        if (noticeBrackets != null)
            noticeBrackets.rectTransform.sizeDelta = new Vector2(bracketSize.x * Mathf.Lerp(1.2f, 1f, bracket), bracketSize.y * Mathf.Lerp(1.5f, 1f, bracket));
        if (noticeClip != null)
            noticeClip.sizeDelta = new Vector2(noticeClipSize.x, noticeClipSize.y * Mathf.Lerp(.04f, 1f, ShakeV2Presentation.CssEaseOut(age / .14f)));
        if (noticeScan != null)
        {
            float scan = ShakeV2Presentation.CssEaseOut(age / .3f);
            noticeScan.rectTransform.anchoredPosition = new Vector2(0f, -scan * noticeClipSize.y);
            var color = signal;
            color.a = .8f * (1f - scan);
            noticeScan.color = color;
        }

        float fade = 1f;
        if (!broken && age >= look.openAtSec) fade = 1f - ShakeV2Presentation.CssEaseOut((age - look.openAtSec) / look.noticeFadeSec);
        if (noticeGroup != null) noticeGroup.alpha = fade;
    }

    // CSS steps(n, end)로 잘라 보이는 글자 수. 픽셀 글꼴이라 폭 대신 글자 수로 나눈다.
    static int Typed(float age, float delay, float duration, int steps, int length)
    {
        float progress = Mathf.Clamp01((age - delay) / duration);
        return Mathf.FloorToInt(Mathf.Floor(progress * steps) / steps * length);
    }
}
