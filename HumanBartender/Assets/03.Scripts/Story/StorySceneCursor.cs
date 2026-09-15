using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그날 자동으로 실행될 씬을 순서대로 골라 준다(2부 운영 명세 §4).
///
/// 고르는 단위가 씬 하나가 아니라 <b>같은 seq를 쓰는 묶음</b>이다. 한 자리에 조건이 다른 씬을 여럿 두고
/// 그중 하나만 실행하는 것이 분기의 방식이라, 묶음을 통째로 보고 참인 것을 찾는다.
///
/// 지나간 묶음은 다시 보지 않는다. 진행 중에 플래그가 바뀌어도 앞선 자리로 되돌아가지 않는다 —
/// 되돌아본다면 방금 켠 플래그 때문에 이미 지나온 장면이 다시 열리는 일이 생긴다.
///
/// 한 묶음에서 둘 이상이 참이면 아무거나 고르지 않고 멈춘다(AUTO_SCENE_AMBIGUOUS). 어느 쪽이 맞는지는
/// 데이터가 정할 일이고, 실행기가 임의로 고르면 그날그날 다른 장면이 나온다.
/// </summary>
public class StorySceneCursor
{
    readonly List<List<NewScriptSceneData>> groups = new();
    readonly IConditionUtil conditions;

    int nextGroup;

    /// <summary>남은 묶음이 없으면 true.</summary>
    public bool IsDone => nextGroup >= groups.Count;

    /// <summary>자동 실행 대상 씬이 하나도 없는 날인지. 그런 날은 2부를 열지 않는다(Day 3).</summary>
    public bool IsEmpty => groups.Count == 0;

    public StorySceneCursor(NewDayScriptBase script, IConditionUtil conditions)
    {
        this.conditions = conditions;

        BuildGroups(script);
    }

    /// <summary>
    /// phase가 bar이고 자동으로 시작하는 씬만 모아 seq로 묶는다.
    ///
    /// 같은 파일에 있어도 카메오·수동·상호작용 씬은 넣지 않는다 — 그것들은 전용 이벤트나 goto로 불린다.
    /// bar_open(개점 대화)도 2부 본편이 아니다.
    /// </summary>
    void BuildGroups(NewDayScriptBase script)
    {
        if (script?.Scenes == null) return;

        var bySeq = new SortedDictionary<int, List<NewScriptSceneData>>();

        foreach (var scene in script.Scenes)
        {
            if (scene.Phase != ENewScenePhase.Bar) continue;
            if (scene.Trigger != ENewSceneTrigger.Auto) continue;

            if (!bySeq.TryGetValue(scene.Seq, out var list))
            {
                list = new List<NewScriptSceneData>();
                bySeq[scene.Seq] = list;
            }

            list.Add(scene);
        }

        foreach (var pair in bySeq)
            groups.Add(pair.Value);
    }

    /// <summary>
    /// 다음에 실행할 씬을 고른다. 조건이 거짓인 묶음은 지나가고, 남은 묶음이 없으면 false.
    ///
    /// 거짓인 묶음도 "평가했다"로 치고 커서를 넘긴다. 조건을 다시 볼 기회를 주지 않는 것이 규칙이다.
    /// </summary>
    public bool TryTakeNext(out NewScriptSceneData scene)
    {
        while (nextGroup < groups.Count)
        {
            List<NewScriptSceneData> group = groups[nextGroup++];

            if (!TryPickFromGroup(group, out scene)) continue;

            return true;
        }

        scene = default;
        return false;
    }

    /// <summary>한 묶음에서 조건이 참인 씬 하나를 고른다. 둘 이상이면 고르지 않는다.</summary>
    bool TryPickFromGroup(List<NewScriptSceneData> group, out NewScriptSceneData picked)
    {
        picked = default;
        int matched = 0;

        foreach (var scene in group)
        {
            if (!conditions.Check(scene.When)) continue;

            matched++;
            if (matched == 1) picked = scene;
        }

        if (matched <= 1) return matched == 1;

        var ids = new List<string>(group.Count);
        foreach (var scene in group)
        {
            if (conditions.Check(scene.When)) ids.Add(scene.Id);
        }

        Debug.LogError($"[Story] 같은 순서(seq {group[0].Seq})에서 씬이 둘 이상 참이라 고를 수 없습니다: " +
                       $"{string.Join(", ", ids)} — 대본의 when을 확인하세요.");

        picked = default;
        return false;
    }
}
