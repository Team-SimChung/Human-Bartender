using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바에서 플레이어가 실제로 본 대사를 기록한다.
/// 단골·튜토리얼 대화창은 드러난 글자를 따라가다 줄이 끝나면 전문을 남긴다.
/// 영업 중 손님 말풍선은 그 좌석을 보고 있고 카메라가 멈춰 있을 때 드러난 글자까지만 남긴다.
/// 기록을 읽어도 타이핑을 끝내거나 다음 줄로 넘기지 않는다.
/// </summary>
public sealed class BarDialogueHistory : MonoBehaviour
{
    [Header("단골·튜토리얼 대화")]
    [SerializeField] DynamicSpeechBubble[] storyBubbles = System.Array.Empty<DynamicSpeechBubble>();
    [SerializeField] PlayPhaseController phases;
    [SerializeField] StoryScriptRunner storyRunner;
    [SerializeField] CraftFlowController craft;

    [Header("영업 중 손님 말풍선")]
    [Tooltip("기록할 손님 좌석. 비워 두면 같은 씬의 GuestSlot을 실행 시 모두 찾는다.")]
    [SerializeField] GuestSlot[] guestSlots = System.Array.Empty<GuestSlot>();
    [Tooltip("선택한 좌석과 카메라 이동 여부를 읽는다. 비워 두면 같은 씬에서 찾는다.")]
    [SerializeField] PlayCamera playCamera;
    [Tooltip("단골 손님 이름 표(characters.csv). 비워 두면 불러온 이야기 데이터의 인물 표를 쓴다.")]
    [SerializeField] NewCharacterDataSO characterData;
    [Tooltip("일반 손님 이름으로 쓰는 성격 이름 표(guests/personalities.csv). 비워 두면 불러온 표를 실행 시 찾는다.")]
    [SerializeField] NewPersonalityDataSO personalityData;

    bool CraftIdle => craft == null || (!craft.IsBusy && !craft.IsCraftFlowActive && !craft.IsAwaitingServeDecision);
    bool PresentingStoryLine => phases != null && phases.CanReceiveInput(EPlayPhase.Dialogue) &&
        storyRunner != null && storyRunner.IsPresentingLine;
    bool InService => phases != null && phases.CanReceiveInput(EPlayPhase.Tycoon);
    /// <summary>
    /// 2부에서 주문을 받고 제조를 기다리는 중. 서비스 패널이 열리는 조건(ServicePanelController.CanOpen)과 같다.
    /// 튜토리얼이 조작을 안내하는 중(0일차 진토닉·1일차 맥주 제조 실습)에는 웹처럼 잠근다 — 안내 창이 기록 화면 위에 남는다.
    /// </summary>
    bool AwaitingStoryCraft => phases != null && phases.CanReceiveInput(EPlayPhase.Dialogue) &&
        storyRunner != null && storyRunner.CanStartCraft && !storyRunner.IsGuidingLesson;

    /// <summary>
    /// 열람 버튼과 화면 열기가 함께 쓰는 허용 조건. 단골·튜토리얼 대사 중, 2부 제조 대기 중, 영업 중이면 열 수 있다.
    /// 제조 화면에 들어가 있는 동안에는 열지 않는다.
    /// </summary>
    public bool CanUseHistory => CraftIdle && (PresentingStoryLine || AwaitingStoryCraft || InService);

    // 단골·튜토리얼 대화창의 글자를 기록하는 조건. 예전 열람 조건과 같다.
    bool CanRecordStory => CraftIdle && PresentingStoryLine;

    /// <summary>손님 말풍선을 '본 것'으로 치는 조건. 영업 중 그 좌석을 보고 있고 카메라가 멈춰 있어야 한다.</summary>
    internal static bool SeesGuestLine(bool inService, bool craftIdle, bool paused,
        ESlotType seat, ESlotType selected, bool cameraMoving) =>
        inService && craftIdle && !paused && seat == selected && !cameraMoving;

    sealed class Source
    {
        public DialogueTextPlayer Player;
        public GuestSlot Seat;
        public bool Story;     // 단골·튜토리얼 대화창이면 true, 손님 좌석 말풍선이면 false
        public bool StoryLine; // 단골·튜토리얼 조건에서 시작한 줄
        public readonly DialogueHistoryStore.Line Line = new();
    }

    readonly DialogueHistoryStore store = new();
    readonly Dictionary<DialogueTextPlayer, Source> sources = new();

    void OnEnable()
    {
        FindSceneReferences();
        foreach (var bubble in storyBubbles)
            if (bubble != null) Bind(bubble.TextPlayer, null);
        foreach (var seat in guestSlots)
            if (seat != null) Bind(seat.BarkPlayer, seat);
    }

    // 손님 말풍선은 씬 연결 없이 같은 씬에서 찾는다. Inspector에 넣으면 그 값을 쓴다.
    void FindSceneReferences()
    {
        if (guestSlots == null || guestSlots.Length == 0)
        {
            var found = new List<GuestSlot>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<GuestSlot>(true));
            guestSlots = found.ToArray();
        }
        if (playCamera == null)
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if ((playCamera = root.GetComponentInChildren<PlayCamera>(true)) != null) break;
        if (personalityData == null)
        {
            var loaded = Resources.FindObjectsOfTypeAll<NewPersonalityDataSO>();
            if (loaded.Length > 0) personalityData = loaded[0];
        }
        if (guestSlots.Length > 0 && playCamera == null)
            Debug.LogWarning("[DialogueHistory] PlayCamera를 찾지 못해 손님 말풍선을 기록하지 않습니다.", this);
    }

    void Bind(DialogueTextPlayer player, GuestSlot seat)
    {
        if (player == null || sources.ContainsKey(player)) return;
        sources.Add(player, new Source { Player = player, Seat = seat, Story = seat == null });
        player.LineStarted += Started;
        player.LineEnding += Finished;
    }

    void OnDisable()
    {
        foreach (var source in sources.Values)
        {
            if (source.Line.Entry >= 0) store.Complete(source.Line.Entry);
            if (source.Player == null) continue;
            source.Player.LineStarted -= Started;
            source.Player.LineEnding -= Finished;
        }
        sources.Clear();
    }

    // 좌석을 옮기거나 카메라가 멈추는 순간에도 본 글자가 늘어날 수 있어 매 프레임 확인한다.
    void LateUpdate()
    {
        foreach (var source in sources.Values) Observe(source);
    }

    void Started(DialogueTextPlayer player)
    {
        if (!sources.TryGetValue(player, out var source)) return;
        store.Track(source.Line, player.CurrentSessionId);
        source.StoryLine = source.Story && CanRecordStory;
        Observe(source);
    }

    void Finished(DialogueTextPlayer player)
    {
        if (!sources.TryGetValue(player, out var source)) return;
        Observe(source);
        // 끝난 단골·튜토리얼 대사는 전문으로 남긴다. 손님 말풍선은 본 글자까지만 남는다.
        bool story = source.Story && (source.Line.Entry >= 0 || source.StoryLine || CanRecordStory);
        store.End(source.Line, player.SpeakerName, story ? player.ReadFullText() : null);
        source.StoryLine = false;
    }

    void Observe(Source source)
    {
        var player = source.Player;
        if (player == null) return;
        store.Track(source.Line, player.CurrentSessionId);
        if (!IsShowing(source) || !CanSee(source)) return;
        int visible = player.VisibleCharacterCount;
        if (visible <= source.Line.Recorded) return;
        store.See(source.Line, SpeakerOf(source), visible, player.ReadVisibleText());
    }

    static bool IsShowing(Source source) =>
        source.Player != null && source.Line.Session != 0 && source.Player.gameObject.activeInHierarchy;

    bool CanSee(Source source)
    {
        if (source.Story) return CanRecordStory;
        return source.Seat != null && playCamera != null && SeesGuestLine(InService, CraftIdle, GamePause.IsPaused,
            source.Seat.SlotType, playCamera.SelectedSeat, playCamera.IsMoving);
    }

    string SpeakerOf(Source source) =>
        source.Story ? source.Player.SpeakerName : GuestName(source.Seat != null ? source.Seat.CurrentGuest : null);

    // 웹과 같이 단골은 인물 이름, 일반 손님은 성격 이름으로 적는다. 표에 없으면 id를 그대로 쓴다.
    string GuestName(Guest guest)
    {
        if (guest == null) return "";
        if (guest.isRegular)
        {
            var characters = characterData != null ? characterData.characterData : NewDataLoadManager.StoryCharacters;
            if (characters != null)
                foreach (var character in characters)
                    if (character.Id == guest.characterId && !string.IsNullOrEmpty(character.Name.Ko))
                        return character.Name.Ko;
            return guest.characterId ?? "";
        }
        if (personalityData != null && personalityData.personalityData != null)
            foreach (var personality in personalityData.personalityData)
                if (personality.Id == guest.personality && !string.IsNullOrEmpty(personality.Name.Ko))
                    return personality.Name.Ko;
        return guest.personality ?? "";
    }

    /// <summary>
    /// 창을 여는 순간의 기록 사본. 현재 줄은 지금 보고 있는 대화창·좌석의 줄이며 드러난 글자까지만 담긴다.
    /// 읽기만 하므로 타이핑을 끝내거나 다음 줄로 넘기지 않는다.
    /// </summary>
    public DialogueHistoryStore.Entry[] Snapshot()
    {
        int current = -1;
        foreach (var source in sources.Values)
        {
            Observe(source);
            if (source.Line.Entry >= 0 && IsShowing(source) && CanSee(source))
                current = Mathf.Max(current, source.Line.Entry);
        }
        return store.Snapshot(current);
    }
}
