using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 수정 목록 22: 대화 기록은 플레이어가 실제로 본 글자만 담는다.
/// 출력 중인 줄은 드러난 글자까지만, 손님 말풍선은 그 좌석을 보고 있을 때만 기록하고,
/// 기록을 읽어도 문장을 끝까지 공개하거나 다음 줄로 넘기지 않는다. 영업 중에도 기록을 열 수 있다.
/// </summary>
public sealed class DialogueHistoryCaptureTests
{
    const string BubblePrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/PlayDialogueTextBox.prefab";
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    Scene scene;
    readonly List<DialogueTextPlayer> players = new();
    readonly List<Object> assets = new();

    [SetUp]
    public void SetUp() => scene = EditorSceneManager.NewPreviewScene();

    [TearDown]
    public void TearDown()
    {
        foreach (var player in players)
            if (player != null) player.Stop();
        players.Clear();
        foreach (var asset in assets)
            if (asset != null) Object.DestroyImmediate(asset);
        assets.Clear();
        EditorSceneManager.ClosePreviewScene(scene);
    }

    // ── 기록 정본 ───────────────────────────────────────────────────────

    [Test]
    public void StoreKeepsOnlyTheSeenPrefixAndGrowsIt()
    {
        var store = new DialogueHistoryStore();
        var line = new DialogueHistoryStore.Line();
        store.Track(line, 1);
        Assert.That(store.Snapshot(-1), Is.Empty, "아직 한 글자도 보지 않은 줄은 기록하지 않는다.");

        store.See(line, "온화형", 2, "안녕");
        store.See(line, "온화형", 1, "안");
        Assert.That(Texts(store.Snapshot(line.Entry)), Is.EqualTo(new[] { "안녕" }), "덜 보인 값으로 기록을 줄이지 않는다.");
        Assert.That(store.Snapshot(line.Entry)[0].IsCurrent, Is.True);

        store.See(line, "온화형", 4, "안녕하세");
        Assert.That(Texts(store.Snapshot(line.Entry)), Is.EqualTo(new[] { "안녕하세" }), "더 보이면 같은 기록을 늘린다.");

        store.End(line, "온화형");
        var rows = store.Snapshot(line.Entry);
        Assert.That(Texts(rows), Is.EqualTo(new[] { "안녕하세" }), "손님 말풍선은 끝나도 본 글자까지만 남는다.");
        Assert.That(rows[0].IsCurrent, Is.False);
        Assert.That(rows[0].Speaker, Is.EqualTo("온화형"));
    }

    [Test]
    public void StoreEndsStoryLinesWithFullTextAndSeparatesRepeatedLines()
    {
        var store = new DialogueHistoryStore();
        var line = new DialogueHistoryStore.Line();
        store.Track(line, 1);
        store.See(line, "루나", 2, "안녕");
        store.End(line, "루나", "안녕하세요");
        store.Track(line, 2);
        store.End(line, "루나", "글자가 나오기 전에 넘긴 줄");
        store.Track(line, 3);
        store.See(line, "루나", 5, "안녕하세요");
        store.Track(line, 0);

        var rows = store.Snapshot(-1);
        Assert.That(Texts(rows), Is.EqualTo(new[] { "안녕하세요", "글자가 나오기 전에 넘긴 줄", "안녕하세요" }),
            "끝난 단골·튜토리얼 대사는 전문으로 남고, 같은 문장을 다시 말하면 새 기록이 된다.");
        Assert.That(rows.Any(row => row.IsCurrent), Is.False);
    }

    // ── 수집·열람 조건 ──────────────────────────────────────────────────

    [Test]
    public void GuestLineCountsAsSeenOnlyOnTheSelectedSeatWhileTheCameraIsStill()
    {
        Assert.That(BarDialogueHistory.SeesGuestLine(true, true, false, ESlotType.Left, ESlotType.Left, false), Is.True);
        Assert.That(BarDialogueHistory.SeesGuestLine(true, true, false, ESlotType.Left, ESlotType.Middle, false), Is.False, "다른 좌석");
        Assert.That(BarDialogueHistory.SeesGuestLine(true, true, false, ESlotType.Left, ESlotType.Left, true), Is.False, "카메라 이동 중");
        Assert.That(BarDialogueHistory.SeesGuestLine(false, true, false, ESlotType.Left, ESlotType.Left, false), Is.False, "영업 중이 아님");
        Assert.That(BarDialogueHistory.SeesGuestLine(true, false, false, ESlotType.Left, ESlotType.Left, false), Is.False, "제조 중");
        Assert.That(BarDialogueHistory.SeesGuestLine(true, true, true, ESlotType.Left, ESlotType.Left, false), Is.False, "일시정지 중");
    }

    [Test]
    public void HistoryCanOpenDuringServiceAndWhileAStoryLineIsShown()
    {
        var phases = Root("Phases").AddComponent<PlayPhaseController>();
        var history = Root("History").AddComponent<BarDialogueHistory>();
        Set(history, "phases", phases);

        SetPhase(phases, EPlayPhase.None);
        Assert.That(history.CanUseHistory, Is.False);
        SetPhase(phases, EPlayPhase.Tycoon);
        Assert.That(history.CanUseHistory, Is.True, "영업 중에도 기록을 열 수 있어야 한다.");
        SetPhase(phases, EPlayPhase.Dialogue);
        Assert.That(history.CanUseHistory, Is.False, "단골 대화 단계는 대사를 보여 주는 동안에만 연다(기존 동작).");

        var runner = Root("Runner").AddComponent<StoryScriptRunner>();
        Set(history, "storyRunner", runner);
        SetProperty(runner, "IsPresentingLine", true);
        Assert.That(history.CanUseHistory, Is.True);
    }

    // ── 실제 말풍선 ─────────────────────────────────────────────────────

    [Test]
    public void VisibleTextFollowsTypingWithoutChangingFullTextOrProgress()
    {
        var player = Bubble("Bubble");
        var steps = new TypingSteps();
        Play(player, "안녕하세요", "루나", steps);
        steps.RevealTo(player, 2);

        Assert.That(player.ReadVisibleText(), Is.EqualTo("안녕"));
        Assert.That(player.ReadFullText(), Is.EqualTo("안녕하세요"), "전문 읽기의 의미는 그대로다.");
        Assert.That(player.VisibleCharacterCount, Is.EqualTo(2), "읽기만 해서는 글자가 더 공개되지 않는다.");
        Assert.That(player.IsHolding, Is.False, "읽기만 해서는 타이핑이 끝나지 않는다.");

        player.Skip();
        Assert.That(player.ReadVisibleText(), Is.EqualTo("안녕하세요"), "스킵하면 전부 보인 것이다.");

        player.ShowInstant("바로 보인다");
        Assert.That(player.ReadVisibleText(), Is.EqualTo("바로 보인다"), "즉시 표시한 줄은 처음부터 전부 보인 것이다.");
        Assert.That(player.VisibleCharacterCount, Is.EqualTo(player.ReadFullText().Length));

        player.Stop();
        Assert.That(player.ReadVisibleText(), Is.Empty);
        Assert.That(player.VisibleCharacterCount, Is.Zero);
    }

    [Test]
    public void GuestBarkIsRecordedOnlyWhileItsSeatIsInView()
    {
        var phases = Root("Phases").AddComponent<PlayPhaseController>();
        SetPhase(phases, EPlayPhase.Tycoon);
        var zoom = Root("Zoom").AddComponent<CameraControllerNew>();
        var camera = Root("Camera").AddComponent<PlayCamera>();
        Set(camera, "cameraZoom", zoom);
        Assert.That(camera.SelectedSeat, Is.EqualTo(ESlotType.Middle));

        var left = Seat(ESlotType.Left, "gentle");
        var middle = Seat(ESlotType.Middle, "rough");
        var personalities = ScriptableObject.CreateInstance<NewPersonalityDataSO>();
        assets.Add(personalities);
        personalities.personalityData = new[]
        {
            new NewPersonalityData { Id = "gentle", Name = new LocalizedText { Ko = "온화형" } },
            new NewPersonalityData { Id = "rough", Name = new LocalizedText { Ko = "거친형" } },
        };
        var history = Root("History").AddComponent<BarDialogueHistory>();
        Set(history, "phases", phases);
        Set(history, "playCamera", camera);
        Set(history, "guestSlots", new[] { left, middle });
        Set(history, "personalityData", personalities);
        Invoke(history, "OnEnable");

        // 왼쪽 좌석은 즉시 표시, 보고 있는 가운데 좌석은 타이핑 중.
        left.BarkPlayer.ShowInstant("왼쪽 손님 잡담");
        var steps = new TypingSteps();
        Play(middle.BarkPlayer, "가운데 손님 주문", "", steps);
        steps.RevealTo(middle.BarkPlayer, 3);

        var rows = history.Snapshot();
        Assert.That(Texts(rows), Is.EqualTo(new[] { "가운데" }), "보고 있는 좌석의 드러난 글자만 담고, 보지 않은 좌석은 수집하지 않는다.");
        Assert.That(rows[0].IsCurrent, Is.True);
        Assert.That(rows[0].Speaker, Is.EqualTo("거친형"), "일반 손님은 성격 이름으로 적는다.");
        Assert.That(middle.BarkPlayer.VisibleCharacterCount, Is.EqualTo(3), "기록을 읽어도 글자가 더 공개되지 않는다.");
        Assert.That(middle.BarkPlayer.IsHolding, Is.False, "기록을 읽어도 타이핑이 끝나지 않는다.");

        // 카메라가 움직이는 동안 드러난 글자는 본 것이 아니다.
        Set(zoom, "offset", new CancellationTokenSource());
        steps.RevealTo(middle.BarkPlayer, 5);
        Invoke(history, "LateUpdate");
        Assert.That(Texts(history.Snapshot()), Is.EqualTo(new[] { "가운데" }));
        Set(zoom, "offset", null);

        // 왼쪽으로 옮기면 아직 떠 있는 왼쪽 말풍선을 보게 되고, 가운데 줄은 본 데까지만 남는다.
        Set(camera, "_currentSlotIndex", 0);
        steps.RevealTo(middle.BarkPlayer, 6);
        Invoke(history, "LateUpdate");
        rows = history.Snapshot();
        Assert.That(Texts(rows), Is.EqualTo(new[] { "가운데", "왼쪽 손님 잡담" }));
        Assert.That(rows[0].IsCurrent, Is.False, "현재 줄은 보고 있는 좌석의 줄 하나다.");
        Assert.That(rows[1].IsCurrent, Is.True);
        Assert.That(rows[1].Speaker, Is.EqualTo("온화형"));

        // 다시 돌아오면 그때 보이는 글자까지 늘어나고, 줄이 끝나도 본 데까지만 남는다.
        Set(camera, "_currentSlotIndex", 1);
        Invoke(history, "LateUpdate");
        Assert.That(Texts(history.Snapshot()), Is.EqualTo(new[] { "가운데 손님", "왼쪽 손님 잡담" }));
        middle.BarkPlayer.Stop();
        rows = history.Snapshot();
        Assert.That(Texts(rows), Is.EqualTo(new[] { "가운데 손님", "왼쪽 손님 잡담" }), "끝까지 보지 않은 뒷부분('주문')은 기록하지 않는다.");
        Assert.That(rows.Any(row => row.IsCurrent), Is.False);
    }

    [Test]
    public void StoryLineShowsTheTypedPrefixWhileCurrentAndFullTextAfterItEnds()
    {
        var phases = Root("Phases").AddComponent<PlayPhaseController>();
        SetPhase(phases, EPlayPhase.Dialogue);
        var runner = Root("Runner").AddComponent<StoryScriptRunner>();
        SetProperty(runner, "IsPresentingLine", true);
        var luna = Bubble("Luna");
        var history = Root("History").AddComponent<BarDialogueHistory>();
        Set(history, "phases", phases);
        Set(history, "storyRunner", runner);
        Set(history, "storyBubbles", new[] { luna.GetComponent<DynamicSpeechBubble>() });
        Invoke(history, "OnEnable");

        var steps = new TypingSteps();
        Play(luna, "오늘도 한 잔", "루나", steps);
        steps.RevealTo(luna, 2);
        var rows = history.Snapshot();
        Assert.That(Texts(rows), Is.EqualTo(new[] { "오늘" }), "출력 중인 줄은 지금까지 보인 글자만 보여 준다.");
        Assert.That(rows[0].IsCurrent, Is.True);
        Assert.That(rows[0].Speaker, Is.EqualTo("루나"));
        Assert.That(luna.VisibleCharacterCount, Is.EqualTo(2), "기록을 읽어도 글자가 더 공개되지 않는다.");
        Assert.That(luna.IsHolding, Is.False, "기록을 읽어도 타이핑이 끝나지 않는다.");

        luna.Skip();
        luna.ShowInstant("바로 나온 줄");
        rows = history.Snapshot();
        Assert.That(Texts(rows), Is.EqualTo(new[] { "오늘도 한 잔", "바로 나온 줄" }), "끝난 단골·튜토리얼 대사는 전문으로 남는다.");
        Assert.That(rows[0].IsCurrent, Is.False);
        Assert.That(rows[1].IsCurrent, Is.True);

        // 대화창 대사는 기존처럼 단골·튜토리얼 대화 단계에서만 기록한다.
        SetPhase(phases, EPlayPhase.Tycoon);
        luna.ShowInstant("영업 중의 대화창");
        Assert.That(Texts(history.Snapshot()), Is.EqualTo(new[] { "오늘도 한 잔", "바로 나온 줄" }));
    }

    // ── 준비 ───────────────────────────────────────────────────────────

    GameObject Root(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    DialogueTextPlayer Bubble(string name)
    {
        var canvas = Root(name + "Canvas").AddComponent<Canvas>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
        Assert.That(prefab, Is.Not.Null, BubblePrefabPath);
        var instance = Object.Instantiate(prefab, canvas.transform, false);
        instance.name = name;
        Assert.That(instance.GetComponent<DynamicSpeechBubble>(), Is.Not.Null);
        if (!instance.TryGetComponent(out DialogueTextPlayer player)) player = instance.AddComponent<DialogueTextPlayer>();
        players.Add(player);
        return player;
    }

    GuestSlot Seat(ESlotType type, string personality)
    {
        var slot = Root("Seat" + type).AddComponent<GuestSlot>();
        var player = Bubble("Bubble" + type);
        Set(slot, "slotType", type);
        Set(slot, "speechBubble", player.GetComponent<DynamicSpeechBubble>());
        SetProperty(slot, "CurrentGuest", new Guest { id = "guest_" + type, personality = personality });
        Assert.That(slot.BarkPlayer, Is.SameAs(player));
        return slot;
    }

    static void Play(DialogueTextPlayer player, string text, string speaker, TypingSteps steps) =>
        player.PlayAsync(new TypingData(text, speaker, Vector3.zero, Color.white, false), null, 0.05f,
            CancellationToken.None, null, steps.Wait).Forget();

    static void SetPhase(PlayPhaseController phases, EPlayPhase phase)
    {
        SetProperty(phases, "CurrentPhase", phase);
        Set(phases, "phaseInputEnabled", true);
    }

    static string[] Texts(DialogueHistoryStore.Entry[] rows) => rows.Select(row => row.Text).ToArray();

    static void Set(object owner, string field, object value)
    {
        var info = owner.GetType().GetField(field, Members);
        Assert.That(info, Is.Not.Null, owner.GetType().Name + "." + field);
        info.SetValue(owner, value);
    }

    static void SetProperty(object owner, string property, object value)
    {
        var setter = owner.GetType().GetProperty(property, Members)?.GetSetMethod(true);
        Assert.That(setter, Is.Not.Null, owner.GetType().Name + "." + property);
        setter.Invoke(owner, new[] { value });
    }

    static void Invoke(object owner, string method)
    {
        var info = owner.GetType().GetMethod(method, Members);
        Assert.That(info, Is.Not.Null, owner.GetType().Name + "." + method);
        info.Invoke(owner, null);
    }

    /// <summary>타이핑 대기를 하나씩 풀어 원하는 글자 수까지 결정적으로 진행한다.</summary>
    sealed class TypingSteps
    {
        UniTaskCompletionSource pending;
        CancellationTokenRegistration registration;

        public UniTask Wait(float seconds, CancellationToken token)
        {
            var source = new UniTaskCompletionSource();
            pending = source;
            registration = token.Register(() => source.TrySetCanceled(token));
            return source.Task;
        }

        public void RevealTo(DialogueTextPlayer player, int count)
        {
            for (int guard = 0; player.VisibleCharacterCount < count && pending != null && guard < 1000; guard++)
            {
                var source = pending;
                pending = null;
                registration.Dispose();
                source.TrySetResult();
            }
            Assert.That(player.VisibleCharacterCount, Is.EqualTo(count));
        }
    }
}
