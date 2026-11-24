using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 쉐이킹 V2 HUD(캔버스). 웹 game.js의 drawKeys·drawGauge·글자·완료 문구와 진행 10칸을 옮겼다.
/// 시간 표시는 TimeGroup의 CraftGimmickHud가 공통 규칙(mm:ss, 숨김이면 ??:??)으로 따로 그린다.
///
/// 오른쪽 패널 HUD(ControlHud)의 가운데가 게임 좌표 필드(880×640)의 가운데다. D·E 키 상자와 떠오르는 글자는
/// 월드 셰이커가 정한 게임 좌표를 이 캔버스 좌표로 바꿔 따라간다. 모든 연출은 규칙의 게임 시간으로 계산하므로
/// 순간 정지·일시정지와 함께 멈춘다.
/// </summary>
public sealed class ShakeV2ControlView : MonoBehaviour
{
    [Serializable]
    public sealed class Look
    {
        [Header("배치")]
        [Tooltip("게임 좌표 1단위의 캔버스 크기. 오른쪽 패널 폭 440 / 게임 폭 880.")]
        [Min(.01f)] public float canvasPerGameUnit = .5f;

        [Header("키 상자")]
        [Tooltip("키 상자가 나타날 때 커지는 비율과 가라앉는 시간(초). 웹: 9%, 0.24초.")]
        [Range(0f, 1f)] public float keyPulse = .09f;
        [Min(.01f)] public float keyPulseSec = .24f;

        [Header("시간 윤곽")]
        [Tooltip("이번 회차 경과율 구간별 윤곽 색: 청백 → 연두 → 주황 → 빨강.")]
        public Color[] gaugeColors =
        {
            new Color(240f / 255f, 250f / 255f, 1f, .95f), new Color32(0xB7, 0xEE, 0x55, 0xFF),
            new Color32(0xFF, 0x99, 0x3D, 0xFF), new Color32(0xFF, 0x4B, 0x4B, 0xFF),
        };

        [Header("떠오르는 글자")]
        [Tooltip("글자가 떠 있는 시간(초)과 떠오르는 높이(게임 좌표).")]
        [Min(.01f)] public float textSec = .8f;
        public float textRise = 20f;
        [Tooltip("SHAKE! 색과 사라지기 시작하는 지점.")]
        public Color shakeColor = new Color32(0xF0, 0xC9, 0x87, 0xFF);
        [Range(0f, .99f)] public float shakeFadeStart = .25f;
        [Tooltip("MISS·너무 빨라..·틴이 빠졌다! 색과 사라지기 시작하는 지점.")]
        public Color alertColor = new Color32(0xFF, 0x6A, 0x6A, 0xFF);
        [Range(0f, .99f)] public float alertFadeStart = .7f;
        [Tooltip("MISS·너무 빨라.. 자리(게임 좌표). 웹: 가운데, 내구도 게이지 아래 34.")]
        public Vector2 alertPoint = new Vector2(440f, 203f);
        [Tooltip("틴이 빠졌다! 자리(게임 좌표)와 크기 배율.")]
        public Vector2 popPoint = new Vector2(440f, 48f);
        [Min(.1f)] public float popScale = 1.6f;
        [Tooltip("SHAKE!가 내려오지 않는 가장 위 자리(게임 좌표 Y). 웹: 게이지 아래 48.")]
        public float shakeWordMinY = 217f;
        [Tooltip("SHAKE! 크기 배율.")]
        [Min(.1f)] public float shakeScale = .9f;
        [Tooltip("MISS 글자가 기울 수 있는 최대 각도(rad, ±절반).")]
        public float alertTilt = .25f;

        [Header("완료 문구")]
        [Tooltip("완료 문구가 보이는 시간(초).")]
        [Min(.01f)] public float completionSec = 2.2f;
        [Tooltip("밑줄 최대 폭(캔버스 단위)과 펼쳐지는 시간(초).")]
        public float underlineWidth = 230f;
        [Min(.01f)] public float underlineSec = .28f;
        [Tooltip("완료 순간 패널 섬광의 색(최대 불투명도 포함)과 시간(초).")]
        public Color flashColor = new Color(150f / 255f, 232f / 255f, 250f / 255f, .18f);
        [Min(.01f)] public float flashSec = .23f;
    }

    struct FloatText
    {
        public bool Active;
        public float Start, Scale, Tilt, FadeStart;
        public Vector2 Point;
    }

    [Tooltip("월드 셰이커. D·E 키 자리와 SHAKE! 자리를 게임 좌표로 알려 준다.")]
    [SerializeField] ShakeV2ShakerView shaker;

    [Header("Seal")]
    [Tooltip("결합 대기 중에만 보이는 색 구간 게이지. 막대가 Background, 바늘이 Handle이고 값은 편차(±진폭)다.")]
    [SerializeField] Slider sealGauge;
    [SerializeField] GameObject spaceKey;

    [Header("Keys")]
    [SerializeField] RectTransform downKey;
    [SerializeField] RectTransform backKey;
    [SerializeField] RectTransform upKey;
    [SerializeField] TMP_Text downKeyLabel;
    [SerializeField] TMP_Text backKeyLabel;

    [Header("Durability")]
    [Tooltip("왕복과 파손 중에 보이는 셰이커 윤곽 게이지.")]
    [SerializeField] GameObject durabilityGauge;
    [Tooltip("켜진 윤곽. 오른쪽부터 꺼진다(Filled, 왼쪽 기준).")]
    [SerializeField] Image timeOutline;
    [SerializeField] Image[] durabilityPips;
    [SerializeField] Sprite pipFull;
    [SerializeField] Sprite pipLost;
    [SerializeField] Sprite pipUnused;

    [Header("Progress")]
    [SerializeField] Image[] progressCells;
    [SerializeField] Sprite cellOk;
    [SerializeField] Sprite cellMiss;

    [Header("Texts")]
    [Tooltip("SHAKE! 자리. 여러 개가 동시에 떠 있을 수 있다.")]
    [SerializeField] TMP_Text[] shakeWords;
    [Tooltip("MISS·너무 빨라.. 자리.")]
    [SerializeField] TMP_Text[] alertTexts;
    [Tooltip("틴이 빠졌다! 자리.")]
    [SerializeField] TMP_Text popText;

    [Header("Completion")]
    [SerializeField] GameObject completion;
    [SerializeField] RectTransform completionTitle;
    [SerializeField] CanvasGroup completionGroup;
    [Tooltip("금색 밑줄(임시 단색).")]
    [SerializeField] RectTransform completionUnderline;
    [Tooltip("완료 순간 패널 섬광(임시 단색).")]
    [SerializeField] Image completionFlash;

    [SerializeField] Look look = new Look();

    FloatText[] shakeState = Array.Empty<FloatText>();
    FloatText[] alertState = Array.Empty<FloatText>();
    readonly FloatText[] popState = new FloatText[1];
    TMP_Text[] popTexts = Array.Empty<TMP_Text>();

    public void ResetState()
    {
        shakeState = new FloatText[shakeWords?.Length ?? 0];
        alertState = new FloatText[alertTexts?.Length ?? 0];
        popState[0] = default;
        popTexts = popText != null ? new[] { popText } : Array.Empty<TMP_Text>();
        HideAll(shakeWords);
        HideAll(alertTexts);
        if (popText != null) popText.gameObject.SetActive(false);
        if (completion != null) completion.SetActive(false);
        RefreshKeyLabels();
    }

    static void HideAll(TMP_Text[] texts)
    {
        foreach (var text in texts ?? Array.Empty<TMP_Text>()) if (text != null) text.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        GameKeyBindings.Changed += RefreshKeyLabels;
        RefreshKeyLabels();
    }

    void OnDisable() => GameKeyBindings.Changed -= RefreshKeyLabels;

    /// <summary>A·D는 옵션에서 다시 정할 수 있으므로 지금 키 이름을 쓴다. E는 고정이다.</summary>
    void RefreshKeyLabels()
    {
        if (downKeyLabel != null) downKeyLabel.text = GameKeyBindings.DisplayName(GameControl.ShakeRight).ToUpperInvariant();
        if (backKeyLabel != null) backKeyLabel.text = GameKeyBindings.DisplayName(GameControl.ShakeLeft).ToUpperInvariant();
    }

    // ── 글자 ────────────────────────────────────────────────────────────

    /// <summary>정입력 때 셰이커 위·아래 주변에 SHAKE!를 띄운다. 이미 떠 있는 것과 겹치지 않는 자리를 고른다.</summary>
    public void ShowShakeWord(ShakeV2Rules rules, ShakeV2Pose pose)
    {
        if (shaker == null || shakeState.Length == 0) return;
        float now = rules.GameTime;
        Vector2 point = shaker.PickShakeWord(rules, pose, look.shakeWordMinY, p =>
        {
            foreach (var t in shakeState)
                if (t.Active && now - t.Start < look.textSec && Mathf.Abs(p.x - t.Point.x) < 100f && Mathf.Abs(p.y - t.Point.y) < 55f)
                    return true;
            return false;
        });
        Place(shakeState, new FloatText { Active = true, Start = now, Point = point, Scale = look.shakeScale, FadeStart = look.shakeFadeStart });
    }

    /// <summary>MISS·너무 빨라..를 게이지 아래 가운데에 띄운다.</summary>
    public void ShowAlert(ShakeV2Rules rules, string message)
    {
        int slot = Place(alertState, new FloatText
        {
            Active = true, Start = rules.GameTime, Point = look.alertPoint, Scale = 1f,
            Tilt = (Random.value - .5f) * look.alertTilt, FadeStart = look.alertFadeStart,
        });
        if (slot >= 0 && alertTexts[slot] != null) alertTexts[slot].text = message;
    }

    /// <summary>파손 순간 위쪽 가운데에 "틴이 빠졌다!"를 띄우고, 떠 있던 SHAKE!는 지운다.</summary>
    public void ShowPop(ShakeV2Rules rules)
    {
        for (int i = 0; i < shakeState.Length; i++) shakeState[i].Active = false;
        popState[0] = new FloatText
        {
            Active = true, Start = rules.GameTime, Point = look.popPoint, Scale = look.popScale,
            Tilt = (Random.value - .5f) * look.alertTilt, FadeStart = look.alertFadeStart,
        };
    }

    /// <summary>게임 좌표를 오른쪽 패널 HUD 가운데 기준 캔버스 좌표로 바꾼다.</summary>
    public Vector2 FieldLocal(Vector2 game) =>
        new Vector2((game.x - 440f) * look.canvasPerGameUnit, (320f - game.y) * look.canvasPerGameUnit);

    static int Place(FloatText[] state, FloatText text)
    {
        if (state.Length == 0) return -1;
        int slot = 0;
        for (int i = 0; i < state.Length; i++)
        {
            if (!state[i].Active) { slot = i; break; }
            if (state[i].Start < state[slot].Start) slot = i;
        }
        state[slot] = text;
        return slot;
    }

    // ── 그리기 ──────────────────────────────────────────────────────────

    public void Render(ShakeV2Rules rules)
    {
        float now = rules.GameTime;
        bool sealing = rules.Phase == EShakeV2Phase.Seal;
        if (sealGauge != null)
        {
            SetActive(sealGauge.gameObject, sealing);
            if (sealing) sealGauge.SetValueWithoutNotify(rules.SealOffset);
        }
        SetActive(spaceKey, sealing);

        RenderKeys(rules);
        RenderDurability(rules);
        RenderProgress(rules);
        RenderTexts(shakeWords, shakeState, now, look.shakeColor);
        RenderTexts(alertTexts, alertState, now, look.alertColor);
        RenderTexts(popTexts, popState, now, look.alertColor);
        RenderCompletion(rules);
    }

    void RenderKeys(ShakeV2Rules rules)
    {
        EShakeV2Input? ready = rules.ReadyKey;
        float pulse = 1f + look.keyPulse * (1f - ShakeV2Presentation.Smooth(rules.ReadyAge, look.keyPulseSec));
        RenderKey(downKey, ready == EShakeV2Input.Down, pulse, rules, EShakeV2Input.Down);
        RenderKey(backKey, ready == EShakeV2Input.Back, pulse, rules, EShakeV2Input.Back);
        RenderKey(upKey, ready == EShakeV2Input.Up, pulse, rules, EShakeV2Input.Up);
    }

    void RenderKey(RectTransform key, bool visible, float pulse, ShakeV2Rules rules, EShakeV2Input input)
    {
        if (key == null) return;
        if (key.gameObject.activeSelf != visible) key.gameObject.SetActive(visible);
        if (!visible) return;
        // 셰이커 크기가 화면 픽셀에 따라 달라지므로 D·E 자리는 도착 자세의 큰 틴 모서리로 다시 잡는다.
        if (shaker != null) key.anchoredPosition = FieldLocal(shaker.KeyPoint(rules, input));
        key.localScale = new Vector3(pulse, pulse, 1f);
    }

    void RenderDurability(ShakeV2Rules rules)
    {
        bool visible = rules.Phase is EShakeV2Phase.Shake or EShakeV2Phase.Broken;
        SetActive(durabilityGauge, visible);
        if (!visible) return;

        float elapsed = rules.GaugeElapsed;
        if (timeOutline != null)
        {
            bool lit = elapsed < 1f;
            if (timeOutline.enabled != lit) timeOutline.enabled = lit;
            timeOutline.fillAmount = 1f - elapsed;
            int band = Mathf.Clamp(rules.GaugeBand(elapsed), 0, look.gaugeColors.Length - 1);
            if (look.gaugeColors.Length > 0) timeOutline.color = look.gaugeColors[band];
        }

        for (int i = 0; i < (durabilityPips?.Length ?? 0); i++)
        {
            var pip = durabilityPips[i];
            if (pip == null) continue;
            pip.sprite = i < rules.DurabilityLeft ? pipFull : i < rules.StartDurability ? pipLost : pipUnused;
        }
    }

    void RenderProgress(ShakeV2Rules rules)
    {
        var results = rules.Results;
        for (int i = 0; i < (progressCells?.Length ?? 0); i++)
        {
            var cell = progressCells[i];
            if (cell == null) continue;
            bool done = i < results.Count;
            if (cell.enabled != done) cell.enabled = done;
            if (done) cell.sprite = results[i] == EShakeV2RoundResult.Success ? cellOk : cellMiss;
        }
    }

    void RenderTexts(TMP_Text[] texts, FloatText[] state, float now, Color color)
    {
        for (int i = 0; i < (texts?.Length ?? 0) && i < state.Length; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            float k = (now - state[i].Start) / look.textSec;
            bool visible = state[i].Active && k >= 0f && k < 1f;
            if (!visible) state[i].Active = false;
            if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
            if (!visible) continue;

            float pop = ShakeV2Presentation.TextPop(k) * state[i].Scale;
            var rect = text.rectTransform;
            rect.anchoredPosition = FieldLocal(state[i].Point - new Vector2(0f, k * look.textRise));
            rect.localScale = new Vector3(pop, pop, 1f);
            rect.localEulerAngles = new Vector3(0f, 0f, -state[i].Tilt * Mathf.Rad2Deg);
            var c = color;
            c.a = ShakeV2Presentation.TextAlpha(k, state[i].FadeStart);
            text.color = c;
        }
    }

    void RenderCompletion(ShakeV2Rules rules)
    {
        float age = rules.UnsealAge;
        bool visible = rules.Phase is EShakeV2Phase.Unseal or EShakeV2Phase.Done && !rules.WasBroken && age < look.completionSec;
        SetActive(completion, visible);
        if (!visible) return;

        if (completionTitle != null)
        {
            float scale = ShakeV2Presentation.CompletionScale(age);
            completionTitle.localScale = new Vector3(scale, scale, 1f);
        }
        if (completionGroup != null) completionGroup.alpha = ShakeV2Presentation.CompletionAlpha(age);
        if (completionUnderline != null)
            completionUnderline.sizeDelta = new Vector2(look.underlineWidth * ShakeV2Presentation.Smooth(age, look.underlineSec),
                completionUnderline.sizeDelta.y);
        if (completionFlash != null)
        {
            float f = age < look.flashSec ? 1f - age / look.flashSec : 0f;
            var color = look.flashColor;
            color.a *= f * f;
            completionFlash.color = color;
        }
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
