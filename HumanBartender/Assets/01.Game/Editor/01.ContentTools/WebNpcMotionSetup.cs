using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>웹의 배우 동선·시선·카메라만 공통 동작 목록으로 옮긴다. 대사와 진행 플래그는 기존 CSV가 소유한다.</summary>
public static class WebNpcMotionSetup
{
    public static void Apply(Scene home, Scene outside)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("NPC 동작 설정은 편집 모드에서 실행하세요.");
        var homeTargets = Targets(home);
        var outsideTargets = Targets(outside);
        var homeSpawn = All<Transform>(outside).Single(value => value.name == "HomeSpawn");
        ConfigureRepair(outsideTargets["neighborhood-repairGreeting"]);
        ConfigureFirstThug(outsideTargets["day1-corridor-thug"], homeSpawn);
        ConfigureSecondThug(outsideTargets["day2-corridor-thug"]);
        Facing(Main(outsideTargets["day2-thug-information"]), NpcIdleFacing.Player);
        ConfigureCommute(outsideTargets["commute2"], false);
        ConfigureCommute(outsideTargets["commute3"], true);
        ConfigureTrade(outsideTargets["outside_day1_samho_smuggler"]);
        ConfigureContract(outsideTargets["day3-samho-contract"]);
        ConfigureOutdoorCat(outsideTargets["neighborhood-reveal"]);
        ConfigureHomeCat(homeTargets["neighborhood-homeCat"], home);
        ConfigureCamp(outsideTargets);
        foreach (var id in new[] { "shiba-shop", "shiba-shop-revisit" })
        {
            using var data = new SerializedObject(outsideTargets[id].GetComponent<NpcInteraction>());
            data.FindProperty("minimumDay").intValue = 2;
            data.ApplyModifiedPropertiesWithoutUndo();
            Facing(Main(outsideTargets[id]), NpcIdleFacing.Authored);
        }
    }

    private static void ConfigureRepair(WorldInteractionTarget target)
    {
        var person = Main(target);
        Facing(person, NpcIdleFacing.Player);
        var list = ReadSequences(target);
        var repair = list.Single(value => value.trigger == NpcSequenceTrigger.WorldAction &&
            value.worldAction == OutsideStoryWorldAction.RepairElevator);
        // 지불 조건 및 기존 명령 연결은 그대로 두고 세 구간의 시간만 맞춘다.
        repair.actions.Single(value => value.kind == NpcActionKind.ScreenFadeOut).duration = .35f;
        repair.actions.Single(value => value.kind == NpcActionKind.Wait).duration = 1.35f;
        repair.actions.Single(value => value.kind == NpcActionKind.ScreenFadeIn).duration = .45f;
        var complete = list.Single(value => value.trigger == NpcSequenceTrigger.DialogueCompleted &&
            value.actions.Any(IsDeparture));
        for (int i = 0; i < complete.actions.Length; i++)
        {
            var old = complete.actions[i];
            if (!IsDeparture(old)) continue;
            // 사용자가 작성한 상대 X/Y와 배우 참조는 보존한다. 퇴장 전체 1.6초 중 마지막 .5초만 페이드한다.
            complete.actions[i] = new NpcActionStep
            {
                kind = NpcActionKind.DepartWhileMoving, performer = old.performer,
                offset = old.offset, walkDuration = 1.1f, duration = .5f,
                fadeEase = NpcMotionEase.Linear
            };
        }
        SaveSequences(target, list);
    }

    private static bool IsDeparture(NpcActionStep step) => step.kind is
        NpcActionKind.Depart or NpcActionKind.DepartWhileMoving or NpcActionKind.MoveToAndFade;

    private static void ConfigureFirstThug(WorldInteractionTarget target, Transform homeSpawn)
    {
        var thug = Main(target);
        Facing(thug, NpcIdleFacing.Player);
        var list = ReadSequences(target);
        Before(list, FacePlayer(thug.transform), new NpcActionStep
        {
            kind = NpcActionKind.CameraFocus, positionOrigin = NpcPositionOrigin.Target,
            target = homeSpawn, offset = new Vector2(.4f, .65f), duration = .65f
        });
        Command(list, "punch", OutsideStoryWorldAction.ThugPunch,
            Face(thug), MoveToPlayer(thug, .33f, .65f, true),
            new NpcActionStep
            {
                kind = NpcActionKind.SetPosition, performer = thug, positionOrigin = NpcPositionOrigin.Target,
                offset = new Vector2(.23f, 0), horizontalOnly = true
            }, MoveToPlayer(thug, .63f, .8f, true), Wait(.2f));
        Command(list, "depart", OutsideStoryWorldAction.ThugDepart, Depart(thug, WebNpcSequenceDefaults.DepartureDistance));
        Finish(list, RestoreCamera());
        SaveSequences(target, list);
    }

    private static void ConfigureSecondThug(WorldInteractionTarget target)
    {
        var thug = Main(target);
        Facing(thug, NpcIdleFacing.Player);
        var list = ReadSequences(target);
        var move = MovePlayer(thug.transform, -.55f, .65f, .65f, true);
        move.clampPlayerLeft = true;
        Before(list, FacePlayer(thug.transform), move, FacePlayer(thug.transform));
        Finish(list, RestoreCamera());
        SaveSequences(target, list);
    }

    private static void ConfigureCommute(WorldInteractionTarget target, bool departOnCompletion)
    {
        var samho = Main(target);
        Facing(samho, NpcIdleFacing.Player);
        var list = ReadSequences(target);
        var move = MovePlayer(samho.transform, .78f, .65f, .7f, true);
        move.offsetMode = NpcOffsetMode.SameSide;
        Before(list, FacePlayer(samho.transform), move, FacePlayer(samho.transform));
        var departure = Depart(samho, WebNpcSequenceDefaults.DepartureDistance);
        departure.offsetMode = NpcOffsetMode.AwayFromTarget;
        if (departOnCompletion) Finish(list, departure, RestoreCamera());
        else
        {
            Command(list, "samho-depart", OutsideStoryWorldAction.SamhoDepart, departure);
            Finish(list, RestoreCamera());
        }
        SaveSequences(target, list);
    }

    private static void ConfigureTrade(WorldInteractionTarget target)
    {
        var samho = Role(target, "samho");
        var dealer = Role(target, "dealer");
        Facing(samho, NpcIdleFacing.Target, dealer.transform);
        Facing(dealer, NpcIdleFacing.Target, samho.transform);
        Presence(target, 1, 1, NpcPresencePhase.CommuteOut, false);
        float center = (samho.transform.position.x + dealer.transform.position.x) * .5f;
        var list = ReadSequences(target);
        // 이 사건은 대화가 끝나도 두 배우가 그날 퇴근길에 남아 서로를 바라본다.
        Before(list, new NpcActionStep
        {
            kind = NpcActionKind.FacePlayer, target = samho.transform,
            offset = new Vector2(center - samho.transform.position.x, 0)
        }, new NpcActionStep
        {
            kind = NpcActionKind.CameraFocus, positionOrigin = NpcPositionOrigin.World,
            offset = new Vector2(center, -.05f), cameraMidpoint = true, duration = .65f
        });
        Finish(list, RestoreCamera());
        SaveSequences(target, list);
    }

    private static void ConfigureContract(WorldInteractionTarget target)
    {
        var samho = Role(target, "samho");
        var dealer = Role(target, "dealer");
        Facing(samho, NpcIdleFacing.Target, dealer.transform);
        Facing(dealer, NpcIdleFacing.Target, samho.transform);
        var list = ReadSequences(target);
        var move = MovePlayer(samho.transform, 1, .65f, -.05f - samho.transform.position.y, false);
        move.cameraOffset.x = .2f;
        Before(list, FacePlayer(samho.transform), move, FacePlayer(samho.transform));
        foreach (var sequence in WebNpcSequenceDefaults.CreateContractSequences(samho, dealer))
            Upsert(list, sequence);
        Finish(list, RestoreCamera());
        SaveSequences(target, list);
    }

    private static void ConfigureOutdoorCat(WorldInteractionTarget target)
    {
        var cat = Role(target, "boobi");
        var neighbor = Role(target, "neighbor");
        Facing(cat, NpcIdleFacing.Authored);
        Facing(neighbor, NpcIdleFacing.Player);
        var list = ReadSequences(target);
        Command(list, "reveal", OutsideStoryWorldAction.RevealCat,
            new NpcActionStep
            {
                kind = NpcActionKind.SetPosition, performer = cat, positionOrigin = NpcPositionOrigin.Target,
                target = target.transform
            }, Show(cat), Animation(cat, "RevealEyes", .55f),
            new NpcActionStep
            {
                kind = NpcActionKind.ArcMove, performer = cat, positionOrigin = NpcPositionOrigin.Initial,
                duration = .9f, arcHeight = .55f
            }, Wait(.35f));
        Command(list, "feed", OutsideStoryWorldAction.FeedCat, Show(cat), Animation(cat, "Feed", 1.5f));
        Command(list, "pet", OutsideStoryWorldAction.PetCat, Show(cat), Animation(cat, "Pet", 1.6f));
        // 입양의 실제 암전·홈 씬 이동은 기존 진행 실행기의 전환 경계가 담당한다.
        Command(list, "adopt", OutsideStoryWorldAction.AdoptCat, Wait(0));
        Command(list, "neighbor-enter", OutsideStoryWorldAction.NeighborEnter, Show(neighbor),
            new NpcActionStep
            {
                kind = NpcActionKind.MoveTo, performer = neighbor, positionOrigin = NpcPositionOrigin.Target,
                target = cat.transform, offset = new Vector2(.8f, 0), horizontalOnly = true,
                unitsPerSecond = .24f, movementEase = NpcMotionEase.Linear
            });
        Command(list, "neighbor-exit", OutsideStoryWorldAction.NeighborExit,
            new NpcActionStep
            {
                kind = NpcActionKind.MoveToAndFade, performer = neighbor, positionOrigin = NpcPositionOrigin.Initial,
                horizontalOnly = true, unitsPerSecond = .24f, duration = .4f, fadeEase = NpcMotionEase.Linear
            });
        SaveSequences(target, list);
    }

    private static void ConfigureHomeCat(WorldInteractionTarget target, Scene home)
    {
        var cat = Role(target, "boobi");
        var chris = Role(target, "chris");
        Facing(cat, NpcIdleFacing.Authored);
        Facing(chris, NpcIdleFacing.Authored, right: true);
        float center = HomeCameraCenter(home, 2.2f, 4.8f);
        var list = ReadSequences(target);
        Command(list, "pet", OutsideStoryWorldAction.PetCat, Show(cat), Animation(cat, "Pet", 1.6f));
        Command(list, "meow", OutsideStoryWorldAction.CatMeow, Show(cat), Animation(cat, "Meow", 1));
        Command(list, "chris-enter", OutsideStoryWorldAction.ChrisEnter,
            new NpcActionStep { kind = NpcActionKind.ScreenFadeOut, duration = .45f },
            new NpcActionStep { kind = NpcActionKind.SetPosition, performer = chris, positionOrigin = NpcPositionOrigin.Initial },
            Show(chris), new NpcActionStep
            {
                kind = NpcActionKind.SetPlayerPosition, positionOrigin = NpcPositionOrigin.World,
                offset = new Vector2(2.2f, -.7f)
            }, FacePlayer(chris.transform), new NpcActionStep
            {
                kind = NpcActionKind.CameraSet, positionOrigin = NpcPositionOrigin.World,
                offset = new Vector2(center - .32f, .023f)
            }, Wait(.25f), new NpcActionStep
            {
                kind = NpcActionKind.CameraPan, positionOrigin = NpcPositionOrigin.World,
                offset = new Vector2(center, .023f), duration = 2, movementEase = NpcMotionEase.SmoothStep,
                fadeInTogether = .6f
            });
        Finish(list, RestoreCamera());
        SaveSequences(target, list);
    }

    private static void ConfigureCamp(Dictionary<string, WorldInteractionTarget> targets)
    {
        var camp = targets["neighborhood-bossIntro"];
        var boss = Role(camp, "boss");
        var gangmate = Role(camp, "gangmate");
        var guardTarget = targets["neighborhood-guardBlocked"];
        Presence(camp, 0, -1, NpcPresencePhase.Any, true);
        Presence(guardTarget, 0, -1, NpcPresencePhase.Any, true);
        Facing(boss, NpcIdleFacing.Target, gangmate.transform, true);
        Facing(gangmate, NpcIdleFacing.Target, boss.transform, true);
        Facing(Main(guardTarget), NpcIdleFacing.Target, boss.transform, true);
    }

    private static void Presence(WorldInteractionTarget target, int minimumDay, int maximumDay,
        NpcPresencePhase phase, bool minimumDayStartsOnReturn)
    {
        var interaction = target.GetComponent<NpcInteraction>();
        using var data = new SerializedObject(interaction);
        data.FindProperty("useStoryPresence").boolValue = false;
        data.FindProperty("minimumDay").intValue = minimumDay;
        data.FindProperty("maximumDay").intValue = maximumDay;
        data.FindProperty("presencePhase").enumValueIndex = (int)phase;
        data.FindProperty("minimumDayStartsOnReturn").boolValue = minimumDayStartsOnReturn;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(interaction);
    }

    private static void Facing(NpcActor actor, NpcIdleFacing facing, Transform target = null,
        bool duringDialogue = false, bool? right = null)
    {
        using var data = new SerializedObject(actor);
        data.FindProperty("idleFacing").enumValueIndex = (int)facing;
        data.FindProperty("idleFacingTarget").objectReferenceValue = target;
        data.FindProperty("facePlayerDuringDialogue").boolValue = duringDialogue;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actor);
        if (!right.HasValue) return;
        using var movement = new SerializedObject(actor.GetComponent<NpcMovement2D>());
        movement.FindProperty("facingRight").boolValue = right.Value;
        movement.ApplyModifiedPropertiesWithoutUndo();
        var renderer = (SpriteRenderer)data.FindProperty("spriteRenderer").objectReferenceValue;
        renderer.flipX = data.FindProperty("artworkFacesRight").boolValue ? !right.Value : right.Value;
        EditorUtility.SetDirty(renderer);
    }

    private static NpcActionStep MoveToPlayer(NpcActor actor, float x, float duration, bool keepFacing = false) => new()
    {
        kind = NpcActionKind.MoveTo, performer = actor, positionOrigin = NpcPositionOrigin.Target,
        offset = new Vector2(x, 0), horizontalOnly = true, duration = duration,
        movementEase = NpcMotionEase.Linear, keepFacing = keepFacing
    };
    private static NpcActionStep MovePlayer(Transform target, float x, float duration, float cameraY, bool midpoint) => new()
    {
        kind = NpcActionKind.MovePlayer, target = target, positionOrigin = NpcPositionOrigin.Target,
        offset = new Vector2(x, 0), horizontalOnly = true, duration = duration, movementEase = NpcMotionEase.SmoothStep,
        cameraWithMovement = true, cameraMidpoint = midpoint, cameraOffset = new Vector2(0, cameraY), keepFacing = true
    };
    private static NpcActionStep Depart(NpcActor actor, float x) => WebNpcSequenceDefaults.CreateDeparture(actor, x);
    private static NpcActionStep Animation(NpcActor actor, string name, float duration) => new()
    { kind = NpcActionKind.Animation, performer = actor, stateName = name, returnState = "Idle", duration = duration };
    private static NpcActionStep Show(NpcActor actor) => new() { kind = NpcActionKind.Show, performer = actor };
    private static NpcActionStep Face(NpcActor actor) => new() { kind = NpcActionKind.Face, performer = actor };
    private static NpcActionStep FacePlayer(Transform target) => new() { kind = NpcActionKind.FacePlayer, target = target };
    private static NpcActionStep Wait(float duration) => new() { kind = NpcActionKind.Wait, duration = duration };
    private static NpcActionStep RestoreCamera() => new() { kind = NpcActionKind.CameraRestore, duration = .4f };

    private static void Before(List<NpcSequence> list, params NpcActionStep[] actions) =>
        Upsert(list, new NpcSequence { id = "web-dialogue-start", trigger = NpcSequenceTrigger.BeforeDialogue, actions = actions });
    private static void Finish(List<NpcSequence> list, params NpcActionStep[] actions) =>
        Upsert(list, new NpcSequence { id = "web-dialogue-finish", trigger = NpcSequenceTrigger.DialogueCompleted, actions = actions });
    private static void Command(List<NpcSequence> list, string id, OutsideStoryWorldAction command, params NpcActionStep[] actions) =>
        Upsert(list, new NpcSequence { id = id, trigger = NpcSequenceTrigger.WorldAction, worldAction = command, actions = actions });

    private static void Upsert(List<NpcSequence> list, NpcSequence replacement) => NpcSequenceEditorData.Upsert(list, replacement);

    private static List<NpcSequence> ReadSequences(WorldInteractionTarget target)
    {
        using var data = new SerializedObject(target.GetComponent<NpcInteraction>());
        return NpcSequenceEditorData.ReadList(data.FindProperty("sequences"));
    }

    private static void SaveSequences(WorldInteractionTarget target, List<NpcSequence> sequences)
    {
        var interaction = target.GetComponent<NpcInteraction>();
        using var data = new SerializedObject(interaction);
        NpcSequenceEditorData.WriteList(data.FindProperty("sequences"), sequences);
        data.ApplyModifiedProperties();
        if (NpcSequenceEditorData.RequiresStaging(sequences) && target.GetComponent<NpcSceneStaging>() == null)
            Undo.AddComponent<NpcSceneStaging>(target.gameObject);
        EditorUtility.SetDirty(interaction);
        PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
    }

    private static Dictionary<string, WorldInteractionTarget> Targets(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("먼저 Home과 OutSide 씬을 열어 주세요.");
        return All<WorldInteractionTarget>(scene).Where(value => value.GetComponent<NpcInteraction>() != null &&
            !string.IsNullOrEmpty(value.StorySceneId)).ToDictionary(value => value.StorySceneId);
    }
    private static IEnumerable<T> All<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<T>(true));
    private static NpcActor Main(WorldInteractionTarget target)
    {
        using var data = new SerializedObject(target.GetComponent<NpcInteraction>());
        return (NpcActor)data.FindProperty("actor").objectReferenceValue;
    }
    private static NpcActor Role(WorldInteractionTarget target, string role) =>
        target.GetComponentsInChildren<NpcActor>(true).Single(value => value.name.EndsWith("· " + role, StringComparison.Ordinal));

    private static float HomeCameraCenter(Scene scene, float playerX, float width)
    {
        var controller = All<CameraControllerNew>(scene).Single();
        using var data = new SerializedObject(controller);
        var confiner = (CinemachineConfiner2D)data.FindProperty("confiner").objectReferenceValue;
        if (confiner == null || confiner.BoundingShape2D == null)
            throw new InvalidOperationException("Home 카메라의 경계 연결을 확인하세요.");
        var bounds = confiner.BoundingShape2D.bounds;
        return bounds.size.x <= width ? bounds.center.x : Mathf.Clamp(playerX, bounds.min.x + width * .5f, bounds.max.x - width * .5f);
    }
}
