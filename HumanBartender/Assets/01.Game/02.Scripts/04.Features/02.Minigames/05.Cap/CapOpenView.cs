using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 병따기 화면. 웹 본편 open-view.js의 캔버스 그리기와 그 위에 얹힌 글자·버튼·수업 안내를 그대로 옮겼다.
///
/// 웹 수치를 바꾸지 않으려고 좌표계를 웹에 맞춘다. 월드 쪽은 무대 하나(stage)를 기믹 카메라 중앙에 두고
/// y를 뒤집어 1280×720 캔버스 픽셀을 그대로 쓰고, UI 쪽은 1280×720 기준 캔버스에 CSS 위치를 그대로 쓴다.
/// 판정은 모른다 — CapManager가 넘겨준 규칙 상태를 읽어 그리기만 한다.
/// </summary>
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
    [SerializeField] int sortingOrder = 0;
    [Tooltip("글자·버튼 캔버스의 정렬 순서. 실행기가 띄울 때 기준값을 더해 바 UI 위로 올린다.")]
    [SerializeField] int canvasSortingOrder = 20;

    Transform stage;
    Camera stageCamera;
    CapCanvasLayer halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay;
    Texture2D haloDefault, haloSuccess;
    CapCarbonationCanvas carbonation;

    Canvas canvas;
    RectTransform webRoot;
    TMP_Text phaseLabel, feedback, timingHint, gaugeLabel, hitLabel, finishLabel;
    Button hitButton, finishButton;
    CanvasGroup hitGroup, finishGroup;
    GameObject hitKey;

    GameObject lessonRoot;
    RawImage lessonShade;
    Texture2D lessonPowerMask, lessonRingMask;
    TMP_Text lessonText;
    RectTransform lessonArrow;
    bool lessonWanted;

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
        // 실행기는 프리팹을 띄운 직후 화면 공간 캔버스의 정렬을 올린다. 그 전에 캔버스가 있어야 함께 올라간다.
        BuildWorld();
        BuildUi();
    }

    // ── 월드: 웹 캔버스 ───────────────────────────────────────────────

    void BuildWorld()
    {
        stage = new GameObject("Web Stage").transform;
        stage.SetParent(transform, false);

        int layerId = 0;
        int order = sortingOrder;
        halo = CapCanvasLayer.Create(stage, "Halo", null, order++, layerId);
        body = CapCanvasLayer.Create(stage, "Bottle", bottleTexture, order++, layerId);
        carbonation = new CapCarbonationCanvas();
        carbonationLayer = CapCanvasLayer.Create(stage, "Carbonation", carbonation.Texture, order++, layerId);
        mouth = CapCanvasLayer.Create(stage, "Bottle Mouth", null, order++, layerId);
        cap = CapCanvasLayer.Create(stage, "Cap", bottleTexture, order++, layerId);
        perfectBurst = CapCanvasLayer.Create(stage, "Perfect Burst", null, order++, layerId);
        opener = CapCanvasLayer.Create(stage, "Opener", openerTexture, order++, layerId);
        overlay = CapCanvasLayer.Create(stage, "Rings Gauge Cracks", null, order, layerId);

        haloDefault = CreateHalo(Hex("#19364926"));
        haloSuccess = CreateHalo(Hex("#124e4740"));
        halo.SetTexture(haloDefault);
    }

    /// <summary>웹: createRadialGradient(640,310,25, 640,310,450), 안쪽 색 → #080e1700.</summary>
    static Texture2D CreateHalo(Color inner)
    {
        const int size = 256;
        var outer = Hex("#080e1700");
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "CapHalo", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 텍스처 한 변이 지름 900px(반지름 450)이다.
                float dx = (x + .5f) / size * 900f - 450f, dy = (y + .5f) / size * 900f - 450f;
                float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 25f) / 425f);
                pixels[y * size + x] = Color.Lerp(inner, outer, t);
            }
        texture.SetPixels(pixels);
        texture.Apply(false);
        return texture;
    }

    /// <summary>무대를 기믹 카메라 중앙에 맞춘다. 카메라 높이 720px이 웹 캔버스 높이와 같아지도록 줄인다.</summary>
    void PlaceStage()
    {
        if (stageCamera == null) stageCamera = CraftGimmickStage.Find(this);
        if (stageCamera == null) return;

        float k = stageCamera.orthographic ? stageCamera.orthographicSize * 2f / WebHeight : 1f / 100f;
        Vector3 center = stageCamera.transform.position;
        stage.position = new Vector3(center.x - Cx * k, center.y + WebHeight * .5f * k, transform.position.z);
        Vector3 parent = stage.parent != null ? stage.parent.lossyScale : Vector3.one;
        stage.localScale = new Vector3(k / parent.x, -k / parent.y, 1f);
    }

    /// <summary>
    /// 한 프레임을 그린다. 웹 sync()의 캔버스 부분을 순서 그대로 옮겼다.
    /// </summary>
    public void Render(CapOpenRules rules, string ingredientId, float startRadius, float targetRadius, bool paused)
    {
        PlaceStage();
        var tuning = rules.Tuning;
        CapOpenFx? fx = rules.Fx;
        float t = fx?.Age ?? 0f;
        bool ok = fx.HasValue && fx.Value.Success;
        bool miss = fx.HasValue && !fx.Value.Success && t < .55f;
        bool perfect = fx.HasValue && fx.Value.Perfect;
        bool broken = rules.Phase == ECapOpenPhase.Broken;
        var identity = CapAffine2D.Identity;

        foreach (var layer in new[] { halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay }) layer.Clear();

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
            for (int i = 4; i >= 1; i--) overlay.StrokeCircle(identity, Cx, Cy, targetRadius, 3f + i * 5f, WithAlpha(glow, .022f * (5 - i)));
            overlay.StrokeCircle(identity, Cx, Cy, targetRadius, 3f, Hex("#7cf3c6"));
            overlay.StrokeCircle(identity, Cx, Cy, radius, 2f, miss ? Hex("#f18d89") : Hex("#e1faff"));
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

        bool gauge = rules.Phase is ECapOpenPhase.Power or ECapOpenPhase.PowerDone;
        if (gauge)
        {
            const float x = 400f, y = 240f, h = 250f;
            float top = tuning.powerTarget + tuning.powerWindow, perfectTop = tuning.powerTarget + tuning.powerPerfectWindow;
            overlay.FillRect(identity, x, y, 30f, h, Hex("#17252fee"));
            overlay.FillRect(identity, x, y + h * (1f - top), 30f, h * tuning.powerWindow * 2f, Hex("#42775d"));
            overlay.FillRect(identity, x, y + h * (1f - perfectTop), 30f, h * tuning.powerPerfectWindow * 2f, Hex("#8bc99b"));
            overlay.FillRect(identity, x - 9f, y + h * (1f - rules.PowerPos) - 2f, 48f, 4f, Hex("#e8f6ee"));
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

        foreach (var layer in new[] { halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay }) layer.Apply();

        RenderUi(rules, ok, miss, perfect, gauge, paused);
    }

    // ── UI: 웹 DOM ───────────────────────────────────────────────────

    void BuildUi()
    {
        var canvasObject = new GameObject("Cap Web Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WebWidth, WebHeight);
        // 월드 무대도 높이에 맞춰 줄이므로, 글자도 높이를 따라야 둘이 어긋나지 않는다.
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        webRoot = NewRect("Web Root", canvasObject.transform);
        webRoot.anchorMin = webRoot.anchorMax = new Vector2(.5f, .5f);
        webRoot.pivot = new Vector2(0f, 1f);
        webRoot.sizeDelta = new Vector2(WebWidth, WebHeight);
        webRoot.anchoredPosition = new Vector2(-WebWidth * .5f, WebHeight * .5f);

        // .opening-timing-hint: top 200, 가운데, 24px
        timingHint = Text("Timing Hint", webRoot, "Space", 24f, Hex("#d4fff0"), TextAlignmentOptions.Top);
        Place(timingHint.rectTransform, 0f, 200f, WebWidth, 40f);

        // .opening-feedback: left 780, top 272, 42px
        feedback = Text("Feedback", webRoot, string.Empty, 42f, Hex("#8affd1"), TextAlignmentOptions.TopLeft);
        Place(feedback.rectTransform, 780f, 272f, 400f, 60f);

        // .opening-phase: top 45, 가운데, 20px
        phaseLabel = Text("Phase", webRoot, string.Empty, 20f, Hex("#e7c88e"), TextAlignmentOptions.Top);
        Place(phaseLabel.rectTransform, 0f, 45f, WebWidth, 32f);

        // 캔버스 fillText('힘', 400, 222): 기준선이 222라서 글자 윗변을 그만큼 올린다.
        gaugeLabel = Text("Gauge Label", webRoot, string.Empty, 17f, Hex("#e8f6ee"), TextAlignmentOptions.TopLeft);
        Place(gaugeLabel.rectTransform, 400f, 222f - 17f, 120f, 24f);

        BuildHitButton();
        BuildFinishButton();
        BuildLesson();
    }

    void BuildHitButton()
    {
        // .opening-hit: 아래 40, 가운데, 최소 190, 배경 #0b202cf0, 글자 #dffaf5. 테두리는 .primary(#e1c389).
        var rect = NewRect("Opening Hit", webRoot);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 40f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite(4, 1, Hex("#0b202cf0"), Hex("#e1c389"));
        image.type = Image.Type.Sliced;
        hitButton = rect.gameObject.AddComponent<Button>();
        hitButton.transition = Selectable.Transition.None;
        // 누르기는 입력 라우터가 이미 받는다(클릭·Space). 버튼이 또 누르면 한 번에 두 번 판정한다.
        hitButton.navigation = new Navigation { mode = Navigation.Mode.None };
        hitGroup = rect.gameObject.AddComponent<CanvasGroup>();

        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 11, 11);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rect.gameObject.AddComponent<LayoutElement>().minWidth = 190f;

        hitKey = Kbd(rect, "Space", 12f, Hex("#e9dcb9")).gameObject;
        hitLabel = Text("Label", rect, string.Empty, 15f, Hex("#dffaf5"), TextAlignmentOptions.Center);
        hitLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void BuildFinishButton()
    {
        // .gimmick-finish: 오른쪽 22, 아래 18, 14px, 안쪽 여백 12/24. .primary 색.
        var rect = NewRect("Gimmick Finish", webRoot);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-22f, 18f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite(4, 1, Hex("#e1c389"), Hex("#e1c389"));
        image.type = Image.Type.Sliced;
        finishButton = rect.gameObject.AddComponent<Button>();
        finishButton.transition = Selectable.Transition.None;
        finishButton.onClick.AddListener(() => NextClicked?.Invoke());
        finishGroup = rect.gameObject.AddComponent<CanvasGroup>();

        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 12, 12);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        finishLabel = Text("Label", rect, L("다음 →", "Next →"), 14f, Hex("#1e2227"), TextAlignmentOptions.Center);
        finishLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void BuildLesson()
    {
        // .opening-lesson: 화면 전체를 #020611(0.55)로 덮고 볼 곳만 뚫는다. 입력은 막지 않는다.
        var root = NewRect("Opening Lesson", webRoot);
        Place(root, 0f, 0f, WebWidth, WebHeight);
        lessonRoot = root.gameObject;

        var shade = NewRect("Shade", root);
        Place(shade, 0f, 0f, WebWidth, WebHeight);
        lessonShade = shade.gameObject.AddComponent<RawImage>();
        lessonShade.color = WithAlpha(Hex("#020611"), .55f);
        lessonShade.raycastTarget = false;
        lessonPowerMask = CreateLessonMask(true);
        lessonRingMask = CreateLessonMask(false);

        // .opening-lesson-copy: left 70, top 230, 너비 290, 안쪽 20/24, 배경 #0a1522f5, 테두리 #68ced7, 둥글기 12.
        var copy = NewRect("Copy", root);
        copy.anchorMin = copy.anchorMax = new Vector2(0f, 1f);
        copy.pivot = new Vector2(0f, 1f);
        copy.anchoredPosition = new Vector2(70f, -230f);
        var background = copy.gameObject.AddComponent<Image>();
        background.sprite = RoundedSprite(12, 1, Hex("#0a1522f5"), Hex("#68ced7"));
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        var layout = copy.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 20, 20);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = copy.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        copy.sizeDelta = new Vector2(290f, 0f);

        Kbd(copy, "SPACE", 17f, Hex("#9ef5f0"));
        lessonText = Text("Text", copy, string.Empty, 20f, Hex("#e5ffff"), TextAlignmentOptions.TopLeft);
        // 웹 line-height 1.65
        lessonText.lineSpacing = 40f;
        var textLayout = lessonText.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 290f - 48f;

        var arrowText = Text("Arrow", copy, "→", 36f, Hex("#9ef5f0"), TextAlignmentOptions.Left);
        arrowText.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        lessonArrow = arrowText.rectTransform;
        lessonArrow.anchorMin = lessonArrow.anchorMax = new Vector2(1f, .5f);
        lessonArrow.pivot = new Vector2(0f, 1f);
        lessonArrow.sizeDelta = new Vector2(44f, 48f);

        lessonRoot.SetActive(false);
    }

    /// <summary>
    /// 수업 안내를 켠다. text가 비어 있으면 끈다. power가 true면 게이지 칸을, 아니면 병 둘레 원을 뚫는다.
    /// </summary>
    public void SetLesson(string text, bool power)
    {
        lessonWanted = !string.IsNullOrEmpty(text);
        if (!lessonWanted) return;
        lessonShade.texture = power ? lessonPowerMask : lessonRingMask;
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
            Place(feedback.rectTransform, 790f, 265f, 400f, 60f);
            feedback.fontSize = 32f;
            feedback.characterSpacing = 2f / 32f * 100f;
            feedback.color = Hex("#ffe3a0");
        }
        else
        {
            Place(feedback.rectTransform, 780f, 272f, 400f, 60f);
            feedback.fontSize = 42f;
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
            lessonArrow.anchoredPosition = new Vector2(nudge, 0f);
        }
    }

    /// <summary>웹 수업 가림막. 게이지 칸(또는 병 둘레 원)과 아래 두 버튼 자리를 둥근 모서리로 뚫는다.</summary>
    static Texture2D CreateLessonMask(bool power)
    {
        const int w = 640, h = 360;
        const float scale = WebWidth / w;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        { name = power ? "CapLessonPower" : "CapLessonRing", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[w * h];
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                float x = (col + .5f) * scale, y = (row + .5f) * scale;
                float distance = Mathf.Min(
                    power ? RoundedRectDistance(x, y, 374f, 196f, 100f, 318f, 12f) : Vector2.Distance(new Vector2(x, y), new Vector2(Cx, Cy)) - 190f,
                    Mathf.Min(RoundedRectDistance(x, y, 510f, 600f, 260f, 100f, 12f),
                              RoundedRectDistance(x, y, 1100f, 640f, 165f, 70f, 12f)));
                float alpha = Mathf.Clamp01(.5f + distance / scale);
                // 텍스처 행 0이 아래이므로 웹 y를 뒤집어 넣는다.
                pixels[(h - 1 - row) * w + col] = new Color(1f, 1f, 1f, alpha);
            }
        texture.SetPixels(pixels);
        texture.Apply(false);
        return texture;
    }

    /// <summary>둥근 사각형 바깥까지의 거리. 안쪽이면 음수다.</summary>
    static float RoundedRectDistance(float x, float y, float left, float top, float width, float height, float radius)
    {
        float cx = left + width * .5f, cy = top + height * .5f;
        float qx = Mathf.Abs(x - cx) - (width * .5f - radius), qy = Mathf.Abs(y - cy) - (height * .5f - radius);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    // ── 도우미 ───────────────────────────────────────────────────────

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>CSS의 left·top·width·height를 그대로 쓴다(웹 루트 왼쪽 위 기준).</summary>
    static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        var rect = NewRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>웹 kbd: 테두리 #6d7477, 아래 테두리 3px, 배경 #17212a, 둥글기 3, 안쪽 3/6.</summary>
    RectTransform Kbd(Transform parent, string label, float size, Color color)
    {
        var rect = NewRect("Kbd", parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite(3, 1, Hex("#17212a"), Hex("#6d7477"), 3);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 3, 5);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var text = Text("Label", rect, label, size, color, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return rect;
    }

    /// <summary>둥근 모서리 9분할 스프라이트를 만든다. bottom을 주면 아래 테두리만 그만큼 굵다.</summary>
    static Sprite RoundedSprite(int radius, int border, Color fill, Color stroke, int bottom = -1)
    {
        if (bottom < 0) bottom = border;
        int size = radius * 2 + 4 + bottom;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "CapRounded", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + .5f, py = y + .5f;
                float outer = RoundedRectDistance(px, py, 0f, 0f, size, size, radius);
                float coverage = Mathf.Clamp01(.5f - outer);
                if (coverage <= 0f) { pixels[y * size + x] = Color.clear; continue; }
                // 텍스처 y는 아래에서 위로 간다. 아래 테두리(웹 border-bottom)는 y가 작은 쪽이다.
                float inner = RoundedRectDistance(px, py, border, bottom, size - border * 2, size - border - bottom, Mathf.Max(0, radius - border));
                float fillAmount = Mathf.Clamp01(.5f - inner);
                Color color = Color.Lerp(stroke, fill, fillAmount);
                color.a *= coverage;
                pixels[y * size + x] = color;
            }
        texture.SetPixels(pixels);
        texture.Apply(false);
        int edge = radius + Mathf.Max(border, bottom) + 1;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
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
        foreach (var texture in new[] { haloDefault, haloSuccess, lessonPowerMask, lessonRingMask })
            if (texture != null) Destroy(texture);
    }
}
