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
    internal sealed class StudioPreviewPanel
    {
        [NonSerialized] private IStudioPreviewHost host;
        private CutsceneStudioWindow Window => host.Window;
        internal void Initialize(IStudioPreviewHost value) { host = value; }
        [SerializeField]
        private int previewTool;

        [SerializeField]
        private bool previewRecordKeys;

        [NonSerialized] private Vector2 previewToolbarScroll;

        private const float PreviewToolsWidth = 430;

        private const float PreviewToolbarMinWidth = 220 + 12 + PreviewToolsWidth;

        [NonSerialized] private bool previewToolFocus;

        [NonSerialized] private int previewDragAxis = -1, previewDragControl, previewUndoGroup;

        [NonSerialized] private Vector2 previewDragStart, previewDragPivot;

        [NonSerialized] private float previewUnits, previewKeyTime, previewLastAngle, previewAngle;

        [NonSerialized] private TimelineClip previewDragClip;

        [NonSerialized] private readonly StudioClipSnapshot previewBefore = new();

        private void DrawPreviewTools(Rect area)
        {
            string[] labels =
            {
                "이동(W)",
                "회전(E)",
                "크기(R)"
            };
            for (int i = 0; i < labels.Length; i++)
                if (GUI.Toggle(new Rect(area.x + i * 72, area.y, 70, 22), previewTool == i, labels[i], EditorStyles.miniButton))
                    previewTool = i;
            string recordLabel = previewRecordKeys ? "키 기록 (현재 시간에 키 저장)" : "키 기록 (클립 전체 변형)";
            previewRecordKeys = GUI.Toggle(new Rect(area.x + 224, area.y, area.width - 224, 22), previewRecordKeys, new GUIContent(recordLabel, "켜짐: 현재 시간에 키 저장 / 꺼짐: 클립 전체 변형"));
            if (Event.current.rawType == EventType.MouseDown && area.Contains(Event.current.mousePosition))
                previewToolFocus = true;
        }


        internal bool HandlePreviewToolKeys(Event e)
        {
            if (EditorWindow.focusedWindow != Window || e.type != EventType.KeyDown || EditorGUIUtility.editingTextField || host.NativeTextEditing())
                return false;
            if (e.keyCode == KeyCode.Escape && previewDragAxis >= 0)
            {
                EndPreviewTransform(true);
                host.ClearClipSelection();
                e.Use();
                return true;
            }

            if (!previewToolFocus || e.control || e.command || e.alt || previewDragAxis >= 0)
                return false;
            // 입력 대기 중 E는 회전 도구 전환보다 재생 진행에 우선 사용함.
            if (e.keyCode == KeyCode.E && host.Clock.Waiting.HasValue)
                return false;
            int tool = e.keyCode == KeyCode.W ? 0 : e.keyCode == KeyCode.E ? 1 : e.keyCode == KeyCode.R ? 2 : -1;
            if (tool < 0)
                return false;
            previewTool = tool;
            e.Use();
            Window.Repaint();
            return true;
        }


        private RectTransform PreviewTarget()
        {
            var clip = host.FindSelected();
            if (clip == null || host.Clock.DisplayTime < clip.start || host.Clock.DisplayTime > clip.end || host.Playing)
                return null;
            if (host.Selected.Kind == StudioKind.Background)
                return host.Preview.Stage.BackgroundOpacity > 0 ? host.Preview.Stage.BackgroundTransform : null;
            if (StudioEvaluation.OwnsActor(host.Selected) || host.Selected.Kind == StudioKind.State && !host.Selected.StateCamera)
                return host.Preview.Stage.ActorVisible(host.Selected.ActorId) ? host.Preview.Stage.ActorTransform(host.Selected.ActorId) : null;
            return null;
        }


        private void DrawPreviewTransform(Rect frame)
        {
            var e = Event.current;
            if (e.rawType == EventType.MouseDown && frame.Contains(e.mousePosition))
                previewToolFocus = true;
            var target = PreviewTarget();
            int control = GUIUtility.GetControlID("StudioPreviewTransform".GetHashCode(), FocusType.Passive);
            if (target == null && previewDragAxis < 0)
                return;
            Vector2 pivot = target == null ? previewDragPivot : (Vector2)host.Preview.Stage.Frame.InverseTransformPoint(target.position);
            float pixels = frame.width / StudioSequence.Width;
            pivot = target == null ? previewDragPivot : new Vector2(frame.center.x + pivot.x * pixels, frame.center.y - pivot.y * pixels);
            Vector2 xTip = pivot + new Vector2(58, 0), yTip = pivot + new Vector2(0, -58);
            int hit = -1;
            if (frame.Contains(e.mousePosition))
            {
                if (previewTool == 0)
                {
                    if (new Rect(pivot.x - 8, pivot.y - 8, 16, 16).Contains(e.mousePosition))
                        hit = 0;
                    else if (new Rect(pivot.x + 10, pivot.y - 7, 55, 14).Contains(e.mousePosition))
                        hit = 1;
                    else if (new Rect(pivot.x - 7, pivot.y - 65, 14, 55).Contains(e.mousePosition))
                        hit = 2;
                }
                else if (previewTool == 1 && Mathf.Abs(Vector2.Distance(e.mousePosition, pivot) - 48) <= 8)
                    hit = 0;
                else if (previewTool == 2 && (new Rect(xTip - new Vector2(8, 8), Vector2.one * 16).Contains(e.mousePosition) || new Rect(yTip - new Vector2(8, 8), Vector2.one * 16).Contains(e.mousePosition) || new Rect(pivot - Vector2.one * 9, Vector2.one * 18).Contains(e.mousePosition)))
                    hit = 0;
            }

            if (Event.current.type == EventType.Repaint && frame.Contains(pivot))
            {
                var old = Handles.color;
                Handles.BeginGUI();
                GUI.BeginGroup(frame);
                Vector2 p = pivot - frame.position, x = xTip - frame.position, y = yTip - frame.position;
                if (previewTool == 1)
                {
                    Handles.color = hit >= 0 || previewDragAxis >= 0 ? Color.yellow : new Color(.4f, .65f, 1);
                    Handles.DrawWireDisc(p, Vector3.forward, 48);
                }
                else
                {
                    Handles.color = hit == 1 ? Color.yellow : new Color(.95f, .35f, .3f);
                    Handles.DrawAAPolyLine(3, p, x);
                    if (previewTool == 0)
                        Handles.DrawAAConvexPolygon(x, x + new Vector2(-10, -5), x + new Vector2(-10, 5));
                    else
                        EditorGUI.DrawRect(new Rect(x - Vector2.one * 5, Vector2.one * 10), Handles.color);
                    Handles.color = hit == 2 ? Color.yellow : new Color(.4f, .85f, .35f);
                    Handles.DrawAAPolyLine(3, p, y);
                    if (previewTool == 0)
                        Handles.DrawAAConvexPolygon(y, y + new Vector2(-5, 10), y + new Vector2(5, 10));
                    else
                        EditorGUI.DrawRect(new Rect(y - Vector2.one * 5, Vector2.one * 10), Handles.color);
                    EditorGUI.DrawRect(new Rect(p - Vector2.one * 5, Vector2.one * 10), hit == 0 ? Color.yellow : Color.white);
                }

                GUI.EndGroup();
                Handles.EndGUI();
                Handles.color = old;
            }

            if (hit >= 0)
                EditorGUIUtility.AddCursorRect(new Rect(e.mousePosition - Vector2.one * 5, Vector2.one * 10), MouseCursor.MoveArrow);
            if (e.type == EventType.MouseDown && e.button == 0 && hit >= 0)
            {
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
                previewDragAxis = hit;
                previewDragControl = control;
                GUIUtility.hotControl = control;
                previewDragClip = host.FindSelected();
                previewDragStart = e.mousePosition;
                previewDragPivot = pivot;
                previewUnits = 1 / Mathf.Max(.001f, pixels * host.Preview.Stage.CameraZoom);
                previewKeyTime = Mathf.Clamp((float)(host.Clock.DisplayTime - previewDragClip.start), 0, (float)previewDragClip.duration);
                if (host.SnapEnabled)
                    previewKeyTime = Mathf.Clamp((float)StudioTiming.Snap(host.Sequence, previewKeyTime), 0, (float)previewDragClip.duration);
                previewBefore.Capture(host.Selected);
                previewLastAngle = Mathf.Atan2(-(e.mousePosition.y - pivot.y), e.mousePosition.x - pivot.x) * Mathf.Rad2Deg;
                previewAngle = 0;
                Undo.IncrementCurrentGroup();
                previewUndoGroup = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(host.Selected, "프리뷰 오브젝트 변형");
                e.Use();
            }

            if (previewDragAxis >= 0 && e.type == EventType.MouseDrag && GUIUtility.hotControl == previewDragControl)
            {
                previewBefore.Restore((StudioClip)previewDragClip.asset);
                Vector2 delta = e.mousePosition - previewDragStart;
                if (previewTool == 0)
                {
                    if (previewDragAxis != 2)
                        ApplyPreviewProperty(previewDragClip, StudioProperty.PositionX, previewKeyTime, delta.x * previewUnits, false, previewRecordKeys);
                    if (previewDragAxis != 1)
                        ApplyPreviewProperty(previewDragClip, StudioProperty.PositionY, previewKeyTime, -delta.y * previewUnits, false, previewRecordKeys);
                }
                else if (previewTool == 1)
                {
                    float angle = Mathf.Atan2(-(e.mousePosition.y - previewDragPivot.y), e.mousePosition.x - previewDragPivot.x) * Mathf.Rad2Deg;
                    previewAngle += Mathf.DeltaAngle(previewLastAngle, angle);
                    previewLastAngle = angle;
                    ApplyPreviewProperty(previewDragClip, StudioProperty.Rotation, previewKeyTime, e.shift ? Mathf.Round(previewAngle / 15) * 15 : previewAngle, false, previewRecordKeys);
                }
                else
                    ApplyPreviewProperty(previewDragClip, StudioProperty.Scale, previewKeyTime, Mathf.Exp(Mathf.Clamp((delta.x - delta.y) / 100, -7, 7)), true, previewRecordKeys);
                host.Changed();
                e.Use();
            }

            if (previewDragAxis >= 0 && e.type == EventType.MouseUp && e.button == 0)
            {
                EndPreviewTransform();
                e.Use();
            }
        }


        internal static void ApplyPreviewProperty(TimelineClip clip, StudioProperty property, float at, float delta, bool multiply, bool recordKey)
        {
            var data = (StudioClip)clip.asset;
            float Change(float value) => StudioKeyframes.Limit(property, multiply ? value * delta : value + delta);
            if (recordKey)
            {
                float value = StudioKeyframes.Evaluate(data, property, at, clip.duration);
                StudioKeyframeEditing.Set(clip, property, -1, at, Change(value), false);
            }
            else
            {
                var keys = StudioKeyframes.GetKeys(data, property, clip.duration);
                if (keys.Length == 0)
                    StudioKeyframeEditing.Set(clip, property, -1, at, Change(StudioKeyframes.Evaluate(data, property, at, clip.duration)), false);
                else
                    for (int i = 0; i < keys.Length; i++)
                        StudioKeyframeEditing.Set(clip, property, i, keys[i].Time, Change(keys[i].Value), false);
            }

            EditorUtility.SetDirty(data);
        }


        internal void EndPreviewTransform(bool cancel = false)
        {
            if (previewDragAxis < 0)
                return;
            if (cancel && previewDragClip?.asset != null)
            {
                previewBefore.Restore((StudioClip)previewDragClip.asset);
                host.Changed();
            }

            if (GUIUtility.hotControl == previewDragControl)
                GUIUtility.hotControl = 0;
            Undo.CollapseUndoOperations(previewUndoGroup);
            previewDragAxis = -1;
            previewDragClip = null;
            previewBefore.Dispose();
        }


        private void DrawPreviewInteraction(Rect output)
        {
            var frame = StudioEvaluation.Fit(output);
            host.HandleLibraryDrop(frame, false);
            host.Preview.Stage.Evaluate(host.Clock.DisplayTime, host.Clock.Waiting.HasValue);
            DrawPreviewTransform(frame);
            foreach (var actor in host.Sequence.Actors.OrderByDescending(SelectActorsOrderByDescending))
            {
                if (!host.Preview.Stage.ActorVisible(actor.Id))
                    continue;
                var local = host.Preview.Stage.ActorFrameBounds(actor.Id);
                float scale = frame.width / StudioSequence.Width;
                var rect = new Rect(frame.center.x + local.x * scale, frame.center.y - local.yMax * scale, local.width * scale, local.height * scale);
                if (host.ActorIndex >= 0 && host.Sequence.Actors[host.ActorIndex].Id == actor.Id)
                {
                    var visible = Rect.MinMaxRect(Mathf.Max(rect.x, frame.x), Mathf.Max(rect.y, frame.y), Mathf.Min(rect.xMax, frame.xMax), Mathf.Min(rect.yMax, frame.yMax));
                    if (visible.width > 0 && visible.height > 0)
                    {
                        EditorGUI.DrawRect(new Rect(visible.x, visible.y, visible.width, 1), StudioGUI.Accent);
                        EditorGUI.DrawRect(new Rect(visible.x, visible.yMax, visible.width, 1), StudioGUI.Accent);
                        EditorGUI.DrawRect(new Rect(visible.x, visible.y, 1, visible.height), StudioGUI.Accent);
                        EditorGUI.DrawRect(new Rect(visible.xMax, visible.y, 1, visible.height), StudioGUI.Accent);
                    }
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && frame.Contains(Event.current.mousePosition) && rect.Contains(Event.current.mousePosition))
                {
                    double time = host.Clock.Time;
                    host.ChooseActor(host.Sequence.EditableActors.IndexOf(actor));
                    host.Seek(time);
                    previewToolFocus = true;
                    Event.current.Use();
                    break;
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && frame.Contains(Event.current.mousePosition) && host.Preview.Stage.BackgroundOpacity > 0)
            {
                var background = StudioEvaluation.Clips(host.Sequence).LastOrDefault(MatchesSequenceLastOrDefault);
                if (background.Clip != null)
                {
                    double time = host.Clock.Time;
                    host.Select(background.Clip);
                    host.Seek(time);
                    previewToolFocus = true;
                    Event.current.Use();
                }

                bool MatchesSequenceLastOrDefault(StudioEntry e)
                {
                    return e.Asset.Kind == StudioKind.Background && e.Contains(host.Clock.DisplayTime);
                }
            }

            int SelectActorsOrderByDescending(StudioActor a)
            {
                return a.Layer;
            }
        }

        [SerializeField]
        private int resolutionIndex;

        [SerializeField]
        private Vector2 customResolution = new(1920, 1080);

        private static readonly string[] Resolutions =
        {
            "1920×1080 / 16:9",
            "2560×1440 / 16:9",
            "3840×2160 / 16:9",
            "1920×1200 / 16:10",
            "3440×1440 / 울트라와이드",
            "직접 입력"
        };

        private static readonly Vector2[] Sizes =
        {
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160),
            new(1920, 1200),
            new(3440, 1440)
        };


        internal void DrawPreview(Rect area)
        {
            var body = StudioGUI.DrawPanelHeader(area, "미리보기");
            if (host.Sequence == null || host.Preview == null || host.PreviewRebuildPending)
                return;
            bool scrollTools = area.width - 20 < PreviewToolbarMinWidth;
            float toolbarHeight = scrollTools ? 40 : 22;
            var header = new Rect(body.x + 10, body.y + 8, body.width - 20, toolbarHeight);
            float toolbarWidth = Mathf.Max(header.width, PreviewToolbarMinWidth);
            // 도구와 해상도 선택을 한 줄에 배치하며 폭이 좁으면 도구 막대를 스크롤함.
            if (scrollTools)
                previewToolbarScroll = GUI.BeginScrollView(header, previewToolbarScroll, new Rect(0, 0, toolbarWidth, 22));
            else
            {
                previewToolbarScroll = Vector2.zero;
                GUI.BeginGroup(header);
            }

            DrawPreviewTools(new Rect(0, 0, PreviewToolsWidth, 22));
            StudioGUI.Popup(new Rect(toolbarWidth - 220, 0, 220, 22), resolutionIndex, Resolutions, Window, SelectResolution);
            if (scrollTools)
                GUI.EndScrollView();
            else
                GUI.EndGroup();
            var size = resolutionIndex < Sizes.Length ? Sizes[resolutionIndex] : customResolution;
            if (resolutionIndex == Sizes.Length)
            {
                DrawCustomPreviewSize(new Rect(header.x, header.y + toolbarHeight + 4, header.width, CustomPreviewSizeHeight(header.width)));
                size = customResolution;
            }

            var available = PreviewImageArea(area);
            float ratio = size.x / size.y;
            float width = Mathf.Min(available.width, available.height * ratio);
            float height = width / ratio;
            var output = new Rect(available.center.x - width / 2, available.center.y - height / 2, width, height);
            if (Event.current.type == EventType.Repaint && width > 0 && height > 0)
                GUI.DrawTexture(output, host.Preview.Draw(new Rect(0, 0, width, height), host.Clock.DisplayTime, host.Clock.Waiting.HasValue), ScaleMode.StretchToFill, false);
            DrawPreviewInteraction(output);
            DrawPreviewPlayback(area);
            void SelectResolution(int index)
            {
                resolutionIndex = index;
            }
        }


        private Rect PreviewImageArea(Rect area)
        {
            float top = 36 + 38 + (area.width - 20 < PreviewToolbarMinWidth ? 18 : 0) + (resolutionIndex == Sizes.Length ? CustomPreviewSizeHeight(area.width - 20) : 0);
            return new Rect(area.x + 12, area.y + top, Mathf.Max(0, area.width - 24), Mathf.Max(0, area.height - top - PreviewPlaybackHeight(area.width - 20) - 21));
        }

        [SerializeField]
        private Vector2Int customPreviewAspect = new(16, 9);

        private static float CustomPreviewSizeHeight(float width) => width < 520 ? 52 : 26;

        private void DrawCustomPreviewSize(Rect area)
        {
            var safeSize = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(customResolution.x), 1, 32768), Mathf.Clamp(Mathf.RoundToInt(customResolution.y), 1, 32768));
            if (customResolution != (Vector2)safeSize || customPreviewAspect.x <= 0 || customPreviewAspect.y <= 0 || Mathf.Abs(customResolution.y - customResolution.x * customPreviewAspect.y / customPreviewAspect.x) > 1)
                SetCustomPreviewResolution(safeSize);
            bool stacked = area.width < 520;
            float width = stacked ? area.width : Mathf.Min(300, (area.width - 16) / 2);
            var resolutionRect = new Rect(area.x, area.y, width, 22);
            var aspectRect = new Rect(stacked ? area.x : area.x + width + 16, stacked ? area.y + 26 : area.y, width, 22);
            var pixels = new Vector2Int(Mathf.RoundToInt(customResolution.x), Mathf.RoundToInt(customResolution.y));
            EditorGUI.BeginChangeCheck();
            pixels = DrawPreviewSizePair(resolutionRect, "해상도", "×", pixels, "미리보기 화면의 가로 × 세로 픽셀 수");
            if (EditorGUI.EndChangeCheck())
                SetCustomPreviewResolution(pixels);
            EditorGUI.BeginChangeCheck();
            var aspect = DrawPreviewSizePair(aspectRect, "비율", ":", customPreviewAspect, "가로 : 세로 비율. 변경하면 가로 해상도를 기준으로 세로를 계산합니다.");
            if (EditorGUI.EndChangeCheck())
                SetCustomPreviewAspect(aspect);
        }


        private static Vector2Int DrawPreviewSizePair(Rect area, string label, string separator, Vector2Int value, string tooltip)
        {
            GUI.Label(new Rect(area.x, area.y, 48, 22), new GUIContent(label, tooltip));
            float fieldWidth = Mathf.Max(25, (area.width - 70) / 2);
            int x = EditorGUI.DelayedIntField(new Rect(area.x + 50, area.y, fieldWidth, 22), value.x);
            GUI.Label(new Rect(area.x + 52 + fieldWidth, area.y, 16, 22), separator, EditorStyles.centeredGreyMiniLabel);
            int y = EditorGUI.DelayedIntField(new Rect(area.x + 70 + fieldWidth, area.y, fieldWidth, 22), value.y);
            return new Vector2Int(x, y);
        }


        private void SetCustomPreviewResolution(Vector2Int size)
        {
            size.x = Mathf.Clamp(size.x, 1, 32768);
            size.y = Mathf.Clamp(size.y, 1, 32768);
            customResolution = size;
            int a = size.x, b = size.y;
            while (b != 0)
            {
                int remainder = a % b;
                a = b;
                b = remainder;
            }

            customPreviewAspect = new Vector2Int(size.x / a, size.y / a);
            Window.Repaint();
        }


        private void SetCustomPreviewAspect(Vector2Int aspect)
        {
            aspect.x = Mathf.Clamp(aspect.x, 1, 10000);
            aspect.y = Mathf.Clamp(aspect.y, 1, 10000);
            customPreviewAspect = aspect;
            float ratio = (float)aspect.x / aspect.y;
            int width = Mathf.Clamp(Mathf.RoundToInt(customResolution.x), 1, 32768);
            float height = width / ratio;
            if (height > 32768)
                width = Mathf.Max(1, Mathf.RoundToInt(32768 * ratio));
            else if (height < 1)
                width = Mathf.CeilToInt(ratio);
            customResolution = new Vector2(width, Mathf.Clamp(Mathf.RoundToInt(width / ratio), 1, 32768));
            Window.Repaint();
        }

        [SerializeField]
        private bool previewApplyInputWait = true;

        [SerializeField]
        private int previewSpeedIndex = 2;

        internal static readonly float[] PreviewSpeeds =
        {
            .5f,
            .75f,
            1f,
            1.25f,
            1.5f,
            2f
        };

        private static readonly string[] PreviewSpeedLabels =
        {
            "0.5x",
            "0.75x",
            "1x",
            "1.25x",
            "1.5x",
            "2x"
        };

        [NonSerialized] private Vector2 previewPlaybackScroll;

        private static float PreviewPlaybackHeight(float width) => width < 370 ? 72 : width < 720 ? 54 : 29;

        private void DrawPreviewPlayback(Rect area)
        {
            float width = area.width - 20;
            bool stacked = width < 720, scroll = width < 370;
            float height = PreviewPlaybackHeight(width);
            var rect = new Rect(area.x + 10, area.yMax - height - 10, width, height);
            if (scroll)
                previewPlaybackScroll = GUI.BeginScrollView(rect, previewPlaybackScroll, new Rect(0, 0, 370, 54));
            else
                GUI.BeginGroup(rect);
            try
            {
                // 고정 사각형을 사용해 크기 변경과 팝업 이벤트 중 GUILayout 영역 스택이 달라지지 않게 함.
                DrawPreviewTransport(new Rect(0, 2, 365, 22));
                float optionsWidth = PreviewWaitWidth + 8 + PreviewSpeedTitleWidth + 6 + 75;
                DrawPreviewPlaybackOptions(new Rect(Mathf.Max(370, width) - optionsWidth, stacked ? 29 : 2, optionsWidth, 22));
            }
            finally
            {
                if (scroll)
                    GUI.EndScrollView();
                else
                    GUI.EndGroup();
            }
        }


        private void DrawPreviewTransport(Rect rect)
        {
            float y = rect.y;
            if (GUI.Button(new Rect(0, y, 32, 22), "|◀"))
                host.Seek(0);
            if (GUI.Button(new Rect(36, y, 25, 22), "‹"))
                host.Seek(host.Clock.Time - StudioTiming.FrameDuration(host.Sequence));
            if (GUI.Button(new Rect(65, y, 40, 22), host.Playing ? "Ⅱ" : "▶"))
                host.TogglePlay();
            if (GUI.Button(new Rect(109, y, 25, 22), "›"))
                host.Seek(host.Clock.Time + StudioTiming.FrameDuration(host.Sequence));
            GUI.Label(new Rect(140, y, 155, 22), host.Clock.Time.ToString("F2") + " / " + host.Sequence.Duration.ToString("F2") + "s");
            if (host.Clock.Waiting.HasValue && GUI.Button(new Rect(300, y, 55, 22), "E 다음"))
                host.Clock.Advance();
        }


        private static float PreviewWaitWidth => EditorStyles.toggle.CalcSize(new GUIContent("미리보기에서 입력 대기 적용")).x;

        private static float PreviewSpeedTitleWidth => EditorStyles.label.CalcSize(new GUIContent("배속")).x;


        private void DrawPreviewPlaybackOptions(Rect rect)
        {
            bool apply = GUI.Toggle(new Rect(rect.x, rect.y, PreviewWaitWidth, 22), previewApplyInputWait, "미리보기에서 입력 대기 적용", EditorStyles.toggle);
            if (apply != previewApplyInputWait)
            {
                previewApplyInputWait = apply;
                if (!apply && host.Clock.Waiting.HasValue)
                    host.Clock.Advance();
            }

            float speedX = rect.x + PreviewWaitWidth + 8;
            GUI.Label(new Rect(speedX, rect.y, PreviewSpeedTitleWidth, 22), "배속");
            StudioGUI.Popup(new Rect(speedX + PreviewSpeedTitleWidth + 6, rect.y, 75, 22), previewSpeedIndex, PreviewSpeedLabels, Window, SelectPreviewSpeed);
            void SelectPreviewSpeed(int index)
            {
                previewSpeedIndex = index;
            }
        }

        internal bool PreviewToolFocus { get { return previewToolFocus; } set { previewToolFocus = value; } }
        internal int PreviewDragAxis { get { return previewDragAxis; } set { previewDragAxis = value; } }
        internal bool PreviewApplyInputWait { get { return previewApplyInputWait; } set { previewApplyInputWait = value; } }
        internal int PreviewSpeedIndex { get { return previewSpeedIndex; } set { previewSpeedIndex = value; } }
    }
}
