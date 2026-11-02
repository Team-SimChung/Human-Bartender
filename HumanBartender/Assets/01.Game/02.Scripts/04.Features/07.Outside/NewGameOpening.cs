using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// 새 게임으로 집에 처음 도착했을 때의 오프닝. 플레이 안내 → 도입 컷신 → 이동·상호작용 안내 → "첫 출근" 목표 순서다.
/// 실행 여부는 진행 플래그가, 안내 문구와 시간은 이 컴포넌트의 직렬화 값이, 그림은 기존 튜토리얼 오버레이가 소유한다.
/// 도입 컷신 리소스가 아직 없으면 OptionalStoryCutscene 정책대로 건너뛴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewGameOpening : MonoBehaviour
{
    /// <summary>한 장의 안내 화면. 인스펙터에서 문구와 입력 잠금 시간을 편집한다.</summary>
    [Serializable]
    public sealed class GuidePage
    {
        public string Title;
        [TextArea(2, 5)] public string Body;
        [Tooltip("본문 아래 작은 글씨. 진행 키 안내에 쓴다.")]
        public string Footer;
        [Tooltip("이 시간 동안은 진행 입력을 받지 않는다. 직전 키 입력으로 바로 넘어가지 않게 한다.")]
        [Min(0)] public float InputLockSeconds;
        [Tooltip("켜면 스페이스바로도 넘긴다. 끄면 상호작용 키(E)만 받는다.")]
        public bool AcceptAdvanceKey;
        [Tooltip("뒤 화면을 가리는 정도. 1이면 완전히 가린다.")]
        [Range(0, 1)] public float ShadeAlpha = .72f;

        public GuidePage(string title, string body, string footer, float inputLock, bool acceptAdvance, float shade)
        {
            Title = title; Body = body; Footer = footer;
            InputLockSeconds = inputLock; AcceptAdvanceKey = acceptAdvance; ShadeAlpha = shade;
        }
    }

    // 오프닝을 끝까지 본 기록. 저장된 진행 플래그라 다시 불러와도 반복하지 않는다.
    const string SeenFlag = "flag.opening_seen";

    [Tooltip("안내 화면에 쓰는 기존 튜토리얼 오버레이 프리팹(StoryTutorialOverlay).")]
    [SerializeField] private StoryTutorialView viewPrefab;

    [Tooltip("cutscenes.csv의 도입 연출 id. 리소스가 연결되지 않았으면 건너뛴다.")]
    [SerializeField] private string introCutsceneId = "tl_intro";

    [SerializeField] private GuidePage playGuide = new("플레이 안내",
        "이 게임은 인물들과의 대화를 중심으로 이야기가 진행됩니다.\n대화는 E키 또는 스페이스바로 넘길 수 있습니다.",
        "E / SPACE", 1f, true, 1f);

    [SerializeField] private GuidePage[] controlsPages =
    {
        new("이동 · 달리기", "A와 D를 눌러 이동합니다.\nShift를 누른 채 이동하면 달립니다.",
            "1 / 2 · E키를 눌러 진행", 2f, false, .72f),
        new("상호작용", "E키를 눌러 오브젝트나 다른 인물들과 상호작용할 수 있습니다.",
            "2 / 2 · E키를 눌러 진행", .3f, false, .72f),
    };

    [Tooltip("안내가 끝난 뒤 띄우는 새 목표.")]
    [SerializeField] private string firstObjective = "첫 출근하기!";

    [Inject] private ICutScenePlayer cutscenes;
    [Inject] private IConditionUtil conditions;
    [Inject] private IPopupService popups;

    /// <summary>오프닝을 아직 보지 않았는지. 씬 컨텍스트가 시작 자리와 함께 판단한다.</summary>
    public bool IsPending => conditions != null && !conditions.CheckRequired(SeenFlag);

    /// <summary>플레이어 조작을 잠근 채 오프닝을 끝까지 진행하고 완료를 기록한다.</summary>
    public async UniTask PlayAsync(Player player, CancellationToken token)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (conditions == null) throw new InvalidOperationException("NewGameOpening이 주입되지 않았습니다.");
        if (!IsPending) return;

        var view = StoryTutorialView.Create(viewPrefab, null);
        try
        {
            using (InteractionStateLease.Acquire(player, EInteractorState.Lock))
            {
                await ShowAsync(view, playGuide, token);
                await OptionalStoryCutscene.PlayAsync(cutscenes, introCutsceneId, token);
                foreach (var page in controlsPages) await ShowAsync(view, page, token);
                // 안내를 닫은 키 입력이 같은 프레임에 문·TV 상호작용으로 이어지지 않게 한 프레임 뒤에 조작을 돌려준다.
                await UniTask.NextFrame(token);
            }
            conditions.ApplyRequired(SeenFlag + " = true");
        }
        finally
        {
            if (view != null) Destroy(view.gameObject);
        }
        if (popups != null && !string.IsNullOrWhiteSpace(firstObjective))
            popups.Popup(PopupType.NewObjective, firstObjective).Forget();
    }

    static UniTask ShowAsync(StoryTutorialView view, GuidePage page, CancellationToken token) =>
        view.ShowPageAsync(page.Title, page.Body, page.Footer, page.ShadeAlpha, page.InputLockSeconds,
            () => GameKeyBindings.WasPressed(GameControl.Interact) ||
                  page.AcceptAdvanceKey && GameKeyBindings.WasPressed(GameControl.Advance), token);

    private void OnValidate()
    {
        if (viewPrefab == null) Debug.LogWarning("[NewGameOpening] View Prefab(StoryTutorialOverlay)을 연결하세요.", this);
        if (string.IsNullOrWhiteSpace(introCutsceneId)) Debug.LogWarning("[NewGameOpening] 도입 컷신 id가 비어 있습니다.", this);
    }
}
