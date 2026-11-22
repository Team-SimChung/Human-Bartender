using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 애니메이션 항목 맨 위의 엔진 클립 칸과 상태 줄. 판정과 연결 / 해제는 StudioEngineClips에 맡김.
    internal sealed partial class StudioPropertiesPanel
    {
        private const string EngineSame = "engine-same", EngineDiffers = "engine-differs", EngineError = "engine-error";
        // 속성 창을 다시 그려도 같은 타임라인 클립이면 같은 엔진 클립 칸을 씀(선택 창이 자기를 연 칸에 값을 넣기 때문).
        [System.NonSerialized] private readonly StudioKeptObjectField engineField = new();

        private void BuildEngineClip(VisualElement parent)
        {
            var asset = host.Selected;
            var sequence = host.Sequence;
            const string tip = "게임에서 이 그림을 재생하는 애니메이션 클립입니다. Project 창에서 끌어다 놓거나 선택하면 키 시간 / 길이 / 반복 / " +
                "엔진 속도 보정을 가져옵니다. 같은 그림이면 지금 프레임 이미지는 유지하고, 다르면 프레임 목록도 바꿉니다. " +
                "재생 배속 / 원본 시작 / 클립 길이는 바꾸지 않습니다. 비우면 연결을 해제합니다.";

            var row = new VisualElement { name = "engine-clip-row" };
            row.AddToClassList("studio-properties-engine-row");
            var label = new Label("엔진 클립") { tooltip = tip };
            label.AddToClassList("studio-properties-engine-label");
            var field = engineField.Take(asset);
            field.name = "engine-clip";
            field.objectType = typeof(AnimationClip);
            field.allowSceneObjects = false;
            field.tooltip = tip;
            field.AddToClassList("studio-properties-engine-field");
            var buttons = new VisualElement();
            buttons.AddToClassList("studio-properties-engine-buttons");
            var reload = StudioGUI.ActionButton("↻", null, "engine-reload");
            reload.tooltip = "원본에서 다시 가져오기 / 연결된 엔진 클립의 키 시간 / 길이 / 반복 / 엔진 속도 보정을 다시 넣습니다.";
            var unlink = StudioGUI.ActionButton("X", null, "engine-unlink");
            unlink.tooltip = "연결 해제 / 키 시간을 버리고 초당 프레임 방식으로 되돌립니다. 프레임 / 반복 / 재생 배속 / 원본 시작 / 클립 길이는 그대로입니다.";
            buttons.Add(reload);
            buttons.Add(unlink);
            row.Add(label);
            row.Add(field);
            row.Add(buttons);
            parent.Add(row);

            var statusRow = new VisualElement { name = "engine-status-row" };
            statusRow.AddToClassList("studio-properties-engine-status");
            var gutter = new VisualElement();
            gutter.AddToClassList("studio-properties-engine-gutter");
            var status = new Label { name = "engine-status" };
            statusRow.Add(gutter);
            statusRow.Add(status);
            parent.Add(statusRow);

            reload.clicked += Reload;
            unlink.clicked += Unlink;
            engineField.Listen(Pick);
            refreshFields.Add(Refresh);
            Refresh();

            bool CanEdit() => !host.Playing && host.Sequence == sequence && asset != null;

            void Pick(ChangeEvent<Object> e)
            {
                if (!CanEdit()) { Refresh(); return; }
                if (e.newValue == null) { Unlink(); return; }
                var clip = e.newValue as AnimationClip;
                string problem = StudioEngineClips.LinkProblem(clip);
                if (problem != null)
                {
                    engineField.Reject("넣을 수 없음 · " + problem, host.ContentVersion);
                    Refresh();
                    return;
                }
                engineField.ClearRejection();
                Undo.RecordObject(asset, "엔진 클립 연결");
                StudioEngineClips.Link(asset, clip, StudioTiming.FrameDuration(sequence));
                EditorUtility.SetDirty(asset);
                host.Changed(true);
            }

            void Unlink()
            {
                if (!CanEdit() || !StudioEngineClips.CanUnlink(asset)) { Refresh(); return; }
                engineField.ClearRejection();
                Undo.RecordObject(asset, "엔진 클립 연결 해제");
                StudioEngineClips.Unlink(asset);
                EditorUtility.SetDirty(asset);
                host.Changed(true);
            }

            void Reload()
            {
                var source = StudioEngineClips.Source(asset);
                if (!CanEdit() || source == null) return;
                engineField.ClearRejection();
                Undo.RecordObject(asset, "원본에서 다시 가져오기");
                StudioEngineClips.Apply(asset, source, StudioTiming.FrameDuration(sequence));
                EditorUtility.SetDirty(asset);
                host.Changed(true);
            }

            void Refresh()
            {
                bool editable = !host.Playing;
                bool linked = !string.IsNullOrEmpty(asset.SourceAnimationGuid);
                var source = StudioEngineClips.Source(asset);
                field.SetValueWithoutNotify(StudioEngineClips.SourceClip(asset));
                field.SetEnabled(editable);
                reload.style.display = linked && source != null ? DisplayStyle.Flex : DisplayStyle.None;
                unlink.style.display = StudioEngineClips.CanUnlink(asset) ? DisplayStyle.Flex : DisplayStyle.None;
                reload.SetEnabled(editable);
                unlink.SetEnabled(editable);

                var result = StudioEngineClips.Evaluate(asset);
                string kind = null;
                // 거부 문구는 그 뒤 시퀀스 내용이 바뀌면(다른 칸 편집, 연결 / 해제 / 다시 가져오기, Undo) 지우고 평소 상태로 돌아감.
                string rejected = engineField.RejectionAt(host.ContentVersion);
                if (rejected != null)
                {
                    status.text = rejected;
                    kind = EngineError;
                }
                else switch (result.Match)
                {
                    case StudioEngineMatch.Same:
                        status.text = $"엔진과 같은 속도 · 한 바퀴 {result.Current:0.00}초";
                        kind = EngineSame;
                        break;
                    case StudioEngineMatch.Different:
                        status.text = $"엔진과 속도가 다름 · 지금 {result.Current:0.00}초, 엔진 {result.Engine.Cycle:0.00}초 ({result.Engine.Name})";
                        kind = EngineDiffers;
                        break;
                    case StudioEngineMatch.MissingSource:
                        status.text = $"원본 클립을 찾을 수 없음 · 저장된 값으로 재생 (한 바퀴 {result.Current:0.00}초)";
                        kind = EngineDiffers;
                        break;
                    default:
                        status.text = $"한 바퀴 {result.Current:0.00}초";
                        break;
                }
                status.EnableInClassList(EngineSame, kind == EngineSame);
                status.EnableInClassList(EngineDiffers, kind == EngineDiffers);
                status.EnableInClassList(EngineError, kind == EngineError);
                status.tooltip = StatusTooltip(result, source, linked);
                bool still = asset.Frames == null || asset.Frames.Length < 2;
                statusRow.style.display = rejected == null && !linked && still ? DisplayStyle.None : DisplayStyle.Flex;
            }

            string StatusTooltip(StudioEngineStatus result, StudioEngineClip source, bool linked)
            {
                var lines = new List<string>();
                float stored = StudioExtensionValues.Speed(asset.EngineSpeed);
                if (source != null)
                {
                    var info = source.Speed;
                    lines.Add($"엔진 속도 보정 ×{stored:0.##} · {info.Reason}" + (Mathf.Abs(info.Value - stored) > .0005f
                        ? $" (지금 엔진 규칙은 ×{info.Value:0.##}, ↻(원본에서 다시 가져오기)로 맞출 수 있음)" : ""));
                }
                else
                    lines.Add($"엔진 속도 보정 ×{stored:0.##} · " + (linked ? "원본 없음, 저장된 값 사용" : "연결된 엔진 클립 없음"));
                if (result.Engine != null && result.Engine != source)
                    lines.Add($"비교한 엔진 클립 {result.Engine.Name}: {result.Engine.Speed.Label}, 한 바퀴 {result.Engine.Cycle:0.00}초");
                if (StudioPropertyEditing.UsesSourceTiming(asset))
                    lines.Add($"원본 키 시간 길이 {asset.AnimationLength:0.###}초");
                if (source?.Ignored != null)
                    lines.Add(source.Ignored);
                return string.Join("\n", lines);
            }
        }
    }

    // 속성 창을 다시 그려도 같은 주인(타임라인 클립)이면 같은 ObjectField를 다시 붙인다.
    // ObjectField 선택 창은 자기를 연 필드 인스턴스에 값을 넣고, 패널에서 떨어진 필드는 ChangeEvent를 보내지 않기 때문.
    // 주인이 바뀌면 옛 필드는 버리고 콜백을 떼어, 열려 있던 선택 창의 클릭이 새 클립에 들어가지 않게 한다.
    internal sealed class StudioKeptObjectField
    {
        private EventCallback<ChangeEvent<Object>> changed;
        private string rejection;
        private int rejectedVersion;

        internal ObjectField Field { get; private set; }
        internal Object Owner { get; private set; }

        // 같은 주인이면 지금 필드를 떼어 돌려주고, 다르면 새 필드를 만든다. 값 콜백은 Listen으로 다시 붙인다.
        internal ObjectField Take(Object owner)
        {
            if (Field == null || Owner != owner)
            {
                Release();
                Field = new ObjectField();
                Owner = owner;
            }
            else
            {
                Field.RemoveFromHierarchy();
                if (changed != null)
                    Field.UnregisterValueChangedCallback(changed);
                changed = null;
            }
            return Field;
        }

        internal void Listen(EventCallback<ChangeEvent<Object>> callback)
        {
            if (Field == null)
                return;
            if (changed != null)
                Field.UnregisterValueChangedCallback(changed);
            changed = callback;
            Field.RegisterValueChangedCallback(changed);
        }

        internal void Release()
        {
            if (Field != null)
            {
                if (changed != null)
                    Field.UnregisterValueChangedCallback(changed);
                Field.RemoveFromHierarchy();
            }
            Field = null;
            Owner = null;
            changed = null;
            ClearRejection();
        }

        // 받을 수 없는 클립을 넣었을 때의 문구. 그때의 시퀀스 내용 버전과 함께 둔다.
        internal void Reject(string message, int contentVersion)
        {
            rejection = message;
            rejectedVersion = contentVersion;
        }

        internal void ClearRejection() => rejection = null;

        // 내용 버전이 그대로면 거부 문구를, 바뀌었으면 지우고 null을 돌려준다.
        internal string RejectionAt(int contentVersion)
        {
            if (rejection != null && rejectedVersion != contentVersion)
                rejection = null;
            return rejection;
        }
    }
}
