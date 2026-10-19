using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Collections.Generic;

/// <summary>서빙 위치와 트레이 표시를 담당한다.</summary>
public class CraftServingView : MonoBehaviour
{
    [Tooltip("서빙 동안에만 켜는 트레이 등. 레시피 브라우저가 있는 Left Slide Panel Canvas는 넣지 않는다.")]
    [SerializeField] private GameObject[] craftUiRoots;

    [Header("Serve")]
    [Tooltip("좌석에 앉은 인물의 위치를 묻는 곳. 서빙 자리를 그 앞에 놓는다.")]
    [SerializeField] private DialogueCharacterManager characterManager;

    [Tooltip("서빙 자리를 올려놓을 캔버스. 완성 잔 트레이가 쓰는 것과 같아야 잔을 끌어다 놓을 수 있다.")]
    [SerializeField] private Canvas serveCanvas;
    [SerializeField] private StoryServeDropTarget serveZonePrefab;

    [Tooltip("좌석 월드 좌표에서 서빙 자리까지의 어긋남. 좌석의 좌우·깊이 위치를 조정하며 높이는 책상 앵커를 사용한다.")]
    [SerializeField] private Vector3 serveZoneWorldOffset = new(0f, -0.6f, 0f);

    [Tooltip("책상 표면의 서빙 높이. 인물 리그의 중심 높이와 별도로 배치한다.")]
    [SerializeField] private Transform tabletopAnchor;
    [SerializeField] private float tabletopWorldY = -1.5f;

    [Tooltip("서빙 자리를 눈에 보이게 할지. 코스터 그림이 붙기 전까지 자리를 확인하는 용도다.")]
    [SerializeField] private bool showServeZoneGuide = true;

    [Header("Craft lifetime")]
    [SerializeField] private CraftFlowController craftFlow;
    int openPresentations;
    bool closeWhenIdle;
    CoasterLesson coasterLesson;
    readonly List<(GameObject zone, string actor)> trackedZones = new();

    public RectTransform CoasterTarget => coasterLesson?.TargetRect;
    public RectTransform CoasterSupply
    {
        get
        {
            if (serveCanvas == null) return null;
            foreach (var item in serveCanvas.GetComponentsInChildren<CoasterDragItem>(true))
                if (!item.IsPlaced) return item.TrayRect;
            return null;
        }
    }

    public IOrderPresentation CreatePresentation(OrderDetails order) => new Presentation(this, order);

    public CoasterLesson CreateCoasterLesson(string actor)
    {
        coasterLesson?.Dispose();
        return coasterLesson = new(this, actor);
    }

    public sealed class CoasterLesson : IDisposable
    {
        readonly CraftServingView owner;
        readonly string actor;
        readonly UniTaskCompletionSource placed = new();
        StoryCoasterDropTarget target;
        public RectTransform TargetRect => target != null ? (RectTransform)target.transform : null;

        public CoasterLesson(CraftServingView owner, string actor)
        {
            this.owner = owner;
            this.actor = actor;
            var zone = owner.BuildServeZone(new OrderDetails("tutorial-coaster", actor, "gin_tonic"));
            zone.name = "Story Coaster Lesson (" + actor + ")";
            zone.GetComponent<StoryServeDropTarget>().enabled = false;
            target = zone.AddComponent<StoryCoasterDropTarget>();
            owner.openPresentations++;
            owner.OpenServeUi();
            // Activating an initially hidden serve canvas runs StoryServeDropTarget.Awake,
            // which disables its graphic. Bind the coaster input after that initialization.
            target.Bind(() => placed.TrySetResult());
        }

        public UniTask WaitAsync(CancellationToken token) => placed.Task.AttachExternalCancellation(token);

        internal bool TransferToOrder(string receiver, out GameObject zone, out CoasterDragItem coaster)
        {
            zone = null;
            coaster = null;
            if (target == null || receiver != actor || target.PlacedCoaster == null) return false;
            zone = target.gameObject;
            coaster = target.PlacedCoaster;
            Destroy(target);
            target = null;
            owner.coasterLesson = null;
            owner.openPresentations--;
            zone.GetComponent<StoryServeDropTarget>().enabled = true;
            return true;
        }

        public void Dispose()
        {
            if (target == null) return;
            if (target.PlacedCoaster != null) target.PlacedCoaster.ReturnToTray();
            Destroy(target.gameObject);
            target = null;
            if (owner.coasterLesson == this) owner.coasterLesson = null;
            owner.openPresentations--;
            if (owner.openPresentations == 0) owner.CloseUi();
        }
    }

    sealed class Presentation : IOrderPresentation
    {
        readonly CraftServingView owner;
        readonly OrderDetails order;
        GameObject zone;
        CoasterDragItem coaster;
        bool opened;

        public Presentation(CraftServingView owner, OrderDetails order)
        {
            this.owner = owner;
            this.order = order;
        }

        public void Open(Func<CraftedDrink, bool> receive)
        {
            if (owner.coasterLesson == null ||
                !owner.coasterLesson.TransferToOrder(order.ReceiverId, out zone, out coaster))
                zone = owner.BuildServeZone(order);
            zone.GetComponent<StoryServeDropTarget>().Bind(receive);
            owner.openPresentations++;
            opened = true;
            owner.OpenServeUi();
        }

        public void Unbind()
        {
            if (zone == null) return;
            if (coaster != null) { coaster.ReturnToTray(); coaster = null; }
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

    void OnEnable() => Canvas.willRenderCanvases += UpdateServePositions;
    void OnDisable() => Canvas.willRenderCanvases -= UpdateServePositions;

    void UpdateServePositions()
    {
        for (int i = trackedZones.Count - 1; i >= 0; i--)
        {
            var tracked = trackedZones[i];
            if (tracked.zone == null) { trackedZones.RemoveAt(i); continue; }
            ((RectTransform)tracked.zone.transform).anchoredPosition = ResolveSeatCanvasPoint(tracked.actor);
        }
    }

    private void OpenServeUi()
    {
        closeWhenIdle = false;
        SetCraftUiActive(true);
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
        if (serveCanvas == null || characterManager == null || serveZonePrefab == null)
        {
            throw new InvalidOperationException("serveCanvas, characterManager, serveZonePrefab 연결이 필요합니다.");
        }

        StoryServeDropTarget zone = Instantiate(serveZonePrefab, serveCanvas.transform);
        GameObject go = zone.gameObject;
        go.name = $"Story Serve Zone ({order.ReceiverId})";
        RectTransform rect = (RectTransform)zone.transform;
        rect.anchoredPosition = ResolveSeatCanvasPoint(order.ReceiverId);
        trackedZones.Add((go, order.ReceiverId));

        Image image = go.GetComponent<Image>();
        if (image != null && !showServeZoneGuide)
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);
        // 평소에는 잔 클릭을 가리지 않고, 잔을 집은 동안에만 StoryServeDropTarget이 켠다.
        if (image != null) image.raycastTarget = false;

        return go;
    }

    Vector2 ResolveSeatCanvasPoint(string actorId)
    {
        Vector3 world = characterManager.GetCharacterPosition(actorId) + serveZoneWorldOffset;
        world.y = tabletopAnchor != null ? tabletopAnchor.position.y : tabletopWorldY;

        Camera camera = Camera.main;
        if (camera == null) return Vector2.zero;

        Vector2 screenPoint = camera.WorldToScreenPoint(world);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)serveCanvas.transform, screenPoint,
            serveCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : serveCanvas.worldCamera, out Vector2 localPoint);

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
