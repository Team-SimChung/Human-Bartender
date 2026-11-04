using System.Collections.Generic;
using UnityEngine;

/// <summary>바의 단골·튜토리얼 대화만 기록한다. 일반 영업의 손님 말풍선은 구독하지 않는다.</summary>
public sealed class BarDialogueHistory : MonoBehaviour
{
    [SerializeField] DynamicSpeechBubble[] storyBubbles = System.Array.Empty<DynamicSpeechBubble>();
    [SerializeField] PlayPhaseController phases;
    [SerializeField] StoryScriptRunner storyRunner;
    [SerializeField] CraftFlowController craft;

    // 기록 수집, 열람 버튼, 화면 열기가 함께 사용하는 허용 조건.
    public bool CanUseHistory => phases != null && phases.CanReceiveInput(EPlayPhase.Dialogue) &&
        storyRunner != null && storyRunner.IsPresentingLine &&
        (craft == null || (!craft.IsBusy && !craft.IsCraftFlowActive && !craft.IsAwaitingServeDecision));

    sealed class Source
    {
        public DialogueTextPlayer Player;
        public int Session;
        public int Entry = -1;
    }

    readonly DialogueHistoryStore store = new();
    readonly Dictionary<DialogueTextPlayer, Source> sources = new();

    void OnEnable()
    {
        foreach (var bubble in storyBubbles) Bind(bubble);
    }

    void Bind(DynamicSpeechBubble bubble)
    {
        if (bubble == null) return;
        var player = bubble.TextPlayer;
        if (sources.ContainsKey(player)) return;
        sources.Add(player, new Source { Player = player });
        player.LineStarted += Capture;
        player.LineEnding += Finish;
    }

    void OnDisable()
    {
        foreach (var source in sources.Values)
        {
            if (source.Entry >= 0) store.Complete(source.Entry);
            if (source.Player == null) continue;
            source.Player.LineStarted -= Capture;
            source.Player.LineEnding -= Finish;
        }
        sources.Clear();
    }

    void Capture(DialogueTextPlayer player)
    {
        if (!CanUseHistory || !player.isActiveAndEnabled) return;
        if (!sources.TryGetValue(player, out var source)) return;
        int session = player.CurrentSessionId;
        if (session != source.Session)
        {
            if (source.Entry >= 0) store.Complete(source.Entry);
            source.Session = session;
            source.Entry = -1;
        }
        if (session == 0 || source.Entry >= 0) return;
        // 현재 발화의 전체 본문을 한 번만 기록한다. 타이핑이나 스킵은 별도 재생기가 소유한다.
        string text = player.ReadFullText();
        if (string.IsNullOrWhiteSpace(text)) return;
        source.Entry = store.Append(player.SpeakerName, text);
    }

    void Finish(DialogueTextPlayer player)
    {
        if (!CanUseHistory) return;
        Capture(player);
        if (sources.TryGetValue(player, out var source) && source.Entry >= 0)
            store.Complete(source.Entry);
    }

    public DialogueHistoryStore.Entry[] Snapshot()
    {
        if (!CanUseHistory) return store.Snapshot(-1);
        int current = -1;
        foreach (var source in sources.Values)
        {
            Capture(source.Player);
            if (source.Player.CurrentSessionId != 0 && source.Player.isActiveAndEnabled)
                current = Mathf.Max(current, source.Entry);
        }
        return store.Snapshot(current);
    }
}
