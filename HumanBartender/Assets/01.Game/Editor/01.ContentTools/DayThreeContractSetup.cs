using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>3일차 계약의 세 CSV 연출을 웹 원본의 배우·방향·시간으로 설정한다.</summary>
public static class DayThreeContractSetup
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string StoryId = "day3-samho-contract";

    [MenuItem("Tools/Story/Apply Web Day 3 Contract Motion")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("3일차 계약 연출은 편집 모드에서 설정하세요.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException("OutSide의 편집 내용을 저장한 다음 계약 연출을 설정하세요.");
        var previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var target = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(value => value.StorySceneId == StoryId);
            ConfigureTarget(target);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("3일차 계약 연출을 저장하지 못했습니다.");
            Debug.Log("DAY_THREE_CONTRACT_SETUP_OK: 밀수업자 왼쪽 퇴장 → 삼호의 루나 접근 → 삼호 오른쪽 퇴장을 연결했습니다.");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static void ConfigureTarget(WorldInteractionTarget target)
    {
        if (target == null || target.StorySceneId != StoryId)
            throw new ArgumentException("3일차 계약 대상을 선택하세요.", nameof(target));
        var interaction = target.GetComponent<NpcInteraction>();
        var speakers = target.GetComponent<WorldDialogueSpeakers>();
        if (interaction == null || speakers == null)
            throw new InvalidOperationException("계약 대상의 공통 NPC Interaction과 화자를 먼저 연결하세요.");
        NpcActor Role(string id)
        {
            if (!speakers.TryResolve(id, out var anchor, out _, out _))
                throw new InvalidOperationException("계약의 기존 화자를 찾을 수 없습니다: " + id);
            var actor = anchor.GetComponentInParent<NpcActor>();
            if (actor == null || !actor.transform.IsChildOf(target.transform))
                throw new InvalidOperationException("계약 화자 아래의 NPC Actor를 확인하세요: " + id);
            return actor;
        }
        var samho = Role("contract-samho");
        var dealer = Role("contract-smuggler");
        if (samho == dealer) throw new InvalidOperationException("삼호와 밀수업자는 서로 다른 NPC Actor여야 합니다.");

        using var data = new SerializedObject(interaction);
        var registered = new HashSet<NpcActor> { (NpcActor)data.FindProperty("actor").objectReferenceValue };
        var additional = data.FindProperty("additionalActors");
        for (int i = 0; i < additional.arraySize; i++)
            registered.Add((NpcActor)additional.GetArrayElementAtIndex(i).FindPropertyRelative("actor").objectReferenceValue);
        if (!registered.Contains(samho) || !registered.Contains(dealer))
            throw new InvalidOperationException("삼호와 밀수업자를 대표 또는 추가 Actor에 등록하세요.");

        var sequences = NpcSequenceEditorData.ReadList(data.FindProperty("sequences"));
        // 작성한 ID와 실행 조건은 보존하며, 최신 계약 연출의 공통 정의를 사용한다.
        foreach (var replacement in CreateSequences(samho, dealer)) NpcSequenceEditorData.Upsert(sequences, replacement);
        NpcSequenceEditorData.WriteList(data.FindProperty("sequences"), sequences);
        data.ApplyModifiedProperties();
        if (NpcSequenceEditorData.RequiresStaging(sequences) && target.GetComponent<NpcSceneStaging>() == null)
            Undo.AddComponent<NpcSceneStaging>(target.gameObject);
        EditorUtility.SetDirty(interaction);
        PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
    }

    public static NpcSequence[] CreateSequences(NpcActor samho, NpcActor dealer)
    {
        return WebNpcSequenceDefaults.CreateContractSequences(samho, dealer);
    }
}
