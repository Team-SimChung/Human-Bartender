using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>NPC 설정 도구가 같은 직렬화 규칙으로 연출을 읽고 쓴다. 지원하지 않는 새 자료형은 누락시키지 않고 알린다.</summary>
internal static class NpcSequenceEditorData
{
    private static readonly FieldInfo[] StepFields = FindSerializedStepFields();

    internal static List<NpcSequence> ReadList(SerializedProperty rows)
    {
        var sequences = new List<NpcSequence>(rows.arraySize);
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var actions = Required(row, nameof(NpcSequence.actions));
            var sequence = new NpcSequence
            {
                id = Required(row, nameof(NpcSequence.id)).stringValue,
                trigger = (NpcSequenceTrigger)Required(row, nameof(NpcSequence.trigger)).intValue,
                worldAction = (OutsideStoryWorldAction)Required(row, nameof(NpcSequence.worldAction)).intValue,
                requiredCondition = Required(row, nameof(NpcSequence.requiredCondition)).stringValue,
                actions = new NpcActionStep[actions.arraySize]
            };
            for (int j = 0; j < actions.arraySize; j++)
                sequence.actions[j] = ReadStep(actions.GetArrayElementAtIndex(j));
            sequences.Add(sequence);
        }
        return sequences;
    }

    internal static void WriteList(SerializedProperty rows, IReadOnlyList<NpcSequence> sequences)
    {
        rows.arraySize = sequences.Count;
        for (int i = 0; i < sequences.Count; i++) WriteSequence(rows.GetArrayElementAtIndex(i), sequences[i]);
    }

    internal static void WriteSequence(SerializedProperty row, NpcSequence sequence)
    {
        Required(row, nameof(NpcSequence.id)).stringValue = sequence.id ?? "";
        Required(row, nameof(NpcSequence.trigger)).intValue = (int)sequence.trigger;
        Required(row, nameof(NpcSequence.worldAction)).intValue = (int)sequence.worldAction;
        Required(row, nameof(NpcSequence.requiredCondition)).stringValue = sequence.requiredCondition ?? "";
        var actions = Required(row, nameof(NpcSequence.actions));
        actions.arraySize = sequence.actions.Length;
        for (int i = 0; i < sequence.actions.Length; i++) WriteStep(actions.GetArrayElementAtIndex(i), sequence.actions[i]);
    }

    internal static void Upsert(List<NpcSequence> sequences, NpcSequence replacement)
    {
        int matchingIndex = -1;
        for (int i = 0; i < sequences.Count; i++)
        {
            var candidate = sequences[i];
            bool matches = replacement.trigger == NpcSequenceTrigger.WorldAction
                ? candidate.trigger == NpcSequenceTrigger.WorldAction && candidate.worldAction == replacement.worldAction
                : candidate.id == replacement.id;
            if (!matches) continue;
            if (matchingIndex >= 0) throw new InvalidOperationException("NPC 동작 목록이 중복되었습니다: " + replacement.id);
            matchingIndex = i;
        }
        if (matchingIndex >= 0)
        {
            var original = sequences[matchingIndex];
            replacement.id = original.id;
            replacement.requiredCondition = original.requiredCondition;
            sequences[matchingIndex] = replacement;
            return;
        }
        foreach (var candidate in sequences)
            if (candidate.id == replacement.id)
                throw new InvalidOperationException("NPC 연출 ID가 다른 목록과 중복되었습니다: " + replacement.id);
        sequences.Add(replacement);
    }

    internal static bool RequiresStaging(IReadOnlyList<NpcSequence> sequences)
    {
        foreach (var sequence in sequences)
            foreach (var action in sequence.actions)
                if (action.cameraWithMovement || action.kind is NpcActionKind.MovePlayer or NpcActionKind.SetPlayerPosition or
                    NpcActionKind.FacePlayer or NpcActionKind.CameraFocus or NpcActionKind.CameraPan or
                    NpcActionKind.CameraSet or NpcActionKind.CameraRestore) return true;
        return false;
    }

    private static FieldInfo[] FindSerializedStepFields()
    {
        var fields = new List<FieldInfo>();
        foreach (var field in typeof(NpcActionStep).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsInitOnly || field.IsDefined(typeof(NonSerializedAttribute), false) ||
                !field.IsPublic && !field.IsDefined(typeof(SerializeField), false)) continue;
            var type = field.FieldType;
            if (!type.IsEnum && type != typeof(bool) && type != typeof(float) && type != typeof(string) &&
                type != typeof(Vector2) && !typeof(UnityEngine.Object).IsAssignableFrom(type))
                throw new InvalidOperationException("NPC 동작 필드의 편집용 직렬화 지원을 추가하세요: " + field.Name + " / " + type.Name);
            fields.Add(field);
        }
        return fields.ToArray();
    }

    private static NpcActionStep ReadStep(SerializedProperty row)
    {
        var step = new NpcActionStep();
        foreach (var field in StepFields)
        {
            var value = Required(row, field.Name);
            var type = field.FieldType;
            object read;
            if (type.IsEnum) read = Enum.ToObject(type, value.intValue);
            else if (type == typeof(bool)) read = value.boolValue;
            else if (type == typeof(float)) read = value.floatValue;
            else if (type == typeof(string)) read = value.stringValue;
            else if (type == typeof(Vector2)) read = value.vector2Value;
            else read = value.objectReferenceValue;
            field.SetValue(step, read);
        }
        return step;
    }

    private static void WriteStep(SerializedProperty row, NpcActionStep step)
    {
        foreach (var field in StepFields)
        {
            var value = Required(row, field.Name);
            var type = field.FieldType;
            object written = field.GetValue(step);
            if (type.IsEnum) value.intValue = Convert.ToInt32(written);
            else if (type == typeof(bool)) value.boolValue = (bool)written;
            else if (type == typeof(float)) value.floatValue = (float)written;
            else if (type == typeof(string)) value.stringValue = (string)written ?? "";
            else if (type == typeof(Vector2)) value.vector2Value = (Vector2)written;
            else value.objectReferenceValue = (UnityEngine.Object)written;
        }
    }

    private static SerializedProperty Required(SerializedProperty parent, string name)
    {
        return parent.FindPropertyRelative(name) ??
            throw new InvalidOperationException("NPC 연출의 직렬화 필드를 찾을 수 없습니다: " + parent.propertyPath + "." + name);
    }
}
