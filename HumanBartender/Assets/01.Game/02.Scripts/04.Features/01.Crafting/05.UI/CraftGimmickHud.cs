using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기믹이 바뀌어도 자리를 지키는 공통 표시. 어떤 기믹을 하고 있든 화면 네 귀퉁이의 정보는 같다.
///
/// 기믹 프리팹 안이 아니라 밖에 산다. 전체 제조시간은 기믹을 넘나들며 이어지고, 기믹 하나가
/// 끝나 파괴되는 순간에도 시간과 남은 화면은 그대로 있어야 하기 때문이다.
///
/// 무엇을 얼마나 넣었는지는 기믹마다 재는 것이 달라서 기믹에게 물어본다(ICraftGimmickProgress).
/// 여기서는 그 문자열을 어디에 놓을지만 안다.
/// </summary>
public class CraftGimmickHud : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("기믹이 도는 동안만 켜지는 묶음. 제조가 끝나면 통째로 꺼진다.")]
    [SerializeField] GameObject root;

    [Header("Texts")]
    [Tooltip("좌측 상단. 지금까지 넣은 양과 목표. 기믹이 만들어 준 문자열을 그대로 쓴다.")]
    [SerializeField] TMP_Text progressText;
    [Tooltip("상단 중앙. 지금 다루는 재료 이름. 재료를 쓰지 않는 기믹에서는 기믹 이름이 들어간다.")]
    [SerializeField] TMP_Text subjectText;
    [Tooltip("우측 상단. 스터와 같은 시계 아이콘과 분:초로 한 잔 전체의 경과 시간을 표시한다.")]
    [SerializeField] TMP_Text timeText;
    [Tooltip("시계 아이콘과 시간 텍스트를 함께 감추기 위한 묶음.")]
    [SerializeField] GameObject timeRoot;
    [SerializeField] Vector2 timeInset = new Vector2(73f, 54f);

    [Header("Pour / Fill-up")]
    [SerializeField] GameObject quantityRoot;
    [SerializeField] TMP_Text targetValueText;
    [SerializeField] TMP_Text currentValueText;
    [SerializeField] GameObject ingredientRoot;
    [SerializeField] GameObject tiltRoot;
    [SerializeField] RectTransform tiltMarker;
    [SerializeField] RectTransform tiltFlowRange;
    [SerializeField] RectTransform tiltWarningRange;
    [SerializeField] TMP_Text nextButtonLabel;

    [Header("O.K")]
    [Tooltip("목표에 도달했을 때 화면 가운데에서 깜빡인다. 기믹을 끝내지는 않는다.")]
    [SerializeField] GameObject okMark;
    [SerializeField] float okBlinkPerSecond = 2f;

    [Header("Next")]
    [Tooltip("우측 하단. 지금까지의 결과로 기믹을 끝낸다. 끝낼 수 없는 기믹에서는 숨긴다.")]
    [SerializeField] Button nextButton;

    ICraftGimmickProgress progress;
    ICraftGimmickManualEnd manualEnd;
    ICraftGimmickQuantityProgress quantity;

    /// <summary>이번 기믹이 자기가 그리겠다고 가져간 자리. 공통은 그 자리를 비워 둔다.</summary>
    ECraftHudPart ownedByGimmick;
    CraftTimer timer;

    /// <summary>
    /// 시간을 가릴지 여부. 플레이어가 고른 재료 중 정답이 하나도 없으면 제조시간도 알려 주지 않는다.
    /// 가리는 건 화면뿐이고 실제 시간은 그대로 재서 기록에 남는다.
    /// </summary>
    bool hideTime;
    bool UsesWebLayout => quantityRoot != null && targetValueText != null && currentValueText != null;

    void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);

        SetVisible(false);
    }

    /// <summary>
    /// 그리기 순서를 밖에서 정한다. 기믹 프리팹이 저마다 다른 순서를 쓰기 때문에,
    /// 공통 표시가 항상 그 위에 오도록 실행기가 기믹을 띄우면서 함께 맞춰 준다.
    /// </summary>
    public void SetSortingOrder(int order)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(OnNextClicked);
    }

    /// <summary>제조를 시작할 때 한 번 부른다. 이 잔 내내 바뀌지 않는 값을 받는다.</summary>
    public void BeginCraft(CraftTimer craftTimer, float cocktailTimeLimitSec, bool hideTimeDisplay)
    {
        timer = craftTimer;
        hideTime = hideTimeDisplay;

        SetVisible(true);
        RefreshTimeLayout();
    }

    /// <summary>지금 진행 표시에 연결된 기믹. 튜토리얼이 안내 단계를 정할 때 읽기만 한다.</summary>
    public object CurrentGimmick { get; private set; }
    /// <summary>목표량·현재량을 보여 주는 진행 문구. 튜토리얼 강조 대상이다.</summary>
    public Transform ProgressTarget => quantityRoot != null && quantityRoot.activeInHierarchy
        ? quantityRoot.transform
        : progressText != null && progressText.gameObject.activeInHierarchy ? progressText.transform : null;
    /// <summary>기믹을 직접 끝내는 버튼(따르기 마치기 등). 튜토리얼 강조 대상이다.</summary>
    public Transform NextControl => nextButton != null && nextButton.gameObject.activeInHierarchy ? nextButton.transform : null;

    /// <summary>기믹 하나를 시작할 때 부른다. 대상 이름을 바꾸고 진행 표시를 그 기믹에 연결한다.</summary>
    public void BindGimmick(GimmickStep step, object gimmick)
    {
        CurrentGimmick = gimmick;
        progress = gimmick as ICraftGimmickProgress;
        manualEnd = gimmick as ICraftGimmickManualEnd;
        quantity = gimmick as ICraftGimmickQuantityProgress;
        ownedByGimmick = (gimmick as ICraftGimmickOwnHud)?.OwnedParts ?? ECraftHudPart.None;

        // 기믹이 가져간 자리는 통째로 감춘다. 그러지 않으면 같은 정보가 두 군데 뜬다.
        SetPartActive(progressText, ECraftHudPart.Progress);
        SetPartActive(subjectText, ECraftHudPart.Subject);
        SetPartActive(timeText, ECraftHudPart.Time);
        if (timeRoot != null) timeRoot.SetActive(!Owns(ECraftHudPart.Time));

        bool mixing = step.Type == ECraftGimmick.Shake || step.Type == ECraftGimmick.Stir;
        bool quantityVisible = UsesWebLayout && quantity != null && !Owns(ECraftHudPart.Progress);
        if (quantityRoot != null) quantityRoot.SetActive(quantityVisible);
        if (ingredientRoot != null) ingredientRoot.SetActive(quantity != null && !Owns(ECraftHudPart.Subject));
        if (tiltRoot != null) tiltRoot.SetActive(quantity != null);
        if (progressText != null && (quantityVisible || mixing && UsesWebLayout)) progressText.gameObject.SetActive(false);
        if (subjectText != null) subjectText.gameObject.SetActive((quantity != null || !UsesWebLayout) && !Owns(ECraftHudPart.Subject));

        if (subjectText != null)
            subjectText.text = quantity != null ? quantity.IngredientName : string.IsNullOrEmpty(step.IngredientId)
                ? step.Type.ToString()
                : step.IngredientId;

        // 끝낼 수 없는 기믹에서는 버튼 자체를 감춘다. 병따기는 성공해야만 끝나므로 여기 해당한다.
        if (nextButton != null)
            nextButton.gameObject.SetActive(manualEnd != null && (!mixing || !UsesWebLayout) && !Owns(ECraftHudPart.NextButton));
        if (nextButtonLabel != null)
            nextButtonLabel.text = quantity != null ? "따르기 마치기 →" : "다음 →";

        if (okMark != null) okMark.SetActive(false);

        Refresh();
    }

    bool Owns(ECraftHudPart part) => (ownedByGimmick & part) != 0;

    void SetPartActive(TMP_Text text, ECraftHudPart part)
    {
        if (text != null) text.gameObject.SetActive(!Owns(part));
    }

    /// <summary>기믹 하나가 끝났을 때 부른다. 다음 기믹이 연결될 때까지 진행 표시를 비운다.</summary>
    public void UnbindGimmick()
    {
        CurrentGimmick = null;
        progress = null;
        manualEnd = null;
        quantity = null;
        ownedByGimmick = ECraftHudPart.None;

        if (okMark != null) okMark.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (progressText != null) progressText.text = string.Empty;
        if (quantityRoot != null) quantityRoot.SetActive(false);
        if (ingredientRoot != null) ingredientRoot.SetActive(false);
        if (tiltRoot != null) tiltRoot.SetActive(false);
    }

    /// <summary>제조가 끝나면 부른다. 결과 연출로 넘어가므로 표시를 모두 감춘다.</summary>
    public void EndCraft()
    {
        UnbindGimmick();
        timer = null;

        SetVisible(false);
    }

    void Update()
    {
        Refresh();
    }

    void LateUpdate() => RefreshTimeLayout();

    void RefreshTimeLayout()
    {
        if (!(transform is RectTransform canvasRect)) return;
        // 별도 개발 씬의 예전 HUD는 기존 좌표계를 유지한다. 새 참조가 연결된 HUD만 웹 배치를 쓴다.
        if (!UsesWebLayout)
        {
            if (timeRoot == null || !(timeRoot.transform is RectTransform oldTimer)) return;
            var scaler = GetComponent<CanvasScaler>();
            Vector2 reference = scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
            if (reference.x <= 0f || reference.y <= 0f) return;
            float oldScale = Mathf.Min(canvasRect.rect.width / reference.x, canvasRect.rect.height / reference.y);
            if (oldScale <= 0f) return;
            oldTimer.localScale = Vector3.one * oldScale;
            oldTimer.anchoredPosition = -timeInset * oldScale;
            return;
        }
        if (root == null || !(root.transform is RectTransform stage) || stage == canvasRect) return;
        float scale = Mathf.Min(canvasRect.rect.width / 1280f, canvasRect.rect.height / 720f);
        if (scale <= 0f) return;
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
        stage.sizeDelta = new Vector2(1280f, 720f);
        stage.anchoredPosition = Vector2.zero;
        stage.localScale = Vector3.one * scale;
        if (timeRoot != null && timeRoot.transform is RectTransform rect)
        {
            rect.localScale = Vector3.one;
            rect.anchoredPosition = -timeInset;
        }
    }

    void Refresh()
    {
        if (root != null && !root.activeSelf) return;

        if (quantity != null)
        {
            if (targetValueText != null) targetValueText.text = quantity.TargetValueText;
            if (currentValueText != null) currentValueText.text = quantity.CurrentValueText;
            if (subjectText != null) subjectText.text = quantity.IngredientName;
            if (tiltMarker != null)
            {
                Vector2 anchor = new Vector2(quantity.TiltRatio, .5f);
                tiltMarker.anchorMin = tiltMarker.anchorMax = anchor;
                tiltMarker.anchoredPosition = Vector2.zero;
            }
            SetRange(tiltFlowRange, quantity.FlowStartRatio, quantity.FullFlowRatio);
            SetRange(tiltWarningRange, quantity.FullFlowRatio, 1f);
        }
        if (nextButton != null) nextButton.interactable = manualEnd != null && manualEnd.CanEndNow;

        if (progressText != null && !Owns(ECraftHudPart.Progress))
            progressText.text = progress != null ? progress.ProgressText : string.Empty;

        if (timeText != null && !Owns(ECraftHudPart.Time))
        {
            // 제한시간을 넘겨도 계속 센다. 넘겼다는 사실은 마지막에 한 번 판정한다.
            timeText.text = hideTime || timer == null
                ? "??:??"
                : CraftTimer.FormatElapsed(timer.ElapsedSec);
        }

        RefreshOkMark();
    }

    static void SetRange(RectTransform rect, float start, float end)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(Mathf.Clamp01(start), 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(end), 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>목표에 도달해 있는 동안 깜빡인다. 도달 상태가 풀리면 곧바로 끈다.</summary>
    void RefreshOkMark()
    {
        if (okMark == null) return;

        bool show = progress != null && progress.ShowOkMark;

        if (!show)
        {
            if (okMark.activeSelf) okMark.SetActive(false);
            return;
        }

        bool visiblePhase = Mathf.Repeat(Time.time * okBlinkPerSecond, 1f) < 0.5f;
        if (okMark.activeSelf != visiblePhase) okMark.SetActive(visiblePhase);
    }

    void OnNextClicked()
    {
        Debug.Log(
        $"[CraftHud] 다음 클릭 / 연결={manualEnd != null} / " +
        $"종료가능={manualEnd?.CanEndNow}");
        // 눌렀는데 아무 일도 안 일어나는 게 제일 알기 어렵다. 무시했다면 왜 무시했는지 남긴다.
        if (manualEnd == null)
        {
            Debug.LogWarning("[CraftHud] 다음 버튼을 눌렀지만 지금 기믹은 직접 끝낼 수 없습니다. " +
                             "기믹이 연결되지 않았거나 ICraftGimmickManualEnd를 구현하지 않았습니다.");
            return;
        }

        if (!manualEnd.CanEndNow)
        {
            Debug.LogWarning($"[CraftHud] '{manualEnd.GetType().Name}'이 아직 끝낼 수 있는 상태가 아닙니다. " +
                             "시작 전이거나 이미 끝난 기믹입니다.");
            return;
        }

        manualEnd.EndNow();
    }

    void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }
}
