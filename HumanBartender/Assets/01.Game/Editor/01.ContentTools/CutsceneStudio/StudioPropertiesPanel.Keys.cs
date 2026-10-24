using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        [NonSerialized] private StudioProperty listedProperty = StudioProperty.PositionX;

        private void BuildRecordedKeyFields(TimelineClip clip)
        {
            var properties = StudioKeyframes.Properties(host.Selected);
            if (host.KeySelectionOwner == host.Selected && host.KeyIndex >= 0)
                listedProperty = host.KeyProperty;
            var labels = new List<string>();
            int selected = 0;
            for (int i = 0; i < properties.Count; i++)
            {
                labels.Add(StudioTimelinePanel.PropertyLabel(host.Selected, properties[i]));
                if (properties[i] == listedProperty) selected = i;
            }
            listedProperty = properties[selected];
            var section = Section("기록된 키 / 선택 키 편집", "recorded-keys");
            Note(section, "목록에서 고른 키를 편집합니다. 현재 시점 영역과 같은 키 데이터입니다.");
            var picker = new PopupField<string>("편집할 속성", labels, selected) { name = "recorded-key-property" };
            section.Add(picker);
            var list = new ScrollView { name = "recorded-key-list", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            list.AddToClassList("studio-properties-key-list");
            section.Add(list);
            picker.RegisterValueChangedCallback(_ =>
            {
                listedProperty = properties[picker.index];
                host.Session.ClearKey();
                inspectorIdentity = null;
                Sync(false);
            });
            string previous = null;
            refreshFields.Add(Refresh);
            Refresh();
            BuildSelectedKeyFields(clip, section);
            if (host.KeySelectionOwner != host.Selected || host.KeyIndex < 0)
                Note(section, "목록에서 편집할 키를 선택하세요.");

            void Refresh()
            {
                var channel = StudioKeyframes.Channel(host.Selected, listedProperty);
                // 기존 포즈 키도 평가기에서 속성별로 읽어 표시하며, 열기만으로 변환하지 않음.
                var keys = host.Selected.Kind == StudioKind.State ? StudioKeyframes.GetKeys(host.Selected, listedProperty, clip.duration) : channel?.Keys;
                var identity = new StringBuilder();
                identity.Append(host.KeyProperty).Append(':').Append(host.KeyIndex).Append(':').Append(host.Playing);
                if (keys != null)
                    foreach (var key in keys)
                        if (key != null) identity.Append('|').Append(key.Time).Append(':').Append(key.Value);
                string next = identity.ToString();
                if (previous == next) return;
                previous = next;
                list.Clear();
                if (keys == null || keys.Length == 0)
                {
                    if (host.Selected.Kind == StudioKind.State)
                    {
                        Note(list, channel == null ? "기록된 키 없음 / 현재 시점에서 키를 추가하세요." : "기록된 키 없음 / 이 속성의 기본값을 사용합니다.");
                        return;
                    }
                    Note(list, channel == null ? host.Selected.Kind == StudioKind.Effect ?
                        "기록된 키 없음 / 시작값과 종료값의 기본값으로 재생됩니다." :
                        "기록된 키 없음 / 시작점과 도착점의 기본값으로 재생됩니다." : "기록된 키 없음 / 시작 기본값을 사용합니다.");
                    return;
                }
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] == null) continue;
                    int index = i;
                    float time = keys[i].Time;
                    var row = StudioGUI.ActionButton($"{time:0.###}초  /  {keys[i].Value:0.###}", () =>
                    {
                        host.Session.SelectKey(host.Selected, listedProperty, index);
                        host.Seek(clip.start + time);
                        Sync(false);
                    }, "recorded-key-" + i);
                    row.EnableInClassList("is-selected", host.KeySelectionOwner == host.Selected && host.KeyProperty == listedProperty && host.KeyIndex == i);
                    row.SetEnabled(!host.Playing);
                    list.Add(row);
                }
            }
        }
    }
}
