using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private void BuildCameraFollow(VisualElement section, SerializedObject data)
        {
            var ids = new List<string> { "" };
            var labels = new List<string> { "없음 / 직접 위치 지정" };
            foreach (var actor in host.Sequence.Actors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.Id)) continue;
                ids.Add(actor.Id); labels.Add($"{actor.Name} ({actor.Id})");
            }
            int index = ids.IndexOf(host.Selected.CameraTargetId ?? "");
            if (index < 0) { index = ids.Count; ids.Add(host.Selected.CameraTargetId); labels.Add("연결 없음 / " + host.Selected.CameraTargetId); }
            var picker = new PopupField<string>("따라갈 오브젝트", labels, index) { name = "camera-follow-target" };
            section.Add(picker);
            picker.RegisterValueChangedCallback(_ =>
            {
                data.UpdateIfRequiredOrScript();
                StudioSerializedFields.Find(data, "CameraTargetId").stringValue = ids[picker.index];
                if (data.ApplyModifiedProperties()) host.Changed(true);
            });
            BoundField(section, data, "CameraFollowOffset", "위치 보정", index != 0);
            Note(section, "대상을 선택하면 이동을 따라갑니다. 위치 키는 대상 기준의 추가 보정으로 사용하며, 종료 상태 유지는 클립 끝 구도를 유지합니다.");
        }
    }
}
