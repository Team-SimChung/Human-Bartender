using System;
using System.Linq;

/// <summary>웹 CSV 표현을 로드 경계에서 Unity 명령으로 변환한다. 실행과 플레이어 상태 변경은 하지 않는다.</summary>
public static class StoryCsvCompiler
{
    public static readonly string[] RequiredScenes =
    {
        "day1-corridor-thug", "home-wardrobe-pressed", "shiba-allowance", "shared-shop-quizIntro", "shared-shop-lesson",
        "shared-beggar-paid", "shared-beggar-refused", "shared-beggar-questioned", "neighborhood-feed", "neighborhood-repairIntro",
        "neighborhood-repairPaid", "neighborhood-guardPass", "neighborhood-bossWait", "neighborhood-bossPayFirst",
        "neighborhood-bossPayRepeat", "neighborhood-bossRepairFirst", "neighborhood-bossRepairRepeat"
    };

    public static NewDayScriptBase Compile(NewDayScriptBase source)
    {
        if (source == null) return null;
        return new NewDayScriptBase
        {
            Place = source.Place, Day = source.Day, Choices = source.Choices,
            Scenes = (source.Scenes ?? Array.Empty<NewScriptSceneData>()).Select(scene =>
            {
                scene.Steps = (scene.Steps ?? Array.Empty<NewDialogueStepData>()).Select(Compile).ToArray();
                return scene;
            }).ToArray()
        };
    }

    public static NewDialogueStepData Compile(NewDialogueStepData step)
    {
        string Field(string key) => step.WebFields != null && step.WebFields.TryGetValue(key, out var value) ? value as string : null;
        step.Payment ??= Field("payment");
        step.DisplayName ??= Field("who");
        step.CutsceneAfter ??= Field("cinemaAfter");
        step.WebFields = null;
        return step;
    }

    public static NewStreetData Compile(NewStreetData source)
    {
        source.Scenes = (source.Scenes ?? Array.Empty<NewSceneData>()).Select(scene =>
        {
            scene.Steps = Compile(scene.Steps);
            return scene;
        }).ToArray();
        return source;
    }

    static Step[] Compile(Step[] source) => (source ?? Array.Empty<Step>()).Select(step =>
    {
        string kind = Field(step, "type");
        step.CutsceneAfter = Field(step, "cinemaAfter") ?? step.CutsceneAfter;
        step.MemoryKey = Field(step, "memoryKey") ?? step.MemoryKey;
        if (kind == "choice" && step.Options?.Length > 0) { }
        else if (!string.IsNullOrEmpty(kind) && kind != "say" && kind != "timeline" && kind != "effect")
        {
            step.Command = Parse(kind, Field(step, "stage"), Field(step, "action"));
            step.Type = "event";
        }
        if (step.Options != null) step.Options = step.Options.Select(option =>
        {
            if (option.ResultSteps != null) option.ResultSteps = Compile(option.ResultSteps);
            return option;
        }).ToArray();
        // 실행기는 원시 웹 메타데이터를 읽을 수 없다. 원본 CSV와 catalog의 조립 결과는 유지한다.
        step.WebFields = null;
        return step;
    }).ToArray();

    public static OutsideStoryCommand Parse(string kind, string stage, string action) => (kind, stage, action) switch
    {
        ("choice", _, _) => OutsideStoryCommand.SharedChoice,
        ("shop-intro-done", _, _) => OutsideStoryCommand.ShopIntroComplete,
        ("shop-open", _, _) => OutsideStoryCommand.ShopOpen,
        ("shop-choice", "greeting", _) => OutsideStoryCommand.ShopGreeting,
        ("shop-choice", "allowance", _) => OutsideStoryCommand.ShopAllowance,
        ("home-choice", "wardrobe", _) => OutsideStoryCommand.WardrobeChoice,
        ("thug-choice", _, _) => OutsideStoryCommand.ThugChoice,
        ("beggar-choice", _, _) => OutsideStoryCommand.BeggarChoice,
        ("neighborhood-choice", "cat", _) => OutsideStoryCommand.CatChoice,
        ("neighborhood-choice", "home-cat", _) => OutsideStoryCommand.HomeCatChoice,
        ("neighborhood-choice", "repair-intro", _) => OutsideStoryCommand.RepairIntroChoice,
        ("neighborhood-choice", "repair", _) => OutsideStoryCommand.RepairChoice,
        ("neighborhood-choice", "guard", _) => OutsideStoryCommand.GuardChoice,
        ("neighborhood-choice", "boss-first", _) => OutsideStoryCommand.BossFirstChoice,
        ("neighborhood-choice", "boss-repeat", _) => OutsideStoryCommand.BossRepeatChoice,
        ("neighborhood-action", _, "boss-met") => OutsideStoryCommand.MeetBoss,
        ("neighborhood-action", _, "wild-dog-grant") => OutsideStoryCommand.GrantWildDog,
        ("neighborhood-action", _, "repair") => OutsideStoryCommand.RepairElevator,
        ("neighborhood-action", _, "adopt") => OutsideStoryCommand.AdoptCat,
        ("neighborhood-action", _, "reveal") => OutsideStoryCommand.RevealCat,
        ("neighborhood-action", _, "pet") => OutsideStoryCommand.PetCat,
        ("neighborhood-action", _, "feed") => OutsideStoryCommand.FeedCat,
        ("neighborhood-action", _, "neighbor-in") => OutsideStoryCommand.NeighborEnter,
        ("neighborhood-action", _, "neighbor-out") => OutsideStoryCommand.NeighborExit,
        ("neighborhood-action", _, "chris-in") => OutsideStoryCommand.ChrisEnter,
        ("neighborhood-action", _, "meow") => OutsideStoryCommand.CatMeow,
        ("samho-action", _, "depart") => OutsideStoryCommand.SamhoDepart,
        ("notion-action", _, "approach") => OutsideStoryCommand.NotionApproach,
        ("notion-action", _, "dealer-exit") => OutsideStoryCommand.NotionDealerExit,
        ("notion-action", _, "samho-exit") => OutsideStoryCommand.NotionSamhoExit,
        ("thug-action", _, "punch") => OutsideStoryCommand.ThugPunch,
        ("thug-action", _, "depart") => OutsideStoryCommand.ThugDepart,
        ("story_cue", _, _) => OutsideStoryCommand.DoorCue,
        _ => throw new NotSupportedException("지원하지 않는 사건 CSV 명령: " + kind + "/" + stage + "/" + action)
    };

    static string Field(Step step, string key) => step.WebFields != null && step.WebFields.TryGetValue(key, out var value)
        ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;
}
