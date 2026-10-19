using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using System.Linq;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using HumanBartender.CutsceneStudio;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 패널의 표시와 입력을 처리하며 다른 패널은 호스트 계약으로 연결함.
    [Serializable]
    internal sealed class StudioTimelinePanel
    {
        [NonSerialized] private IStudioTimelineHost host;
        private CutsceneStudioWindow Window => host.Window;
        internal void Initialize(IStudioTimelineHost value) { host = value; }

        private void HandleTimelineDrop(Rect viewport, StudioTimelineRow[] rows)
        {
            var e = Event.current;
            if (!viewport.Contains(e.mousePosition) || e.mousePosition.y < viewport.y + StudioTimelineLayout.RulerHeight || e.mousePosition.x < viewport.x + TrackHeaderWidth || e.mousePosition.x >= viewport.xMax - 16 || e.mousePosition.y >= viewport.yMax - 16)
                return;
            Vector2 point = e.mousePosition - viewport.position + timelineScroll;
            var hovered = StudioTimelineLayout.TrackAt(rows, point.y);
            double time = Math.Max(0, host.Snap((point.x - TrackHeaderWidth) / zoom));
            host.ProcessDrop(viewport, true, time, hovered);
        }

        private bool keyframeMode { get { return host.ViewState.Keyframes; } set { host.ViewState.Keyframes = value; } }

        [NonSerialized] private readonly HashSet<StudioClip> collapsedKeyClips = new();

        [NonSerialized] private int keyDragControl, keyUndoGroup;

        [NonSerialized] private bool draggingKey;

        [NonSerialized] private float keyDragTime, keyDragPixels;

        private static readonly string[] PropertyNames =
        {
            "위치 X",
            "위치 Y",
            "크기",
            "회전",
            "불투명도",
            "강도"
        };

        private static readonly Color[] PropertyColors =
        {
            new(.82f, .52f, .52f),
            new(.58f, .74f, .48f),
            new(.48f, .66f, .86f),
            new(.74f, .57f, .80f),
            new(.77f, .73f, .50f),
            new(.83f, .63f, .46f)
        };

        private sealed class KeyRow
        {
            public float Y;
            public int Group;
            public TimelineClip Clip;
            public StudioProperty? Property;
        }


        private List<KeyRow> BuildKeyRows()
        {
            var result = new List<KeyRow>();
            var entries = StudioEvaluation.Clips(host.Sequence, true).Where(MatchesTrueWhere).ToArray();
            float y = 28;
            foreach (int group in new[]
            {
                0,
                1,
                5,
                3,
                4
            }

            )
            {
                result.Add(new KeyRow { Y = y, Group = group });
                y += 28;
                if ((collapsedTimelineGroups & (1 << group)) != 0)
                    continue;
                foreach (var entry in entries.Where(MatchesEntriesWhere))
                {
                    result.Add(new KeyRow { Y = y, Group = group, Clip = entry.Clip });
                    y += 28;
                    if (collapsedKeyClips.Contains(entry.Asset))
                        continue;
                    foreach (var property in StudioKeyframes.Properties(entry.Asset))
                    {
                        result.Add(new KeyRow { Y = y, Group = group, Clip = entry.Clip, Property = property });
                        y += 28;
                    }
                }

                bool MatchesEntriesWhere(StudioEntry e)
                {
                    return StudioTimelineLayout.Group(e.Asset) == group;
                }
            }

            return result;
            bool MatchesTrueWhere(StudioEntry e)
            {
                return StudioKeyframes.Supports(e.Asset);
            }
        }


        private void SetTimelineMode(bool mode)
        {
            if (mode == keyframeMode && !host.DialogueMode)
                return;
            ReleaseKeyDrag();
            ReleaseTimelineResize();
            ReleaseTimelineScrub();
            host.DialogueMode = false;
            keyframeMode = mode;
            timelineScroll = Vector2.zero;
            host.Session.ClearKey();
            if (Event.current != null)
                GUI.FocusControl(null);
            host.PositionToolkitPanels();
            Window.Repaint();
        }


        private void DrawTimelineTabs(Rect area)
        {
            float y = area.y + TimelineToolbarHeight - 28;
            EditorGUI.DrawRect(new Rect(area.x, y, area.width, 28), new Color(.19f, .19f, .19f));
            if (GUI.Toggle(new Rect(area.x + 8, y + 3, 52, 22), !keyframeMode && !host.DialogueMode, "클립", EditorStyles.toolbarButton))
                SetTimelineMode(false);
            if (GUI.Toggle(new Rect(area.x + 64, y + 3, 88, 22), keyframeMode, "키프레임", EditorStyles.toolbarButton))
                SetTimelineMode(true);
            if (GUI.Toggle(new Rect(area.x + 156, y + 3, 52, 22), host.DialogueMode, "대사", EditorStyles.toolbarButton))
                host.SetDialogueMode();
            if (keyframeMode)
            {
                GUI.Label(new Rect(area.x + 218, y + 4, Mathf.Max(0, area.width - 358), 20), "더블 클릭 : 추가 / 드래그 : 이동 / Delete : 삭제", EditorStyles.miniLabel);
            }

            if (!host.DialogueMode)
            {
                string label = Math.Max(0, (long)Math.Round(host.Clock.Time * StudioTiming.FrameRate(host.Sequence))) + "f / " + StudioTiming.FrameRate(host.Sequence).ToString("0.##") + "Fps";
                var frameStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
                GUI.Label(new Rect(area.xMax - 135, y + 4, 123, 20), label, frameStyle);
            }
        }


        private void DrawKeyframeTimeline(Rect area)
        {
            var viewport = new Rect(area.x, area.y + TimelineToolbarHeight, area.width, area.height - TimelineToolbarHeight - 27);
            HandleTimelineResize(viewport, Array.Empty<StudioTimelineRow>());
            var rows = BuildKeyRows();
            float bottom = rows.Last().Y + 28;
            var content = new Rect(0, 0, Mathf.Max(viewport.width - 18, TrackHeaderWidth + (float)host.Sequence.Duration * zoom + 60), Mathf.Max(viewport.height - 18, bottom + 25));
            var e = Event.current;
            bool inBody = viewport.Contains(e.mousePosition) && e.mousePosition.y >= viewport.y + 28 && e.mousePosition.y < viewport.yMax - 16;
            bool inKeys = inBody && e.mousePosition.x > viewport.x + TrackHeaderWidth && e.mousePosition.x < viewport.xMax - 16;
            int control = GUIUtility.GetControlID("StudioKeyDrag".GetHashCode(), FocusType.Passive);
            int first = Mathf.Max(0, Mathf.FloorToInt(timelineScroll.x / zoom));
            int last = Mathf.CeilToInt((timelineScroll.x + viewport.width - TrackHeaderWidth) / zoom);
            timelineScroll = GUI.BeginScrollView(viewport, timelineScroll, content);
            foreach (var row in rows)
            {
                EditorGUI.DrawRect(new Rect(0, row.Y, content.width, 28), row.Clip == null ? new Color(.23f, .23f, .23f) : row.Property.HasValue ? new Color(.17f, .17f, .17f) : new Color(.20f, .20f, .20f));
                EditorGUI.DrawRect(new Rect(0, row.Y + 27, content.width, 1), new Color(.12f, .12f, .12f));
            }

            for (int s = first; s <= last; s++)
                EditorGUI.DrawRect(new Rect(TrackHeaderWidth + s * zoom, 28, 1, content.height - 28), new Color(.25f, .25f, .25f));
            foreach (var row in rows)
            {
                if (row.Y + 28 < timelineScroll.y + 28 || row.Y > timelineScroll.y + viewport.height)
                    continue;
                if (row.Clip == null)
                {
                    foreach (var entry in StudioEvaluation.Clips(host.Sequence, true).Where(MatchesTrueWhere2))
                        EditorGUI.DrawRect(new Rect(TrackHeaderWidth + (float)entry.Clip.start * zoom, row.Y + 11, (float)entry.Clip.duration * zoom, 5), StudioGUI.ClipColor(entry.Asset.Kind));
                    continue;
                    bool MatchesTrueWhere2(StudioEntry c)
                    {
                        return StudioKeyframes.Supports(c.Asset) && StudioTimelineLayout.Group(c.Asset) == row.Group;
                    }
                }

                var asset = (StudioClip)row.Clip.asset;
                float start = TrackHeaderWidth + (float)row.Clip.start * zoom;
                float width = (float)row.Clip.duration * zoom;
                if (!row.Property.HasValue)
                {
                    var bar = new Rect(start, row.Y + 4, width, 20);
                    EditorGUI.DrawRect(bar, asset == host.Selected ? new Color(.22f, .36f, .47f) : new Color(.27f, .29f, .31f));
                    GUI.Label(bar, row.Clip.displayName, EditorStyles.miniLabel);
                    if (inKeys && e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
                    {
                        host.Select(row.Clip, false);
                        host.Session.SelectKey(asset, StudioKeyframes.Properties(asset)[0], -1);
                        e.Use();
                    }

                    continue;
                }

                var property = row.Property.Value;
                var keys = StudioKeyframes.GetKeys(asset, property, row.Clip.duration);
                var color = PropertyColors[(int)property];
                EditorGUI.DrawRect(new Rect(start, row.Y + 13, width, 1), color * new Color(1, 1, 1, .35f));
                for (int i = 0; i < keys.Length; i++)
                {
                    var key = keys[i];
                    if (key == null)
                        continue;
                    float x = start + key.Time * zoom;
                    if (x < timelineScroll.x + TrackHeaderWidth || x > timelineScroll.x + viewport.width - 16)
                        continue;
                    bool chosen = host.KeySelectionOwner == asset && host.KeyProperty == property && host.KeyIndex == i;
                    var hit = new Rect(x - 7, row.Y + 6, 14, 16);
                    if (Event.current.type == EventType.Repaint)
                    {
                        var old = Handles.color;
                        Handles.color = chosen ? Color.white : color;
                        float r = chosen ? 6 : 5;
                        Handles.DrawAAConvexPolygon(new Vector3(x, row.Y + 14 - r), new Vector3(x + r, row.Y + 14), new Vector3(x, row.Y + 14 + r), new Vector3(x - r, row.Y + 14));
                        Handles.color = old;
                    }

                    GUI.Label(hit, new GUIContent("", $"{PropertyLabel(asset, property)} / {key.Time:F2}초 / {key.Value:F2}"));
                    if (inKeys && e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
                    {
                        SelectKey(row.Clip, property, i);
                        Undo.IncrementCurrentGroup();
                        keyUndoGroup = Undo.GetCurrentGroup();
                        Undo.RegisterCompleteObjectUndo(asset, "키프레임 이동");
                        keyDragTime = key.Time;
                        keyDragPixels = 0;
                        draggingKey = true;
                        keyDragControl = control;
                        GUIUtility.hotControl = control;
                        e.Use();
                    }
                }

                if (inKeys && e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && new Rect(start, row.Y, width, 28).Contains(e.mousePosition))
                {
                    AddPropertyKey(row.Clip, property, (float)host.Snap((e.mousePosition.x - start) / zoom));
                    e.Use();
                }
            }

            EditorGUI.DrawRect(new Rect(TrackHeaderWidth + (float)host.Clock.Time * zoom, 28, 2, content.height - 28), StudioGUI.Accent);
            // 클립 화면과 동일하게 이름·값 열을 고정함.
            EditorGUI.DrawRect(new Rect(timelineScroll.x, 0, TrackHeaderWidth, content.height), new Color(.19f, .19f, .19f));
            foreach (var row in rows)
            {
                if (row.Y + 28 < timelineScroll.y + 28 || row.Y > timelineScroll.y + viewport.height)
                    continue;
                float x = timelineScroll.x;
                var rect = new Rect(x, row.Y, TrackHeaderWidth, 28);
                EditorGUI.DrawRect(rect, row.Clip == null ? new Color(.24f, .24f, .24f) : new Color(.19f, .19f, .19f));
                EditorGUI.DrawRect(new Rect(x, row.Y + 27, TrackHeaderWidth, 1), new Color(.12f, .12f, .12f));
                using (new EditorGUI.DisabledScope(!inBody && e.type != EventType.Repaint && e.type != EventType.Layout))
                {
                    if (row.Clip == null)
                    {
                        bool open = (collapsedTimelineGroups & (1 << row.Group)) == 0;
                        if (EditorGUI.Foldout(new Rect(x + 12, row.Y + 4, TrackHeaderWidth - 20, 20), open, StudioClipSchema.Groups[row.Group].GroupLabel, true) != open)
                            collapsedTimelineGroups ^= 1 << row.Group;
                    }
                    else if (!row.Property.HasValue)
                    {
                        var asset = (StudioClip)row.Clip.asset;
                        bool open = !collapsedKeyClips.Contains(asset);
                        string track = string.IsNullOrEmpty(((StudioTrack)row.Clip.GetParentTrack()).StudioDisplayName) ? row.Clip.GetParentTrack().name : ((StudioTrack)row.Clip.GetParentTrack()).StudioDisplayName;
                        if (EditorGUI.Foldout(new Rect(x + 24, row.Y + 4, TrackHeaderWidth - 32, 20), open, new GUIContent(track + " / " + row.Clip.displayName), true) != open)
                        {
                            if (open)
                                collapsedKeyClips.Add(asset);
                            else
                                collapsedKeyClips.Remove(asset);
                        }
                    }
                    else
                    {
                        var asset = (StudioClip)row.Clip.asset;
                        var p = row.Property.Value;
                        var old = GUI.color;
                        GUI.color = PropertyColors[(int)p];
                        if (GUI.Button(new Rect(x + 30, row.Y + 4, TrackHeaderWidth - 130, 20), PropertyLabel(asset, p), EditorStyles.label))
                        {
                            double at = host.Clock.Time;
                            host.Select(row.Clip, false);
                            host.Seek(at);
                            host.Session.SelectKey(asset, p, -1);
                        }

                        GUI.color = old;
                        float value = StudioKeyframes.Evaluate(asset, p, (float)(host.Clock.Time - row.Clip.start), row.Clip.duration);
                        GUI.Label(new Rect(x + TrackHeaderWidth - 98, row.Y + 4, 66, 20), value.ToString("F2"), EditorStyles.miniLabel);
                        if (GUI.Button(new Rect(x + TrackHeaderWidth - 28, row.Y + 4, 22, 20), new GUIContent("+", "현재 시간에 속성 키 추가"), EditorStyles.miniButton))
                            AddPropertyKey(row.Clip, p, (float)(host.Clock.Time - row.Clip.start));
                    }
                }
            }

            GUI.EndScrollView();
            DrawTimelineRuler(viewport, first, last);
            EditorGUI.DrawRect(new Rect(viewport.x + TrackHeaderWidth - 1, viewport.y, 2, viewport.height - 16), new Color(.1f, .1f, .1f));
            if (draggingKey && GUIUtility.hotControl == keyDragControl)
            {
                if (e.type == EventType.MouseDrag && host.KeySelectionOwner != null)
                {
                    var clip = host.FindSelected();
                    var keys = StudioKeyframes.GetKeys(host.KeySelectionOwner, host.KeyProperty, clip.duration);
                    keyDragPixels += e.delta.x;
                    host.Session.UpdateKeyIndex(StudioKeyframeEditing.Set(clip, host.KeyProperty, host.KeyIndex, (float)host.Snap(keyDragTime + keyDragPixels / zoom), keys[host.KeyIndex].Value, false));
                    host.Seek(clip.start + StudioKeyframes.GetKeys(host.KeySelectionOwner, host.KeyProperty, clip.duration)[host.KeyIndex].Time);
                    host.Changed();
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    ReleaseKeyDrag();
                    e.Use();
                }
            }

            DrawTimelineFooter(area);
        }


        internal static string PropertyLabel(StudioClip asset, StudioProperty property) => property == StudioProperty.Scale && StudioKeyframes.IsCamera(asset) ? "카메라 줌" : asset.Kind == StudioKind.Dialogue && property == StudioProperty.PositionX ? "말풍선 이동 X" : asset.Kind == StudioKind.Dialogue && property == StudioProperty.PositionY ? "말풍선 이동 Y" : asset.Kind == StudioKind.Effect && property == StudioProperty.Opacity && asset.Effect == StudioEffect.Shake ? "감쇠" : PropertyNames[(int)property];

        private void SelectKey(TimelineClip clip, StudioProperty property, int index)
        {
            host.Select(clip, false);
            host.Session.SelectKey((StudioClip)clip.asset, property, index);
            host.Seek(clip.start + StudioKeyframes.GetKeys(host.KeySelectionOwner, property, clip.duration)[index].Time);
        }


        private void AddPropertyKey(TimelineClip clip, StudioProperty property, float age)
        {
            if (clip == null)
                return;
            var asset = (StudioClip)clip.asset;
            if (!StudioKeyframes.Properties(asset).Contains(property))
                return;
            age = Mathf.Clamp((float)host.Snap(age), 0, (float)clip.duration);
            float value = StudioKeyframes.Evaluate(asset, property, age, clip.duration);
            int index = StudioKeyframeEditing.Set(clip, property, -1, age, value);
            SelectKey(clip, property, index);
            host.Changed();
        }


        internal void DeleteSelectedKey()
        {
            var clip = host.FindSelected();
            if (clip == null || host.KeySelectionOwner != host.Selected || host.KeyIndex < 0)
                return;
            StudioKeyframeEditing.Delete(clip, host.KeyProperty, host.KeyIndex);
            host.Session.ClearKey();
            host.Changed();
        }


        internal void ReleaseKeyDrag()
        {
            if (!draggingKey)
                return;
            if (GUIUtility.hotControl == keyDragControl)
                GUIUtility.hotControl = 0;
            draggingKey = false;
            Undo.CollapseUndoOperations(keyUndoGroup);
        }

        [SerializeField]
        private float zoom = 48;

        [NonSerialized] private Vector2 timelineScroll;

        private float TrackHeaderWidth => Mathf.Clamp(timelineNameWidth, 160, Mathf.Max(160, Window.position.width - host.Inspector - 240));


        [NonSerialized] private StudioClipDrag clipDrag;


        internal void RevealTimelineSelection(TimelineClip clip)
        {
            {
                collapsedTimelineGroups &= ~(1 << StudioTimelineLayout.Group(host.Selected));
                collapsedKeyClips.Remove(host.Selected);
                var row = StudioTimelineLayout.Build(host.Sequence, collapsedTimelineGroups).FirstOrDefault(ContainsSelectedClip);
                float visibleHeight = host.TimelineHeight - TimelineToolbarHeight - 44;
                if (keyframeMode)
                {
                    var keyRow = BuildKeyRows().FirstOrDefault(IsSelectedKeyRow);
                    if (keyRow != null)
                        timelineScroll.y = Mathf.Max(0, keyRow.Y - 28);
                    bool IsSelectedKeyRow(KeyRow r)
                    {
                        return r.Clip == clip;
                    }
                }
                else if (row != null && (row.Y < timelineScroll.y + 28 || row.Y + row.Height > timelineScroll.y + visibleHeight))
                    timelineScroll.y = Mathf.Max(0, row.Y - 28);
                float x = (float)clip.start * zoom;
                if (x < timelineScroll.x || x > timelineScroll.x + Window.position.width - host.Inspector - TrackHeaderWidth - 80)
                    timelineScroll.x = Mathf.Max(0, x - 35);
                bool ContainsSelectedClip(StudioTimelineRow r)
                {
                    return !r.Header && r.Clips.Contains(clip);
                }
            }

        }

        [SerializeField]
        private int collapsedTimelineGroups;

        [SerializeField]
        private string timelineSearch = "";

        [NonSerialized] private int timelineDragControl;

        [NonSerialized] private int timelineScrubControl;

        [NonSerialized] private bool scrubbingTimeline;

        [SerializeField]
        private float timelineNameWidth = 250;

        [NonSerialized] private int timelineResizeControl;

        [NonSerialized] private bool resizingTimelineNames;

        [NonSerialized] private StudioTrack resizingTimelineTrack;

        [NonSerialized] private Vector2 timelineResizeStart;

        [NonSerialized] private float timelineResizeSize;

        [NonSerialized] private Vector2 timelinePointer;

        [NonSerialized] private bool timelinePointerInside;

        internal float TimelineToolbarHeight => Window.position.width - host.Inspector - 1 < 900 ? 92 : 64;


        internal void UpdateTimelinePointer()
        {
            // 스크롤과 컨트롤이 마우스 이벤트를 사용하기 전에 창 좌표를 저장함.
            var e = Event.current;
            switch (e.rawType)
            {
                case EventType.MouseLeaveWindow:
                    timelinePointerInside = false;
                    Window.Repaint();
                    break;
                case EventType.MouseEnterWindow:
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.ScrollWheel:
                    timelinePointer = e.mousePosition;
                    timelinePointerInside = true;
                    Window.Repaint();
                    break;
            }
        }


        internal void DrawTimeline(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(.16f, .16f, .16f));
            StudioGUI.DrawPanelHeader(area, "시퀀스 타임라인 / " + StudioTiming.FrameRate(host.Sequence).ToString("0.##") + " FPS");
            EditorGUI.DrawRect(new Rect(area.x, area.yMax - 27, area.width, 27), new Color(.20f, .20f, .20f));
            if (host.Sequence == null || host.Sequence.Timeline == null)
                return;
            StudioTrack deleteTrack = null;
            if (!host.DialogueMode)
            {
                float foldWidth = Mathf.Max(72, EditorStyles.miniButton.CalcSize(new GUIContent("트랙 모두 접기")).x + 16);
                float expandWidth = Mathf.Max(88, EditorStyles.miniButton.CalcSize(new GUIContent("트랙 모두 펼치기")).x + 16);
                if (GUI.Button(new Rect(area.x + 220, area.y + 7, foldWidth, 22), "트랙 모두 접기", EditorStyles.miniButton))
                    collapsedTimelineGroups = (1 << StudioClipSchema.Groups.Count) - 1;
                if (GUI.Button(new Rect(area.x + 224 + foldWidth, area.y + 7, expandWidth, 22), "트랙 모두 펼치기", EditorStyles.miniButton))
                {
                    collapsedTimelineGroups = 0;
                    collapsedKeyClips.Clear();
                }

                if (GUI.Button(new Rect(area.x + 228 + foldWidth + expandWidth, area.y + 7, 116, 22), "+ 새 트랙 추가", EditorStyles.miniButton))
                    StudioAddTrackWindow.Open(Window, host.Sequence);
            }

            float controlsY = area.y;
            if (TimelineToolbarHeight > 64)
            {
                EditorGUI.DrawRect(new Rect(area.x, area.y + 36, area.width, 28), new Color(.20f, .20f, .20f));
                controlsY += 28;
            }

            host.SnapEnabled = GUI.Toggle(new Rect(area.xMax - 270, controlsY + 7, 105, 22), host.SnapEnabled, "프레임 스냅");
            zoom = GUI.HorizontalSlider(new Rect(area.xMax - 150, controlsY + 13, 130, 20), zoom, 20, 160);
            DrawTimelineTabs(area);
            if (host.DialogueMode)
            {
                DrawTimelineFooter(area);
                return;
            }

            if (keyframeMode)
            {
                DrawKeyframeTimeline(area);
                return;
            }

            var rows = StudioTimelineLayout.Build(host.Sequence, collapsedTimelineGroups, timelineSearch);
            var viewport = new Rect(area.x, area.y + TimelineToolbarHeight, area.width, area.height - TimelineToolbarHeight - 27);
            HandleTimelineResize(viewport, rows);
            float bottom = rows.Length == 0 ? StudioTimelineLayout.RulerHeight : rows.Last().Y + rows.Last().Height;
            var content = new Rect(0, 0, Mathf.Max(viewport.width - 18, TrackHeaderWidth + (float)host.Sequence.Duration * zoom + 60), Mathf.Max(viewport.height - 18, bottom + 40));
            var e = Event.current;
            bool inBody = viewport.Contains(e.mousePosition) && e.mousePosition.y >= viewport.y + StudioTimelineLayout.RulerHeight;
            bool inClips = inBody && e.mousePosition.x >= viewport.x + TrackHeaderWidth && e.mousePosition.x < viewport.xMax - 16;
            int dragControl = GUIUtility.GetControlID("StudioClipDrag".GetHashCode(), FocusType.Passive);
            timelineScroll = GUI.BeginScrollView(viewport, timelineScroll, content);
            foreach (var row in rows)
            {
                EditorGUI.DrawRect(new Rect(0, row.Y, content.width, row.Height), row.Header ? new Color(.22f, .22f, .22f) : new Color(.17f, .17f, .17f));
                EditorGUI.DrawRect(new Rect(0, row.Y, content.width, 1), new Color(.10f, .10f, .10f));
                if (row.Track != null)
                    EditorGUI.DrawRect(new Rect(0, row.Y + row.Height - 1, content.width, 1), new Color(.10f, .10f, .10f));
            }

            int firstSecond = Mathf.Max(0, Mathf.FloorToInt(timelineScroll.x / zoom));
            int lastSecond = Mathf.Min(Mathf.CeilToInt((float)host.Sequence.Duration), Mathf.CeilToInt((timelineScroll.x + viewport.width - TrackHeaderWidth) / zoom));
            for (int second = firstSecond; second <= lastSecond; second++)
            {
                float x = TrackHeaderWidth + second * zoom;
                EditorGUI.DrawRect(new Rect(x, 28, 1, content.height - 28), new Color(.24f, .24f, .24f));
            }

            foreach (var row in rows)
            {
                if (row.Y + row.Height < timelineScroll.y + 28 || row.Y > timelineScroll.y + viewport.height)
                    continue;
                if (row.Header)
                {
                    foreach (var clip in row.Clips)
                    {
                        var color = StudioGUI.ClipColor(((StudioClip)clip.asset).Kind);
                        color.a = .65f;
                        EditorGUI.DrawRect(new Rect(TrackHeaderWidth + (float)clip.start * zoom, row.Y + 11, Mathf.Max(3, (float)clip.duration * zoom), 5), color);
                    }
                }
                else
                    foreach (var clip in row.Clips)
                    {
                        var asset = (StudioClip)clip.asset;
                        var rect = new Rect(TrackHeaderWidth + (float)clip.start * zoom, row.Y + 6, Mathf.Max(10, (float)clip.duration * zoom), row.Height - 12);
                        if (rect.xMax < timelineScroll.x + TrackHeaderWidth || rect.x > timelineScroll.x + viewport.width)
                            continue;
                        DrawTimelineClip(rect, clip, asset, row.Track.muted);
                        if (inClips)
                        {
                            EditorGUIUtility.AddCursorRect(new Rect(rect.x, rect.y, 7, rect.height), MouseCursor.ResizeHorizontal);
                            EditorGUIUtility.AddCursorRect(new Rect(rect.xMax - 7, rect.y, 7, rect.height), MouseCursor.ResizeHorizontal);
                        }

                        if (inClips && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                        {
                            host.Select(clip, false);
                            int edge = e.mousePosition.x >= rect.xMax - 7 ? 1 : e.mousePosition.x < rect.x + 7 ? -1 : 0;
                            clipDrag = new StudioClipDrag(host.Sequence, clip, edge);
                            timelineDragControl = dragControl;
                            GUIUtility.hotControl = timelineDragControl;
                            e.Use();
                        }
                    }
            }

            foreach (var row in rows)
                if (row.Track != null && row.Clips.Length == 0)
                    GUI.Label(new Rect(timelineScroll.x + TrackHeaderWidth + 12, row.Y + 12, 260, 22), "같은 유형의 클립을 여기에 놓으세요", EditorStyles.miniLabel);
            float head = TrackHeaderWidth + (float)host.Clock.Time * zoom;
            EditorGUI.DrawRect(new Rect(head, 28, 2, content.height - 28), StudioGUI.Accent);
            GUI.Label(new Rect(timelineScroll.x + TrackHeaderWidth + 12, bottom, viewport.width - TrackHeaderWidth - 36, 35), rows.Length == 0 && !string.IsNullOrWhiteSpace(timelineSearch) ? "검색 결과가 없습니다." : "＋ 여기에 놓아 새 트랙 추가", EditorStyles.centeredGreyMiniLabel);
            // 이름 열을 마지막에 그려 가로 스크롤 중에도 고정함.
            EditorGUI.DrawRect(new Rect(timelineScroll.x, 0, TrackHeaderWidth, content.height), new Color(.19f, .19f, .19f));
            foreach (var row in rows)
            {
                if (row.Y + row.Height < timelineScroll.y + 28 || row.Y > timelineScroll.y + viewport.height)
                    continue;
                var nameRect = new Rect(timelineScroll.x, row.Y, TrackHeaderWidth, row.Height);
                var color = StudioClipSchema.Groups[row.Group].Color;
                EditorGUI.DrawRect(nameRect, row.Header ? new Color(.24f, .24f, .24f) : new Color(.19f, .19f, .19f));
                EditorGUI.DrawRect(new Rect(nameRect.x, row.Y, TrackHeaderWidth, 1), new Color(.10f, .10f, .10f));
                if (row.Header)
                {
                    EditorGUI.DrawRect(new Rect(nameRect.x, row.Y + 1, 3, row.Height - 2), color);
                    bool searching = !string.IsNullOrWhiteSpace(timelineSearch);
                    bool open = searching || (collapsedTimelineGroups & (1 << row.Group)) == 0;
                    using (new EditorGUI.DisabledScope(searching || !inBody && e.type != EventType.Repaint && e.type != EventType.Layout))
                    {
                        bool next = EditorGUI.Foldout(new Rect(nameRect.x + 12, row.Y + 5, TrackHeaderWidth - 48, 20), open, StudioClipSchema.Groups[row.Group].GroupLabel, true, EditorStyles.foldout);
                        if (next != open)
                        {
                            collapsedTimelineGroups ^= 1 << row.Group;
                            Window.Repaint();
                        }
                    }

                    GUI.Label(new Rect(nameRect.xMax - 32, row.Y + 5, 28, 20), row.Clips.Length.ToString(), EditorStyles.miniLabel);
                }
                else if (row.Track != null)
                {
                    float nameY = row.Y + (row.Height - 22) * .5f;
                    using (new EditorGUI.DisabledScope(!inBody && e.type != EventType.Repaint && e.type != EventType.Layout))
                    {
                        if (GUI.Button(new Rect(nameRect.x + 22, nameY, TrackHeaderWidth - 90, 22), new GUIContent(TimelineTrackName(row), "클릭하여 트랙 이름 변경"), EditorStyles.label))
                            StudioTrackSettingsWindow.Open(Window, host.Sequence, row.Track, TimelineTrackName(row));
                        bool mute = GUI.Toggle(new Rect(nameRect.xMax - 59, nameY, 25, 22), row.Track.muted, "M", EditorStyles.miniButton);
                        if (mute != row.Track.muted)
                        {
                            Undo.RecordObject(row.Track, "트랙 음소거");
                            row.Track.muted = mute;
                            host.Changed();
                        }

                        bool empty = !row.Track.GetClips().Any() && !row.Track.GetChildTracks().Any();
                        using (new EditorGUI.DisabledScope(!empty))
                            if (GUI.Button(new Rect(nameRect.xMax - 31, nameY, 25, 22), new GUIContent("-", empty ? "빈 트랙 삭제" : "클립이 없는 트랙만 삭제할 수 있습니다."), EditorStyles.miniButton))
                                deleteTrack = row.Track;
                    }
                }
                else
                    GUI.Label(new Rect(nameRect.x + 22, row.Y + 4, TrackHeaderWidth - 28, 22), "클립 없음", EditorStyles.miniLabel);
            }

            EditorGUI.DrawRect(new Rect(timelineScroll.x + TrackHeaderWidth - 1, 0, 1, content.height), new Color(.10f, .10f, .10f));
            GUI.EndScrollView();
            DrawTimelineRuler(viewport, firstSecond, lastSecond);
            EditorGUI.DrawRect(new Rect(viewport.x + TrackHeaderWidth - 2, viewport.y, 3, viewport.height - 16), resizingTimelineNames ? StudioGUI.Accent : new Color(.12f, .12f, .12f));
            DrawTimelineResizeHover(viewport, rows);
            HandleTimelineDrop(viewport, rows);
            HandleClipDrag(e);

            DrawTimelineFooter(area);
            if (deleteTrack != null && StudioEditorAssets.DeleteEmptyTrack(host.Sequence, deleteTrack))
                host.Changed(true);
        }


        private void HandleClipDrag(Event e)
        {
            if (clipDrag == null)
                return;
            if (GUIUtility.hotControl != timelineDragControl)
            {
                ReleaseClipDrag();
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                if (clipDrag.Move(e.delta.x, zoom, host.Snap))
                    host.Changed();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                ReleaseClipDrag();
                e.Use();
            }
        }


        internal void ReleaseClipDrag()
        {
            if (clipDrag == null)
                return;
            var operation = clipDrag;
            clipDrag = null;
            if (GUIUtility.hotControl == timelineDragControl)
                GUIUtility.hotControl = 0;
            if (operation.Complete())
                host.Changed();
        }


        private void DrawTimelineFooter(Rect area)
        {
            GUI.Label(new Rect(area.x + 10, area.yMax - 24, area.width - 150, 22), host.Status, EditorStyles.miniLabel);
            if (GUI.Button(new Rect(area.xMax - 120, area.yMax - 25, 110, 22), "검사 " + host.Sequence.Validate().Count))
                StudioSequenceCheckWindow.Open(Window, host.Sequence);
        }


        private void DrawTimelineRuler(Rect viewport, int first, int last)
        {
            var ruler = new Rect(viewport.x, viewport.y, viewport.width - 16, 28);
            EditorGUI.DrawRect(ruler, new Color(.20f, .20f, .20f));
            GUI.Label(new Rect(ruler.x + 12, ruler.y + 4, TrackHeaderWidth - 20, 20), (keyframeMode ? "속성 / " : "트랙 / ") + StudioTiming.FrameRate(host.Sequence).ToString("0.##") + " FPS", EditorStyles.miniLabel);
            var timeArea = new Rect(ruler.x + TrackHeaderWidth, ruler.y, Mathf.Max(1, ruler.width - TrackHeaderWidth), 28);
            GUI.BeginGroup(timeArea);
            for (int second = first; second <= last; second++)
            {
                float x = second * zoom - timelineScroll.x;
                GUI.Label(new Rect(x + 3, 2, 55, 20), keyframeMode ? Math.Round(second * StudioTiming.FrameRate(host.Sequence)) + "f" : second + "s", EditorStyles.miniLabel);
                EditorGUI.DrawRect(new Rect(x, 19, 1, 9), new Color(.4f, .4f, .4f));
            }

            EditorGUI.DrawRect(new Rect((float)host.Clock.Time * zoom - timelineScroll.x, 0, 2, 28), StudioGUI.Accent);
            GUI.EndGroup();
            EditorGUI.DrawRect(new Rect(ruler.x, ruler.yMax - 1, ruler.width, 1), new Color(.10f, .10f, .10f));
            var e = Event.current;
            int control = GUIUtility.GetControlID("StudioTimelineScrub".GetHashCode(), FocusType.Passive);
            EditorGUIUtility.AddCursorRect(timeArea, MouseCursor.ResizeHorizontal, control);
            if (scrubbingTimeline && GUIUtility.hotControl != timelineScrubControl)
                ReleaseTimelineScrub();
            if (e.type == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl == 0 && timeArea.Contains(e.mousePosition))
            {
                host.ClearClipSelection();
                scrubbingTimeline = true;
                timelineScrubControl = control;
                GUIUtility.hotControl = control;
                host.Seek(host.Snap((e.mousePosition.x - timeArea.x + timelineScroll.x) / zoom));
                e.Use();
            }
            else if (scrubbingTimeline && GUIUtility.hotControl == timelineScrubControl)
            {
                // 눈금 밖으로 나가도 버튼을 놓을 때까지 드래그를 유지하며 시각은 시퀀스 범위로 제한함.
                if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp && e.button == 0)
                {
                    host.Seek(host.Snap((e.mousePosition.x - timeArea.x + timelineScroll.x) / zoom));
                    if (e.type == EventType.MouseUp)
                        ReleaseTimelineScrub();
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    ReleaseTimelineScrub();
                    e.Use();
                }
            }
        }


        internal void ReleaseTimelineScrub()
        {
            if (!scrubbingTimeline)
                return;
            if (GUIUtility.hotControl == timelineScrubControl)
                GUIUtility.hotControl = 0;
            scrubbingTimeline = false;
        }


        private string TimelineTrackName(StudioTimelineRow row)
        {
            if (!string.IsNullOrEmpty(row.Track.StudioDisplayName))
                return row.Track.StudioDisplayName;
            if (row.Clips.Length == 0)
                return row.Track.name;
            var data = (StudioClip)row.Clips[0].asset;
            if (row.Group == 0)
                return row.Track.name == "카메라" ? "메인 카메라" : row.Track.name;
            if (row.Group == 5)
                return row.Track.name;
            var actor = host.Sequence.EditableActors.Find(MatchesActorsFind);
            if (actor != null)
                return actor.Name + " / " + row.Track.name;
            return data.Kind == StudioKind.Audio && data.Sound != null ? data.Sound.name : row.Track.name;
            bool MatchesActorsFind(StudioActor a)
            {
                return a.Id == data.ActorId;
            }
        }


        private void DrawTimelineClip(Rect rect, TimelineClip clip, StudioClip data, bool muted)
        {
            var color = StudioGUI.ClipColor(data.Kind);
            EditorGUI.DrawRect(rect, data == host.Selected ? new Color(.17f, .36f, .53f) : new Color(.25f, .25f, .25f));
            if (muted)
                color.a = .35f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            GUI.BeginGroup(rect);
            float textX = 8;
            var actor = StudioTimelineLayout.Group(data) == 1 ? host.Sequence.EditableActors.Find(MatchesActorsFind2) : null;
            if (rect.width >= 52 && (actor != null || rect.width >= 105))
            {
                float size = Mathf.Min(rect.height - 8, Mathf.Min(80, rect.width * .4f));
                var thumbRect = new Rect(7, 4, size, rect.height - 8);
                EditorGUI.DrawRect(thumbRect, new Color(.13f, .13f, .13f));
                DrawClipThumbnail(thumbRect, data);
                textX = size + 12;
            }

            GUI.Label(new Rect(textX, 3, Mathf.Max(0, rect.width - textX - 4), 21), clip.displayName, EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(textX, 23, Mathf.Max(0, rect.width - textX - 4), 18), clip.duration.ToString("F2") + "s" + (data.WaitForInput ? " / E" : ""), EditorStyles.miniLabel);
            GUI.EndGroup();
            bool MatchesActorsFind2(StudioActor a)
            {
                return a.Id == data.ActorId;
            }
        }


        // 씬 오브젝트 카드와 타임라인 클립에서 같은 썸네일 원본을 사용함.
        private void DrawClipThumbnail(Rect rect, StudioClip data)
        {
            var appearance = StudioClipAppearance.Resolve(host.Sequence, data);
            var actor = appearance.Actor;
            var sprite = appearance.Sprite;
            if (sprite != null)
            {
                var tint = GUI.color;
                GUI.color = actor != null ? actor.Color : Color.white;
                DrawTimelineSprite(rect, sprite);
                GUI.color = tint;
            }
            else if (actor != null)
            {
                var dimensions = new Vector2(Mathf.Max(1, actor.Size.x), Mathf.Max(1, actor.Size.y));
                float fit = Mathf.Min((rect.width - 8) / dimensions.x, (rect.height - 8) / dimensions.y);
                EditorGUI.DrawRect(new Rect(rect.center - dimensions * fit * .5f, dimensions * fit), actor.Color);
            }
            else
                GUI.Label(rect, new GUIContent(data.Kind == StudioKind.Audio ? "♫" : StudioKeyframes.IsCamera(data) ? "▣" : "◇", StudioEvaluation.OwnsActor(data) ? "연결된 이미지 없음" : data.Kind.ToString()), EditorStyles.centeredGreyMiniLabel);
        }


        private static void DrawTimelineSprite(Rect rect, Sprite sprite)
        {
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            float scale = Mathf.Min((rect.width - 4) / sprite.rect.width, (rect.height - 4) / sprite.rect.height);
            var size = sprite.rect.size * scale;
            var fit = new Rect(rect.center - size * .5f, size);
            GUI.DrawTextureWithTexCoords(fit, sprite.texture, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
        }


        internal void RenameTimelineTrack(StudioSequence owner, StudioTrack track, string value)
        {
            if (owner == null || track == null || string.IsNullOrWhiteSpace(value))
                return;
            Undo.RecordObject(track, "트랙 이름 변경");
            track.name = track.StudioDisplayName = value.Trim();
            EditorUtility.SetDirty(track);
            if (host.Sequence == owner)
                host.Changed();
            else
                StudioEditorAssets.Save(owner);
        }


        internal void AddTimelineTrack(StudioSequence owner, StudioKind kind)
        {
            if (owner == null || owner.Timeline == null)
                return;
            var track = StudioEditorAssets.AddTrack(owner, kind);
            if (host.Sequence != owner)
            {
                StudioEditorAssets.Save(owner);
                return;
            }

            timelineSearch = "";
            timelineSearchUI?.SetValueWithoutNotify("");
            collapsedTimelineGroups &= ~(1 << StudioTimelineLayout.Group(kind));
            var row = StudioTimelineLayout.Build(host.Sequence, collapsedTimelineGroups).First(HandleCollapsedtimelinegroupsFirst);
            timelineScroll.y = Mathf.Max(0, row.Y - StudioTimelineLayout.RulerHeight);
            host.Changed(true);
            bool HandleCollapsedtimelinegroupsFirst(StudioTimelineRow r)
            {
                return r.Track == track;
            }
        }


        private void DrawTimelineResizeHover(Rect viewport, StudioTimelineRow[] rows)
        {
            if (resizingTimelineNames)
                return;
            foreach (var row in rows.Where(MatchesRowsWhere2))
            {
                float y = viewport.y + row.Y + row.Height - timelineScroll.y;
                if (y < viewport.y + 32 || y > viewport.yMax - 19)
                    continue;
                var edge = new Rect(viewport.x, y - 3, viewport.width - 16, 6);
                bool hovered = timelinePointerInside && edge.Contains(timelinePointer) && Mathf.Abs(timelinePointer.x - viewport.x - TrackHeaderWidth) > 4;
                if ((hovered && GUIUtility.hotControl == 0) || resizingTimelineTrack == row.Track)
                    EditorGUI.DrawRect(new Rect(edge.x, y - 1, edge.width, 3), new Color(.78f, .76f, .50f, 1f));
            }

            bool MatchesRowsWhere2(StudioTimelineRow r)
            {
                return r.Track != null;
            }
        }


        private void HandleTimelineResize(Rect viewport, StudioTimelineRow[] rows)
        {
            int control = GUIUtility.GetControlID("StudioTimelineResize".GetHashCode(), FocusType.Passive);
            var e = Event.current;
            var divider = new Rect(viewport.x + TrackHeaderWidth - 4, viewport.y, 8, viewport.height - 16);
            EditorGUIUtility.AddCursorRect(divider, MouseCursor.ResizeHorizontal);
            StudioTrack hit = null;
            foreach (var row in rows.Where(MatchesRowsWhere3))
            {
                float y = viewport.y + row.Y + row.Height - timelineScroll.y;
                if (y < viewport.y + 32 || y > viewport.yMax - 19)
                    continue;
                var edge = new Rect(viewport.x, y - 3, viewport.width - 16, 6);
                EditorGUIUtility.AddCursorRect(edge, MouseCursor.ResizeVertical);
                if (edge.Contains(e.mousePosition))
                    hit = row.Track;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl == 0 && (divider.Contains(e.mousePosition) || hit != null))
            {
                resizingTimelineNames = divider.Contains(e.mousePosition);
                resizingTimelineTrack = resizingTimelineNames ? null : hit;
                timelineResizeSize = resizingTimelineNames ? TrackHeaderWidth : Mathf.Clamp(hit.StudioRowHeight, 56, 240);
                timelineResizeStart = e.mousePosition;
                if (resizingTimelineTrack != null)
                    Undo.RegisterCompleteObjectUndo(resizingTimelineTrack, "트랙 높이 변경");
                timelineResizeControl = control;
                GUIUtility.hotControl = control;
                GUIUtility.keyboardControl = 0;
                e.Use();
            }
            else if (GUIUtility.hotControl == timelineResizeControl && (resizingTimelineNames || resizingTimelineTrack != null))
            {
                if (e.type == EventType.MouseDrag)
                {
                    if (resizingTimelineNames)
                        timelineNameWidth = Mathf.Clamp(timelineResizeSize + e.mousePosition.x - timelineResizeStart.x, 160, Mathf.Max(160, viewport.width - 240));
                    else
                        resizingTimelineTrack.StudioRowHeight = Mathf.Clamp(timelineResizeSize + e.mousePosition.y - timelineResizeStart.y, 56, 240);
                    e.Use();
                    Window.Repaint();
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    ReleaseTimelineResize();
                    e.Use();
                }
            }

            bool MatchesRowsWhere3(StudioTimelineRow r)
            {
                return r.Track != null;
            }
        }


        internal void ReleaseTimelineResize()
        {
            if (!resizingTimelineNames && resizingTimelineTrack == null)
                return;
            if (GUIUtility.hotControl == timelineResizeControl)
                GUIUtility.hotControl = 0;
            if (resizingTimelineNames)
                EditorPrefs.SetFloat(StudioPanelLayout.LayoutKey + "TrackNames", timelineNameWidth);
            if (resizingTimelineTrack != null)
            {
                EditorUtility.SetDirty(resizingTimelineTrack);
                host.Changed();
            }

            resizingTimelineNames = false;
            resizingTimelineTrack = null;
            Window.Repaint();
        }

        internal bool KeyframeMode { get { return keyframeMode; } set { keyframeMode = value; } }
        internal HashSet<StudioClip> CollapsedKeyClips { get { return collapsedKeyClips; } }
        internal bool DraggingKey { get { return draggingKey; } set { draggingKey = value; } }
        internal Vector2 TimelineScroll { get { return timelineScroll; } set { timelineScroll = value; } }
        internal StudioClipDrag ClipDrag { get { return clipDrag; } set { clipDrag = value; } }
        internal string TimelineSearch { get { return timelineSearch; } set { timelineSearch = value; } }
        internal float TimelineNameWidth { get { return timelineNameWidth; } set { timelineNameWidth = value; } }
        [NonSerialized] private ToolbarSearchField timelineSearchUI;

        internal VisualElement CreateSearch()
        {
            timelineSearchUI = new ToolbarSearchField
            {
                name = "timeline-search",
                value = timelineSearch,
                tooltip = "트랙 / 클립 검색"
            };
            timelineSearchUI.style.position = Position.Absolute;
            timelineSearchUI.style.marginLeft = timelineSearchUI.style.marginRight = 0;
            timelineSearchUI.style.marginTop = timelineSearchUI.style.marginBottom = 0;
            timelineSearchUI.Q<TextField>().textEdition.placeholder = "트랙 / 클립 검색";
            // 겹쳐진 검색창의 포인터 이벤트가 IMGUI까지 전달되지 않도록 함.
            timelineSearchUI.RegisterCallback<MouseDownEvent>(StopSearchMouseDown);
            timelineSearchUI.RegisterCallback<MouseUpEvent>(StopSearchMouseUp);
            timelineSearchUI.RegisterValueChangedCallback(SearchTimeline);
            return timelineSearchUI;
        }

        internal void PositionSearch()
        {
            if (timelineSearchUI != null)
            {
                float areaWidth = Window.position.width - host.Inspector - 1;
                float width = Mathf.Min(300, Mathf.Max(80, areaWidth - 368));
                timelineSearchUI.style.left = Mathf.Clamp(areaWidth / 2 - width / 2, 218, areaWidth - 140 - width);
                timelineSearchUI.style.top = Window.position.height - host.TimelineHeight + TimelineToolbarHeight - 23;
                timelineSearchUI.style.width = width;
                timelineSearchUI.style.height = 20;
                timelineSearchUI.style.display = !EditorApplication.isPlaying && host.Sequence?.Timeline != null && !keyframeMode && !host.DialogueMode ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        internal bool ContainsSearch(Vector2 position)
        {
            return timelineSearchUI != null && timelineSearchUI.resolvedStyle.display != DisplayStyle.None && timelineSearchUI.worldBound.Contains(position);
        }

        internal void SetEditingEnabled(bool enabled) { timelineSearchUI?.SetEnabled(enabled); }

        private static void StopSearchMouseDown(MouseDownEvent e)
        {
            e.StopPropagation();
        }

        private static void StopSearchMouseUp(MouseUpEvent e)
        {
            e.StopPropagation();
        }

        private void SearchTimeline(ChangeEvent<string> e)
        {
            timelineSearch = e.newValue;
            timelineScroll = new Vector2(timelineScroll.x, 0);
            Window.Repaint();
        }
    }
}
