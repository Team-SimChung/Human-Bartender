using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>설정 도구 정리 후에도 작성 값·배우 참조·스프라이트 식별자가 사라지지 않는지 검사한다.</summary>
public sealed class NpcEditorSetupTests
{
    private SceneSetup[] sceneSetup;
    private string temporaryAssetFolder;

    [SetUp]
    public void SetUp()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(temporaryAssetFolder) && AssetDatabase.IsValidFolder(temporaryAssetFolder))
            AssetDatabase.DeleteAsset(temporaryAssetFolder);
        temporaryAssetFolder = null;
        if (sceneSetup != null && sceneSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
    }

    [Test]
    public void SequenceRoundTripPreservesAllAuthoredValuesAndClearsFieldsFromReplacedActions()
    {
        var root = new GameObject("NPC 직렬화 검사");
        var interaction = root.AddComponent<NpcInteraction>();
        var actor = ChildActor(root.transform, "배우");
        var destination = new GameObject("기준 위치").transform;
        var step = new NpcActionStep
        {
            kind = NpcActionKind.MoveToAndFade, performer = actor, target = destination,
            positionOrigin = NpcPositionOrigin.Initial, movementEase = NpcMotionEase.SmoothStep,
            horizontalOnly = true, distance = 2.4f, offset = new Vector2(-4.75f, 2.25f),
            offsetMode = NpcOffsetMode.AwayFromTarget, unitsPerSecond = 1.7f, arcHeight = 2.1f,
            keepFacing = true, clampPlayerLeft = true, cameraWithMovement = true, cameraMidpoint = true,
            cameraOffset = new Vector2(.25f, -.45f), cameraWidth = 5.9f, fadeInTogether = .61f,
            stateName = "사용자 상태", returnState = "사용자 복귀", duration = .43f,
            walkDuration = 1.27f, fadeEase = NpcMotionEase.SmoothStep, text = "사용자 알림"
        };
        var authored = new NpcSequence
        {
            id = "authored-sequence", trigger = NpcSequenceTrigger.Named,
            worldAction = OutsideStoryWorldAction.NotionDealerExit, requiredCondition = "!flag.custom_done",
            actions = new[] { step, new NpcActionStep { kind = NpcActionKind.Wait, duration = .2f } }
        };
        Write(interaction, new[] { authored });
        var restored = Read(interaction).Single();
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(authored)));
        Assert.That(restored.actions[0].performer, Is.SameAs(actor));
        Assert.That(restored.actions[0].target, Is.SameAs(destination));

        authored.actions = new[] { new NpcActionStep { kind = NpcActionKind.Wait, duration = .8f } };
        Write(interaction, new[] { authored });
        var replaced = Read(interaction).Single().actions.Single();
        Assert.That(JsonUtility.ToJson(replaced), Is.EqualTo(JsonUtility.ToJson(authored.actions[0])));
        Assert.That(replaced.performer, Is.Null);
        Assert.That(replaced.target, Is.Null);
        Assert.That(replaced.cameraWithMovement, Is.False);
        Assert.That(replaced.offset, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void LegacyContractSetupKeepsAuthoredIdsConditionsAndCurrentCameraMotion()
    {
        var root = new GameObject("계약 설정 검사");
        var target = root.AddComponent<WorldInteractionTarget>();
        using (var data = new SerializedObject(target))
        {
            data.FindProperty("storySceneId").stringValue = "day3-samho-contract";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var interaction = root.AddComponent<NpcInteraction>();
        var samho = ChildActor(root.transform, "삼호");
        var dealer = ChildActor(root.transform, "밀수업자");
        root.AddComponent<WorldDialogueSpeakers>().Configure(new[]
        {
            ("contract-samho", samho.transform), ("contract-smuggler", dealer.transform)
        });
        using (var data = new SerializedObject(interaction))
        {
            data.FindProperty("actor").objectReferenceValue = samho;
            var additional = data.FindProperty("additionalActors");
            additional.arraySize = 1;
            additional.GetArrayElementAtIndex(0).FindPropertyRelative("actor").objectReferenceValue = dealer;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var extra = new NpcSequence
        {
            id = "custom-extra", trigger = NpcSequenceTrigger.Named,
            actions = new[] { new NpcActionStep { kind = NpcActionKind.Wait, duration = 3.25f } }
        };
        Write(interaction, new[]
        {
            new NpcSequence
            {
                id = "custom-approach", trigger = NpcSequenceTrigger.WorldAction,
                worldAction = OutsideStoryWorldAction.NotionApproach, requiredCondition = "flag.custom_ready",
                actions = new[] { new NpcActionStep { kind = NpcActionKind.Wait } }
            }, extra
        });

        DayThreeContractSetup.ConfigureTarget(target);

        var sequences = Read(interaction);
        var approach = sequences.Single(sequence => sequence.worldAction == OutsideStoryWorldAction.NotionApproach &&
            sequence.trigger == NpcSequenceTrigger.WorldAction);
        Assert.That(approach.id, Is.EqualTo("custom-approach"));
        Assert.That(approach.requiredCondition, Is.EqualTo("flag.custom_ready"));
        Assert.That(approach.actions[0].cameraWithMovement, Is.True);
        Assert.That(approach.actions[0].cameraMidpoint, Is.True);
        Assert.That(approach.actions[0].performer, Is.SameAs(samho));
        Assert.That(root.GetComponent<NpcSceneStaging>(), Is.Not.Null);
        Assert.That(JsonUtility.ToJson(sequences.Single(sequence => sequence.id == extra.id)), Is.EqualTo(JsonUtility.ToJson(extra)));
    }

    [Test]
    public void ModernSpriteImportPreservesExistingSpriteLocalIdsAcrossRepeatedImports()
    {
        const string source = "Assets/01.Game/03.Content/07.Animations/01.Characters/NpcInteraction/WebCat/WebCatFrames.png";
        string folderName = "__NpcEditorRefactorTests_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folderName);
        temporaryAssetFolder = "Assets/" + folderName;
        string copy = temporaryAssetFolder + "/Frames.png";
        Assert.That(AssetDatabase.CopyAsset(source, copy), Is.True);
        AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
        var originalIds = SpriteIds(copy);
        Assert.That(originalIds.Count, Is.GreaterThan(1));
        var importer = (TextureImporter)AssetImporter.GetAtPath(copy);
        float pixelsPerUnit = importer.spritePixelsPerUnit;
        var definitions = SpriteDefinitions(importer);

        for (int pass = 0; pass < 2; pass++)
        {
            NpcSpriteSheetImporter.Apply(importer, definitions);
            importer.SaveAndReimport();
            Assert.That(SpriteIds(copy), Is.EquivalentTo(originalIds));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(pixelsPerUnit));
            var imported = SpriteDefinitions(importer);
            Assert.That(imported.Length, Is.EqualTo(definitions.Length));
            for (int i = 0; i < imported.Length; i++)
            {
                Assert.That(imported[i].name, Is.EqualTo(definitions[i].name));
                Assert.That(imported[i].rect, Is.EqualTo(definitions[i].rect));
                Assert.That(imported[i].pivot, Is.EqualTo(definitions[i].pivot));
                Assert.That(imported[i].alignment, Is.EqualTo(definitions[i].alignment));
            }
        }
    }

    private static NpcActor ChildActor(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<NpcActor>();
    }

    private static void Write(NpcInteraction interaction, IReadOnlyList<NpcSequence> sequences)
    {
        using var data = new SerializedObject(interaction);
        NpcSequenceEditorData.WriteList(data.FindProperty("sequences"), sequences);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<NpcSequence> Read(NpcInteraction interaction)
    {
        using var data = new SerializedObject(interaction);
        return NpcSequenceEditorData.ReadList(data.FindProperty("sequences"));
    }

    private static Dictionary<string, string> SpriteIds(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId), Is.True);
            result.Add(sprite.name, guid + ":" + localId);
        }
        return result;
    }

    private static SpriteRect[] SpriteDefinitions(TextureImporter importer)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var slices = provider.GetSpriteRects();
        var result = new SpriteRect[slices.Length];
        for (int i = 0; i < slices.Length; i++)
        {
            var slice = slices[i];
            // 호출자가 정의를 새로 만들어도 기존 Sprite의 식별자를 재사용하는지 확인한다.
            result[i] = new SpriteRect
            {
                name = slice.name, rect = slice.rect, pivot = slice.pivot,
                alignment = slice.alignment, spriteID = GUID.Generate()
            };
        }
        return result;
    }
}
