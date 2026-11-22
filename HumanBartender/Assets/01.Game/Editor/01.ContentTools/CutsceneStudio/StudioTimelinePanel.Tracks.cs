using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioTimelinePanel
    {
        [NonSerialized] private StudioTimelineRow reorderSource, reorderTarget;
        [NonSerialized] private Vector2 reorderStart;
        [NonSerialized] private int reorderControl;
        [NonSerialized] private bool reordering, reorderAfter, renameOnClick;

        private void HandleTrackReorder(Rect viewport, StudioTimelineRow[] rows)
        {
            var e = Event.current;
            int control = GUIUtility.GetControlID("StudioTrackReorder".GetHashCode(), FocusType.Passive);
            var names = new Rect(viewport.x, viewport.y + StudioTimelineLayout.RulerHeight,
                TrackHeaderWidth - 68, Mathf.Max(0, viewport.height - StudioTimelineLayout.RulerHeight - 16));
            if (reorderSource != null && GUIUtility.hotControl != reorderControl)
                ReleaseTrackReorder();

            if (e.type == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl == 0 && names.Contains(e.mousePosition))
            {
                float y = e.mousePosition.y - viewport.y + timelineScroll.y;
                var row = rows.FirstOrDefault(item => item.Track != null && y >= item.Y && y < item.Y + item.Height);
                if (row == null) return;
                reorderSource = row;
                reorderStart = e.mousePosition;
                reorderControl = control;
                reordering = false;
                renameOnClick = e.mousePosition.x >= viewport.x + 22;
                GUIUtility.hotControl = control;
                GUIUtility.keyboardControl = 0;
                e.Use();
            }
            else if (reorderSource != null && GUIUtility.hotControl == reorderControl)
            {
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    ReleaseTrackReorder();
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp && e.button == 0)
                {
                    reordering |= (e.mousePosition - reorderStart).sqrMagnitude >= 16;
                    if (reordering)
                    {
                        // 긴 목록에서는 이름 열의 위 / 아래 가장자리로 드래그해 스크롤함.
                        if (e.type == EventType.MouseDrag && names.Contains(e.mousePosition))
                        {
                            float delta = e.mousePosition.y < names.y + 18 ? -12 : e.mousePosition.y > names.yMax - 18 ? 12 : 0;
                            float bottom = rows.Length == 0 ? 0 : rows.Last().Y + rows.Last().Height + 40;
                            timelineScroll.y = Mathf.Clamp(timelineScroll.y + delta, 0, Mathf.Max(0, bottom - viewport.height + 16));
                        }
                        reorderTarget = names.Contains(e.mousePosition)
                            ? rows.FirstOrDefault(row => row.Group == reorderSource.Group && row.Track != null &&
                                e.mousePosition.y - viewport.y + timelineScroll.y >= row.Y &&
                                e.mousePosition.y - viewport.y + timelineScroll.y < row.Y + row.Height)
                            : null;
                        reorderAfter = reorderTarget != null && e.mousePosition.y - viewport.y + timelineScroll.y >= reorderTarget.Y + reorderTarget.Height * .5f;
                    }
                    if (e.type == EventType.MouseUp)
                    {
                        var source = reorderSource;
                        var target = reorderTarget;
                        bool after = reorderAfter;
                        bool move = reordering;
                        bool rename = !move && renameOnClick && names.Contains(e.mousePosition);
                        ReleaseTrackReorder();
                        if (move && target != null && StudioTrackOrdering.Move(host.Sequence, source.Group, source.Track, target.Track, after))
                            host.Changed();
                        else if (rename)
                            StudioTrackSettingsWindow.Open(Window, host.Sequence, source.Track, TimelineTrackName(source));
                    }
                    e.Use();
                    Window.Repaint();
                }
            }
        }

        private void DrawTrackDragLabel(Rect nameRect, float nameY, StudioTimelineRow row)
        {
            var label = new Rect(nameRect.x + 22, nameY, TrackHeaderWidth - 90, 22);
            GUI.Label(label, new GUIContent(TimelineTrackName(row), "드래그하여 같은 카테고리 안에서 순서 변경 / 클릭하여 트랙 이름 변경"), EditorStyles.label);
            if (reordering && reorderSource?.Track == row.Track && reorderSource.Group == row.Group)
                EditorGUI.DrawRect(new Rect(nameRect.x + 2, nameRect.y + 3, 3, nameRect.height - 6), new Color(.72f, .72f, .72f));
            for (int i = 0; i < 3; i++)
                EditorGUI.DrawRect(new Rect(nameRect.x + 8, nameY + 7 + i * 3, 7, 1), new Color(.45f, .45f, .45f));
            EditorGUIUtility.AddCursorRect(new Rect(nameRect.x + 4, nameY, TrackHeaderWidth - 72, 22), MouseCursor.MoveArrow);
        }

        private void DrawTrackInsertion(float width)
        {
            if (!reordering || reorderTarget == null || reorderTarget.Track == reorderSource?.Track)
                return;
            float y = reorderTarget.Y + (reorderAfter ? reorderTarget.Height : 0);
            EditorGUI.DrawRect(new Rect(timelineScroll.x + 4, y - 1, width - timelineScroll.x - 8, 2), new Color(.82f, .82f, .82f));
        }

        internal void ReleaseTrackReorder()
        {
            if (reorderSource == null) return;
            if (GUIUtility.hotControl == reorderControl)
                GUIUtility.hotControl = 0;
            reorderSource = reorderTarget = null;
            reordering = renameOnClick = false;
        }
    }
}
