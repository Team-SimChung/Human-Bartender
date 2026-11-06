using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>배치된 병따기 화면의 값과 연출만 갱신한다. 배치는 RectTransform에서 편집한다.</summary>
[ExecuteAlways]
public sealed class CapOpenView : MonoBehaviour
{
    const float Cx = 640f, Cy = 310f, BottleScale = 4.2f;
    const float WebWidth = 1280f, WebHeight = 720f;

    [Header("Art")]
    [Tooltip("웹 item-beer.png(93×223)와 같은 병 그림. 몸통과 뚜껑을 이 한 장에서 잘라 쓴다.")]
    [SerializeField] Texture2D bottleTexture;
    [Tooltip("웹 item_opener(bottle_opener.png, 70×115)와 같은 병따개 그림.")]
    [SerializeField] Texture2D openerTexture;
    [SerializeField] TMP_FontAsset font;

    [Header("Sorting")]
    [Tooltip("월드 그리기 겹의 시작 순서. 배경(-10)보다 위, 같은 정렬 레이어를 쓴다.")]
    [HideInInspector, SerializeField] int sortingOrder = 0;
    [Tooltip("글자·버튼 캔버스의 정렬 순서. 실행기가 띄울 때 기준값을 더해 바 UI 위로 올린다.")]
    [HideInInspector, SerializeField] int canvasSortingOrder = 20;

    [SerializeField] CapCanvasLayer halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay, powerGauge, rhythmRings;
    [SerializeField] Texture2D haloDefault, haloSuccess;
    CapCarbonationCanvas carbonation;

    [SerializeField] TMP_Text phaseLabel, feedback, timingHint, gaugeLabel, hitLabel, finishLabel;
    [SerializeField] Button hitButton, finishButton;
    [SerializeField] CanvasGroup hitGroup, finishGroup;
    [SerializeField] GameObject hitKey;

    [SerializeField] GameObject lessonRoot;
    [SerializeField] CapLessonShade lessonShade;
    [SerializeField] RectTransform powerFocus, rhythmFocus;
    Vector2 lessonArrowBase;
    [SerializeField] TMP_Text lessonText;
    [SerializeField] RectTransform lessonArrow;
    bool lessonWanted;

    [Header("Editor Preview")]
    [Tooltip("편집 모드에서 타이밍 링을 표시한다. 끄면 힘 게이지를 표시한다.")]
    [SerializeField] bool previewTiming;

    /// <summary>"다음" 버튼을 눌렀을 때. 끝낼 수 있는지는 받는 쪽이 다시 확인한다.</summary>
    public event Action NextClicked;

    /// <summary>화면 아래 가운데 누르기 버튼. 튜토리얼이 가리킬 자리다.</summary>
    public RectTransform PressButton => hitButton != null ? (RectTransform)hitButton.transform : null;
    /// <summary>오른쪽 아래 "다음" 버튼.</summary>
    public Button NextButton => finishButton;

    static string L(string ko, string en) =>
        GameStateManager.Instance != null && GameStateManager.Instance.Language == ELanguage.En ? en : ko;

    void Awake()
    {
        if (!Application.isPlaying) return;
        if (finishButton != null) finishButton.onClick.AddListener(OnNextClicked);
        if (lessonArrow != null) lessonArrowBase = lessonArrow.anchoredPosition;
    }

    void OnEnable() { if (!Application.isPlaying) Preview(); }
    void Update() { if (!Application.isPlaying) Preview(); }

    void Preview()
    {
        if (body == null || powerGauge == null || rhythmRings == null) return;
        var rules = new CapOpenRules();
        rules.Press();
        if (previewTiming)
        {
            float remaining = rules.Tuning.powerTarget / rules.Tuning.powerSpeed;
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, CapOpenRules.MaxStep);
                rules.Tick(step);
                remaining = Mathf.Max(0f, remaining - step);
            }
            rules.Press();
            while (rules.Phase == ECapOpenPhase.PowerDone) rules.Tick(CapOpenRules.MaxStep);
        }
        Render(rules, "beer", 165f, 44f, false, true);
    }

    void OnNextClicked() => NextClicked?.Invoke();

    /// <summary>
    /// 한 프레임을 그린다. 웹 sync()의 캔버스 부분을 순서 그대로 옮겼다.
    /// </summary>
    public void Render(CapOpenRules rules, string ingredientId, float startRadius, float targetRadius, bool paused, bool preview = false)
    {
        if (body == null || powerGauge == null || rhythmRings == null) return;
        var tuning = rules.Tuning;
        CapOpenFx? fx = rules.Fx;
        float t = fx?.Age ?? 0f;
        bool ok = fx.HasValue && fx.Value.Success;
        bool miss = fx.HasValue && !fx.Value.Success && t < .55f;
        bool perfect = fx.HasValue && fx.Value.Perfect;
        bool broken = rules.Phase == ECapOpenPhase.Broken;
        var identity = CapAffine2D.Identity;

        foreach (var layer in new[] { halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay, powerGauge, rhythmRings }) layer.Clear();

        // 할로
        halo.SetTexture(ok ? haloSuccess : haloDefault);
        halo.DrawTexture(identity, Cx - 450f, Cy - 450f, 900f, 900f, Color.white);

        // 병: 성공·실패 순간 옆으로 튄다. 깨진 동안은 흐리게 남긴다.
        float s = BottleScale;
        float kick = ok && t < .24f ? Mathf.Sin(t * 42f) * Mathf.Exp(-t * 18f) * 6f
            : miss ? Mathf.Sin(t * 48f) * Mathf.Exp(-t * 10f) * 5f : 0f;
        float bottleAlpha = broken ? .2f : 1f;
        var bottle = identity.Translate(Cx + kick, Cy);
        body.Image(bottle, 22f, 72f, 43f, 150f, -21f * s, 3f * s, 43f * s, 150f * s, bottleAlpha);

        if (ok && ingredientId == "beer")
        {
            carbonation ??= new CapCarbonationCanvas();
            carbonationLayer.SetTexture(carbonation.Texture);
            carbonation.Draw(t);
            Rect area = CapCarbonationCanvas.BottleRect;
            carbonationLayer.DrawTexture(bottle.Scale(s, s), area.x, area.y, area.width, area.height, new Color(1f, 1f, 1f, bottleAlpha));
        }

        if (ok)
        {
            mouth.FillEllipse(bottle, 0f, 3f * s, 8f * s, 1.5f * s, WithAlpha(Hex("#38291c"), bottleAlpha));
            mouth.StrokeEllipse(bottle, 0f, 3f * s, 8f * s, 1.5f * s, 2f, WithAlpha(Hex("#af9669"), bottleAlpha));
        }

        // 뚜껑: 성공하면 포물선으로 날아가고, 빗나가면 비틀리며 떨린다.
        var capMatrix = bottle;
        float capAlpha = bottleAlpha;
        if (ok)
        {
            float launch = perfect ? 1.2f : 1f;
            capMatrix = capMatrix.Translate(260f * t * launch, -690f * t * launch + 300f * t * t).Rotate(-12f * t * launch);
            capAlpha *= Mathf.Max(0f, 1f - (t - .65f) * 2f);
        }
        else if (miss)
        {
            float decay = Mathf.Exp(-t * 9f);
            float side = fx.Value.Offset < 0f ? 1f : -1f;
            capMatrix = capMatrix.Translate(-10f * Mathf.Sin(t * 36f) * decay, 4f * decay)
                                 .Rotate(side * .24f * Mathf.Sin(t * 28f) * decay)
                                 .Multiply(1f, 0f, .25f * decay, 1f - .22f * decay, 0f, 0f);
        }
        cap.Image(capMatrix, 33f, 66f, 21f, 6f, -10f * s, -3f * s, 21f * s, 6f * s, capAlpha);

        if (ok && perfect && t < .7f)
        {
            var gold = WithAlpha(Hex("#ffe5a0"), Mathf.Max(0f, 1f - t / .7f));
            perfectBurst.StrokeCircle(identity, Cx, Cy, 48f + t * 160f, 2f, gold);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f, distance = 50f + t * 220f;
                float x = Cx + Mathf.Cos(angle) * distance, y = Cy + Mathf.Sin(angle) * distance;
                perfectBurst.StrokePolyline(identity, new[] { new Vector2(x - 5f, y), new Vector2(x + 5f, y) }, 2f, gold);
                perfectBurst.StrokePolyline(identity, new[] { new Vector2(x, y - 5f), new Vector2(x, y + 5f) }, 2f, gold);
            }
        }

        // 병따개: 판정 직후 0.45초 동안 지렛대가 움직인다.
        bool strike = fx.HasValue && t < .45f;
        var openerMatrix = identity
            .Translate(Cx + 48f + (strike ? Mathf.Sin(t * 15f) * 35f : 35f), Cy + 36f + (strike ? Mathf.Sin(t * 15f) * 20f : 35f))
            .Rotate(-.9f + (strike ? (ok ? -1f : 1f) * Mathf.Sin(t * 13f) * .6f : 0f));
        opener.Image(openerMatrix, 13f, 63f, 45f, 44f, -25f, -15f, 135f, 132f, ok ? Mathf.Max(0f, 1f - t * 3f) : .85f);

        if (!ok && rules.Phase == ECapOpenPhase.Rhythm)
        {
            float radius = Mathf.Max(12f, startRadius - (startRadius - targetRadius) * rules.BeatTime / tuning.beatSec);
            // 웹 shadowBlur 12(#60edc0)는 점점 넓고 옅은 고리를 겹쳐 흉내 낸다.
            var glow = Hex("#60edc0");
            for (int i = 4; i >= 1; i--) rhythmRings.StrokeCircle(identity, Cx, Cy, targetRadius, 3f + i * 5f, WithAlpha(glow, .022f * (5 - i)));
            rhythmRings.StrokeCircle(identity, Cx, Cy, targetRadius, 3f, Hex("#7cf3c6"));
            rhythmRings.StrokeCircle(identity, Cx, Cy, radius, 2f, miss ? Hex("#f18d89") : Hex("#e1faff"));
        }
        else if (ok && t < .65f)
        {
            float alpha = 1f - t / .65f;
            overlay.StrokeCircle(identity, Cx, Cy, 44f + t * 210f, 3f, WithAlpha(Hex("#a4ffdd"), alpha));
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 2.399f, r = 30f + t * (110f + i * 8f);
                var color = WithAlpha(i % 3 != 0 ? Hex("#9cffe1") : Hex("#ffe7a8"), alpha);
                overlay.FillRect(identity, Cx + Mathf.Cos(angle) * r, Cy + Mathf.Sin(angle) * r - t * 80f, 3f, 5f, color);
            }
        }

        bool gauge = rules.Phase is ECapOpenPhase.Ready or ECapOpenPhase.Power or ECapOpenPhase.PowerDone;
        if (gauge)
        {
            const float x = 400f, y = 240f, h = 250f;
            float top = tuning.powerTarget + tuning.powerWindow, perfectTop = tuning.powerTarget + tuning.powerPerfectWindow;
            powerGauge.FillRect(identity, x, y, 30f, h, Hex("#17252fee"));
            powerGauge.FillRect(identity, x, y + h * (1f - top), 30f, h * tuning.powerWindow * 2f, Hex("#42775d"));
            powerGauge.FillRect(identity, x, y + h * (1f - perfectTop), 30f, h * tuning.powerPerfectWindow * 2f, Hex("#8bc99b"));
            powerGauge.FillRect(identity, x - 9f, y + h * (1f - rules.PowerPos) - 2f, 48f, 4f, Hex("#e8f6ee"));
        }

        if (!ok && rules.RoundMisses > 0)
        {
            var crack = Hex("#c8dbd4aa");
            var points = new Vector2[6];
            for (int j = 0; j < Mathf.Min(3, rules.RoundMisses); j++)
            {
                float bx = Cx - 28f + j * 22f, by = Cy + 130f + j * 45f;
                points[0] = new Vector2(bx, by);
                for (int i = 1; i < 6; i++) points[i] = new Vector2(bx + Mathf.Sin(i * 2f + j) * 19f, by + i * 10f);
                overlay.StrokePolyline(identity, points, 2f, crack);
            }
        }

        if (broken)
        {
            float age = tuning.brokenDelay - rules.Delay;
            for (int i = 0; i < 36; i++)
            {
                var color = i % 3 != 0 ? Hex("#61432e") : Hex("#a2c5cb");
                overlay.FillRect(identity, Cx + Mathf.Sin(i * 2.4f) * age * 220f,
                                 Cy + 90f + i * 8f - age * 180f + age * age * 160f, 4f + i % 3 * 2f, 7f, color);
            }
        }

        foreach (var layer in new[] { halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay, powerGauge, rhythmRings }) layer.Apply();

        if (!preview) RenderUi(rules, ok, miss, perfect, gauge, paused);
    }

    /// <summary>
    /// 수업 안내를 켠다. text가 비어 있으면 끈다. power가 true면 게이지 칸을, 아니면 병 둘레 원을 뚫는다.
    /// </summary>
    public void SetLesson(string text, bool power)
    {
        lessonWanted = !string.IsNullOrEmpty(text);
        if (!lessonWanted) return;
        lessonShade.SetFocus(power ? powerFocus : rhythmFocus);
        if (lessonText.text != text) lessonText.text = text;
    }

    void RenderUi(CapOpenRules rules, bool ok, bool miss, bool perfect, bool gauge, bool paused)
    {
        timingHint.gameObject.SetActive(rules.InBeatWindow && !paused);

        feedback.text = ok ? (perfect ? "PERFECT!" : "POP!")
            : rules.Phase == ECapOpenPhase.Broken ? L("다시 시도", "TRY AGAIN")
            : miss ? "MISS" : string.Empty;
        if (perfect)
        {
            feedback.characterSpacing = 2f / 32f * 100f;
            feedback.color = Hex("#ffe3a0");
        }
        else
        {
            feedback.characterSpacing = miss ? 0f : 4f / 42f * 100f;
            feedback.color = miss ? Hex("#efa09b") : Hex("#8affd1");
        }

        phaseLabel.text = rules.Phase switch
        {
            ECapOpenPhase.Ready => L("Space / 클릭으로 시작", "Space / click to start"),
            ECapOpenPhase.Power => L("1 · 힘 조절 — 초록 구간에서 누르기", "1 · POWER — press in the green zone"),
            ECapOpenPhase.PowerDone => L("힘 조절 성공", "Power set"),
            ECapOpenPhase.Rhythm => L("2 · 타이밍 — 두 원이 겹칠 때 누르기", "2 · TIMING — press when the rings overlap"),
            ECapOpenPhase.Broken => L("병이 깨졌어요 · 다시 준비 중", "Bottle broken · preparing another"),
            _ => L("병따기 완료", "Bottle opened"),
        };

        gaugeLabel.gameObject.SetActive(gauge);
        if (gauge) gaugeLabel.text = L("힘", "POWER");

        hitKey.SetActive(!rules.Completed);
        hitLabel.text = rules.Completed ? "OPEN!"
            : !rules.Started ? L("시작", "Start")
            : rules.Phase == ECapOpenPhase.Power ? L("힘 조절", "Set power") : L("뚜껑 따기", "Pop the cap");
        hitButton.interactable = !rules.Completed;
        hitGroup.alpha = rules.Completed ? .75f : 1f;

        bool ready = rules.CanFinish;
        finishLabel.text = L("다음 →", "Next →");
        finishButton.interactable = ready;
        finishGroup.alpha = ready ? 1f : .36f;

        // 웹도 일시정지 중에는 수업 안내를 지운다.
        bool lesson = lessonWanted && !paused;
        if (lessonRoot.activeSelf != lesson) lessonRoot.SetActive(lesson);
        if (lesson)
        {
            // 웹 @keyframes opening-guide-point: 1초 주기로 오른쪽 8px까지 갔다 돌아온다(ease-in-out).
            // CSS right:-44px라서 화살표 칸(44px)의 왼쪽 끝이 안내 상자 오른쪽 끝에 붙는다.
            float phase = Mathf.Repeat(Time.unscaledTime, 1f);
            float nudge = (1f - Mathf.Cos(phase * Mathf.PI * 2f)) * .5f * 8f;
            lessonArrow.anchoredPosition = lessonArrowBase + new Vector2(nudge, 0f);
        }
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    void OnDestroy()
    {
        carbonation?.Dispose();
        if (finishButton != null) finishButton.onClick.RemoveListener(OnNextClicked);
    }
}
