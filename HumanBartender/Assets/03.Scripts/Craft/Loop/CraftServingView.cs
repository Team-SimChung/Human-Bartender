using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>제조 메뉴와 서빙 위치 표시만 담당한다. 주문 상태와 판정·정산은 소유하지 않는다.</summary>
public class CraftServingView : MonoBehaviour
{
    [Tooltip("칵테일 메뉴가 들어 있는 좌측 슬라이드 패널. 제조 스텝에서 열어 준다.")]
    [SerializeField] LeftSlidePanel craftPanel;

    [Tooltip("제조·서빙 동안에만 켜는 화면. 좌측 슬라이드 패널 캔버스와 완성 잔 트레이 캔버스를 꽂는다. " +
             "2부 대화 중에는 꺼 두어야 대사 위에 얹히지 않는다.")]
    [SerializeField] GameObject[] craftUiRoots;

    [Header("Serve")]
    [Tooltip("좌석에 앉은 인물의 위치를 묻는 곳. 서빙 자리를 그 앞에 놓는다.")]
    [SerializeField] DialogueCharacterManager characterManager;

    [Tooltip("서빙 자리를 올려놓을 캔버스. 완성 잔 트레이가 쓰는 것과 같아야 잔을 끌어다 놓을 수 있다.")]
    [SerializeField] Canvas serveCanvas;

    [SerializeField] Vector2 serveZoneSize = new(220f, 180f);

    [Tooltip("좌석 월드 좌표에서 서빙 자리까지의 어긋남. 인물의 손 앞에 오도록 맞춘다.")]
    [SerializeField] Vector3 serveZoneWorldOffset = new(0f, -0.6f, 0f);

    [Tooltip("서빙 자리를 눈에 보이게 할지. 코스터 그림이 붙기 전까지 자리를 확인하는 용도다.")]
    [SerializeField] bool showServeZoneGuide = true;

    [SerializeField] Color serveZoneColor = new(1f, 1f, 1f, 0.12f);

    [Header("Craft lifetime")]
    [SerializeField] CraftFlowController craftFlow;
    int openPresentations;
    bool closeWhenIdle;

    public IOrderPresentation CreatePresentation(OrderDetails order) => new Presentation(this, order);

    sealed class Presentation : IOrderPresentation
    {
        readonly CraftServingView owner;
        readonly OrderDetails order;
        GameObject zone;
        bool opened;

        public Presentation(CraftServingView owner, OrderDetails order)
        {
            this.owner = owner;
            this.order = order;
        }

        public void Open(Func<CraftedDrink, bool> receive)
        {
            zone = owner.BuildServeZone(order);
            zone.GetComponent<StoryServeDropTarget>().Bind(receive);
            owner.openPresentations++;
            opened = true;
            owner.OpenUi();
        }

        public void Unbind()
        {
            if (zone == null) return;
            zone.GetComponent<StoryServeDropTarget>().Bind(null);
            Destroy(zone);
            zone = null;
        }

        public async UniTask CloseAsync()
        {
            await UniTask.NextFrame();
            if (owner == null || !opened) return;
            opened = false;
            owner.openPresentations--;
            if (owner.openPresentations != 0) return;
            // 주문 취소로 제조 오브젝트까지 비활성화하지 않는다.
            if (owner.craftFlow != null && owner.craftFlow.IsBusy)
                owner.closeWhenIdle = true;
            else
                owner.CloseUi();
        }
    }

    void LateUpdate()
    {
        if (closeWhenIdle && openPresentations == 0 && (craftFlow == null || !craftFlow.IsBusy))
            CloseUi();
    }

    public void OpenUi()
    {
        closeWhenIdle = false;
        SetCraftUiActive(true);
        if (craftPanel == null || !craftPanel.gameObject.activeInHierarchy)
            throw new InvalidOperationException("제조 메뉴 연결을 확인하세요.");
        craftPanel.Open();
    }

    public void CloseUi()
    {
        closeWhenIdle = false;
        SetCraftUiActive(false);
    }

    /// <summary>
    /// 주문자 앞에 서빙 자리를 놓는다.
    ///
    /// 좌석의 월드 좌표를 화면 좌표로 옮겨 캔버스 위에 놓는다 — 말풍선을 화자 머리 위에 맞추는 것과
    /// 같은 방식이다(UIDialogueTextView.SetBubblePosition). 좌석이 화면 어디에 잡히는지는
    /// 카메라가 정하므로 캔버스 좌표를 고정값으로 둘 수 없다.
    /// </summary>
    GameObject BuildServeZone(OrderDetails order)
    {
        if (serveCanvas == null || characterManager == null)
        {
            throw new InvalidOperationException("serveCanvas 또는 characterManager 연결이 필요합니다.");
        }

        var go = new GameObject($"Story Serve Zone ({order.ReceiverId})",
            typeof(RectTransform), typeof(Image), typeof(StoryServeDropTarget));
        go.transform.SetParent(serveCanvas.transform, false);

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = serveZoneSize;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = ResolveSeatCanvasPoint(order.ReceiverId);

        var image = go.GetComponent<Image>();
        image.color = showServeZoneGuide ? serveZoneColor : new Color(0f, 0f, 0f, 0f);
        // 평소에는 잔 클릭을 가리지 않고, 잔을 집은 동안에만 StoryServeDropTarget이 켠다.
        image.raycastTarget = false;

        return go;
    }

    Vector2 ResolveSeatCanvasPoint(string actorId)
    {
        Vector3 world = characterManager.GetCharacterPosition(actorId) + serveZoneWorldOffset;

        Camera camera = Camera.main;
        if (camera == null) return Vector2.zero;

        Vector2 screenPoint = camera.WorldToScreenPoint(world);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)serveCanvas.transform, screenPoint, null, out Vector2 localPoint);

        return localPoint;
    }

    // ── 화면 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 제조·서빙 화면을 켜고 끈다.
    ///
    /// 닫는 일을 끄기 <b>전에</b> 한다. 슬라이드 패널은 코루틴으로 움직이는데 꺼진 오브젝트에서는
    /// 코루틴이 시작조차 되지 않아서, 순서를 뒤집으면 "Coroutine couldn't be started because the
    /// game object is inactive"가 난다.
    ///
    /// 패널 자신은 craftUiRoots와 별개로 켠다. 인스펙터에서 그 목록에 빠뜨리면 제조가 통째로 막히는데,
    /// 화면에는 아무것도 나지 않아 원인이 멀다.
    /// </summary>
    void SetCraftUiActive(bool active)
    {
        if (!active && craftPanel != null && craftPanel.gameObject.activeInHierarchy) craftPanel.Close();

        if (craftUiRoots != null)
        {
            foreach (var root in craftUiRoots)
            {
                if (root != null) root.SetActive(active);
            }
        }

        if (craftPanel != null) craftPanel.gameObject.SetActive(active);
    }
}
