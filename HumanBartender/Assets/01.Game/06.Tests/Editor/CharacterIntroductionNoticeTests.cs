using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 톰의 '새로운 인물' 알림은 입장 때가 아니라 "내 이름이야, 톰 거너." 대사가 시작될 때 뜬다
/// (엔진 수정 사항 14, 웹 story-notices.js introductions). 다른 인물은 지금처럼 만나는 순간 알린다.
/// </summary>
public sealed class CharacterIntroductionNoticeTests
{
    const string TomNameLine = "campaign_d1_bar_77";
    NewCharacterData[] originalCharacters;
    PlayerDataSO player;
    NoticeSpy popups;
    SceneAudioController audio;
    IConditionUtil conditions;

    [SetUp]
    public void SetUp()
    {
        originalCharacters = NewDataLoadManager.StoryCharacters;
        var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        SetCharacters(csv.Read<NewCharacterData[]>("characters"));
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        popups = new NoticeSpy();
        audio = new SceneAudioController(null, player, popups);
        audio.Start();
        conditions = new ConditionUtil(GameStateManager.Instance, player, player);
    }

    [TearDown]
    public void TearDown()
    {
        audio.Dispose();
        Object.DestroyImmediate(player);
        SetCharacters(originalCharacters);
    }

    [Test]
    public void TomEntryIsSilentAndTheNameLineAnnouncesOnce()
    {
        player.AddFlag("tom_met", true);
        Assert.That(popups.Registered, Is.Empty, "톰은 입장할 때 알리지 않는다(인물 정보 해금용 기록만 남는다).");

        Assert.That(CampaignStoryRules.TryGetIntroduction("tom", TomNameLine, out string effects), Is.True);
        conditions.ApplyRequired(effects);
        Assert.That(popups.Registered, Is.EqualTo(new[] { "tom" }), "이름 대사가 시작되면 톰을 한 번 알린다.");
        Assert.That(player.CheckFlag("tom_name_known"), Is.True);

        conditions.ApplyRequired(effects);
        Assert.That(popups.Registered.Count, Is.EqualTo(1), "같은 대사를 다시 지나가도 두 번 알리지 않는다.");
    }

    [Test]
    public void OtherCharactersStillAnnounceWhenMet()
    {
        player.AddFlag("port_met", true);
        Assert.That(popups.Registered, Is.EqualTo(new[] { "port" }));
        Assert.That(CampaignStoryRules.HasDelayedIntroduction("port"), Is.False);
    }

    [TestCase("tom", "campaign_d1_bar_76")]
    [TestCase("port", TomNameLine)]
    [TestCase(null, TomNameLine)]
    public void OnlyTomsNameLineStartsTheIntroduction(string actor, string dialogueId)
    {
        Assert.That(CampaignStoryRules.TryGetIntroduction(actor, dialogueId, out _), Is.False);
    }

    [Test]
    public void TomIsUnknownOnDayOneUntilHisNameLine()
    {
        Assert.That(CampaignStoryRules.ResolveSpeakerName("tom", 1, conditions, null), Is.EqualTo("???"));
        Assert.That(CampaignStoryRules.ResolveSpeakerName("port", 1, conditions, null), Is.Null, "다른 인물은 원래 이름 그대로.");
        Assert.That(CampaignStoryRules.ResolveSpeakerName("tom", 2, conditions, null), Is.Null, "2일차부터는 이미 아는 인물이다.");

        CampaignStoryRules.TryGetIntroduction("tom", TomNameLine, out string effects);
        conditions.ApplyRequired(effects);
        Assert.That(CampaignStoryRules.ResolveSpeakerName("tom", 1, conditions, null), Is.Null, "이름 대사부터는 원래 이름이 보인다.");
    }

    [Test]
    public void StoryCsvStillHasTomsNameLine()
    {
        string steps = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "csv", "story", "steps.csv"));
        bool found = steps.Split('\n').Any(line => line.Contains(",say,tom,") && line.Contains("," + TomNameLine + ","));
        Assert.That(found, Is.True, "알림 시점을 정하는 톰의 이름 대사(" + TomNameLine + ")가 steps.csv에 있어야 합니다.");
    }

    static void SetCharacters(NewCharacterData[] value) => typeof(NewDataLoadManager).GetProperty("StoryCharacters")
        .GetSetMethod(true).Invoke(null, new object[] { value });

    sealed class NoticeSpy : IPopupService
    {
        public readonly List<string> Registered = new();
        public UniTask<bool> Popup(PopupType type, string data)
        {
            if (type == PopupType.CharacterRegistered) Registered.Add(data);
            return UniTask.FromResult(true);
        }
    }
}
