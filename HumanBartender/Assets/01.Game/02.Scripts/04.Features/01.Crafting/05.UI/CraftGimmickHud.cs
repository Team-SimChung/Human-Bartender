using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기믹이 바뀌어도 자리를 지키는 공통 표시. 어떤 기믹을 하고 있든 화면 네 귀퉁이의 정보는 같다.
///
/// 씬의 공통 HUD 또는 프리팹의 전용 HUD로 사용한다. 전용 HUD가 있는 기믹에는 같은 제조 시계와
/// 표시를 넘기고, 튜토리얼이 읽는 목표량·마치기 버튼도 전용 HUD의 실제 대상으로 연결한다.
///
/// 무엇을 얼마나 넣었는지는 기믹마다 재는 것이 달라서 기믹에게 물어본다(ICraftGimmickProgress).
/// 여기서는 그 문자열을 어디에 놓을지만 안다.
/// </summary>
public class CraftGimmickHud : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("기믹이 도는 동안만 켜지는 묶음. 제조가 끝나면 통째로 꺼진다.")]
    [SerializeField] GameObject root;
    [Tooltip("프리팹에서는 잔·병과 함께 월드 공간에 배치하고, 제조를 시작할 때 화면 UI로 표시한다. 자식 배치는 유지한다.")]
    [SerializeField] bool useScreenSpaceDuringCraft;

    [Header("Texts")]
    [Tooltip("좌측 상단. 지금까지 넣은 양과 목표. 기믹이 만들어 준 문자열을 그대로 쓴다.")]
    [SerializeField] TMP_Text progressText;
    [Tooltip("상단 중앙. 지금 다루는 재료 이름. 재료를 쓰지 않는 기믹에서는 기믹 이름이 들어간다.")]
    [SerializeField] TMP_Text subjectText;
    [Tooltip("우측 상단. 스터와 같은 시계 아이콘과 분:초로 한 잔 전체의 경과 시간을 표시한다.")]
    [SerializeField] TMP_Text timeText;
    [Tooltip("시계 아이콘과 시간 텍스트를 함께 감추기 위한 묶음.")]
    [SerializeField] GameObject timeRoot;

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
    CraftGimmickHud dedicatedHud;
    float timeLimitSec;
    bool crafting;

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
        if (dedicatedHud != null) dedicatedHud.SetSortingOrder(order);
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(OnNextClicked);
    }

    /// <summary>제조를 시작할 때 한 번 부른다. 이 잔 내내 바뀌지 않는 값을 받는다.</summary>
    public void BeginCraft(CraftTimer craftTimer, float cocktailTimeLimitSec, bool hideTimeDisplay)
    {
        if (useScreenSpaceDuringCraft)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Canvas의 화면 좌표계만 전환한다. Panel 이하 편집한 RectTransform은 건드리지 않는다.
                transform.localScale = Vector3.one;
            }
        }
        timer = craftTimer;
        timeLimitSec = cocktailTimeLimitSec;
        hideTime = hideTimeDisplay;
        crafting = true;
        SetVisible(true);
    }

    /// <summary>지금 진행 표시에 연결된 기믹. 튜토리얼이 안내 단계를 정할 때 읽기만 한다.</summary>
    public object CurrentGimmick { get; private set; }
    /// <summary>목표량·현재량을 보여 주는 진행 문구. 튜토리얼 강조 대상이다.</summary>
    public Transform ProgressTarget => dedicatedHud != null ? dedicatedHud.ProgressTarget
        : quantityRoot != null && quantityRoot.activeInHierarchy
        ? quantityRoot.transform
        : progressText != null && progressText.gameObject.activeInHierarchy ? progressText.transform : null;
    /// <summary>기믹을 직접 끝내는 버튼(따르기 마치기 등). 튜토리얼 강조 대상이다.</summary>
    public Transform NextControl => dedicatedHud != null ? dedicatedHud.NextControl
        : nextButton != null && nextButton.gameObject.activeInHierarchy ? nextButton.transform : null;

    /// <summary>기믹 하나를 시작할 때 부른다. 대상 이름을 바꾸고 진행 표시를 그 기믹에 연결한다.</summary>
    public void BindGimmick(GimmickStep step, object gimmick)
    {
        if (dedicatedHud != null) dedicatedHud.EndCraft();
        dedicatedHud = null;
        CurrentGimmick = gimmick;
        var suppliedHud = (gimmick as ICraftGimmickHudProvider)?.GimmickHud;
        if (suppliedHud != null && suppliedHud != this)
        {
            dedicatedHud = suppliedHud;
            dedicatedHud.BeginCraft(timer, timeLimitSec, hideTime);
            var canvas = GetComponent<Canvas>();
            if (canvas != null) dedicatedHud.SetSortingOrder(canvas.sortingOrder);
            dedicatedHud.BindGimmick(step, gimmick);
            SetVisible(false);
            return;
        }
        SetVisible(crafting);
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
        if (dedicatedHud != null) dedicatedHud.EndCraft();
        dedicatedHud = null;
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
        SetVisible(crafting);
    }

    /// <summary>제조가 끝나면 부른다. 결과 연출로 넘어가므로 표시를 모두 감춘다.</summary>
    public void EndCraft()
    {
        crafting = false;
        UnbindGimmick();
        timer = null;

        SetVisible(false);
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (dedicatedHud != null) return;
        if (root != null && !root.activeSelf) return;

        if (quantity != null)
        {
            if (targetValueText != null) targetValueText.text = quantity.TargetValueText;
            if (currentValueText != null) currentValueText.text = quantity.CurrentValueText;
            if (subjectText != null) subjectText.text = quantity.IngredientName;
            if (tiltMarker != null)
            {
                tiltMarker.anchorMin = new Vector2(quantity.TiltRatio, tiltMarker.anchorMin.y);
                tiltMarker.anchorMax = new Vector2(quantity.TiltRatio, tiltMarker.anchorMax.y);
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
        rect.anchorMin = new Vector2(Mathf.Clamp01(start), rect.anchorMin.y);
        rect.anchorMax = new Vector2(Mathf.Clamp01(end), rect.anchorMax.y);
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
