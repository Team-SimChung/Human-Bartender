using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>서빙 위치와 트레이 표시를 담당한다. 대본의 구형 제조 메뉴는 별도로 연다.</summary>
public class CraftServingView : MonoBehaviour
{
    [Tooltip("대본의 주문 없는 튜토리얼에서만 여는 기존 제조 패널.")]
    [SerializeField] private LeftSlidePanel craftPanel;
    [Tooltip("주문 없는 튜토리얼에서만 켜는 기존 Craft Panel 오브젝트.")]
    [SerializeField] private GameObject legacyMenuRoot;

    [Tooltip("서빙 동안에만 켜는 트레이 등. 레시피 브라우저가 있는 Left Slide Panel Canvas는 넣지 않는다.")]
    [SerializeField] private GameObject[] craftUiRoots;

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
            owner.OpenServeUi();
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
        if (craftPanel == null || legacyMenuRoot == null)
        {
            Debug.LogWarning("[CraftServingView] 튜토리얼용 기존 패널과 Craft Panel 참조를 연결해 주세요.", this);
            return;
        }

        craftPanel.gameObject.SetActive(true);
        legacyMenuRoot.SetActive(true);
        craftPanel.Open();
    }

    private void OpenServeUi()
    {
        closeWhenIdle = false;
        SetCraftUiActive(true);
    }

    public void CloseUi()
    {
        closeWhenIdle = false;
        if (craftPanel != null && craftPanel.gameObject.activeInHierarchy) craftPanel.Close();
        if (legacyMenuRoot != null) legacyMenuRoot.SetActive(false);
        if (craftPanel != null) craftPanel.gameObject.SetActive(false);
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
    /// 레시피 브라우저의 Canvas는 서비스 패널이 소유하므로 여기서는 트레이만 켜고 끈다.
    /// </summary>
    void SetCraftUiActive(bool active)
    {
        if (craftUiRoots != null)
        {
            foreach (var root in craftUiRoots)
            {
                if (root != null) root.SetActive(active);
            }
        }

    }
}
