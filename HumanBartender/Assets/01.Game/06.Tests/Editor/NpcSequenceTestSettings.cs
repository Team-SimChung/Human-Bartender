using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>씬에 작성된 공통 연출 목록의 실제 설정을 읽고 검사 값을 조정한다.</summary>
internal static class NpcSequenceTestSettings
{
    internal static SerializedProperty WorldActions(SerializedObject data, OutsideStoryWorldAction command)
    {
        var sequences = data.FindProperty("sequences");
        for (int i = 0; i < sequences.arraySize; i++)
        {
            var sequence = sequences.GetArrayElementAtIndex(i);
            if (sequence.FindPropertyRelative("trigger").enumValueIndex == (int)NpcSequenceTrigger.WorldAction &&
                sequence.FindPropertyRelative("worldAction").enumValueIndex == (int)command)
                return sequence.FindPropertyRelative("actions");
        }
        throw new InvalidOperationException("검사할 CSV 연출 연결이 없습니다: " + command);
    }

    internal static NpcActionKind[] Kinds(SerializedProperty actions) => Enumerable.Range(0, actions.arraySize)
        .Select(index => (NpcActionKind)actions.GetArrayElementAtIndex(index).FindPropertyRelative("kind").enumValueIndex).ToArray();

    // Isolated clones retain real camera actions. Supply a small real rig instead of deleting their staging steps.
    internal static CameraControllerNew CreateStagingCamera(Transform player)
    {
        var camera = new GameObject("NPC test camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = 16f / 9;
        var pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
        pixel.refResolutionX = 480; pixel.refResolutionY = 270; pixel.assetsPPU = 100;
        var vcam = new GameObject("NPC test virtual camera").AddComponent<CinemachineCamera>();
        var composer = vcam.gameObject.AddComponent<CinemachinePositionComposer>();
        composer.TargetOffset = new Vector3(0, .7f, 0);
        var anchor = new GameObject("NPC test camera anchor").transform;
        anchor.SetParent(player, false);
        vcam.Target.TrackingTarget = anchor;
        var lens = vcam.Lens; lens.OrthographicSize = 2.5f; vcam.Lens = lens;
        var rig = new GameObject("NPC test camera controller").AddComponent<CameraControllerNew>();
        using var data = new SerializedObject(rig);
        data.FindProperty("mainCamera").objectReferenceValue = camera;
        data.FindProperty("pixelPerfectCamera").objectReferenceValue = pixel;
        data.FindProperty("vcam").objectReferenceValue = vcam;
        data.FindProperty("cameraAnchor").objectReferenceValue = anchor;
        data.ApplyModifiedPropertiesWithoutUndo();
        return rig;
    }

    internal static SerializedProperty Action(SerializedObject data, NpcActionKind kind)
    {
        var sequences = data.FindProperty("sequences");
        for (int i = 0; i < sequences.arraySize; i++)
        {
            var actions = sequences.GetArrayElementAtIndex(i).FindPropertyRelative("actions");
            for (int j = 0; j < actions.arraySize; j++)
            {
                var action = actions.GetArrayElementAtIndex(j);
                if (action.FindPropertyRelative("kind").enumValueIndex == (int)kind) return action;
            }
        }
        throw new InvalidOperationException("검사할 NPC 연출 동작이 없습니다: " + kind);
    }

    internal static void SetDuration(NpcInteraction value, NpcActionKind kind, float duration)
    {
        using var data = new SerializedObject(value);
        Action(data, kind).FindPropertyRelative("duration").floatValue = duration;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
