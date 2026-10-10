using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 패널 크기와 경계 드래그 및 배치 설정을 관리함.
    internal sealed class StudioPanelLayout
    {
        internal const float MainToolbarHeight = 22;
        private readonly EditorWindow Window;
        internal StudioPanelLayout(EditorWindow window) { Window = window; }

        internal const string LayoutKey = "HumanBartender.CutsceneStudio.Layout.";

        private const float MinSidebar = 240, MinInspector = 280, MinPreviewWidth = 400;

        private const float MinTimeline = 150, MinPreviewHeight = 250;

        private Vector3 preferredPanels = new(250, 300, 240);

        private Vector3 visiblePanels;

        private int resizingPanel = -1, panelControl;

        private Vector2 panelDragStart;

        private Vector3 panelDragSizes;

        internal float Sidebar => visiblePanels.x;

        internal float Inspector => visiblePanels.y;

        internal float TimelineHeight => visiblePanels.z;


        internal void Load()
        {
            Window.minSize = new Vector2(1050, 690);
            preferredPanels = new Vector3(EditorPrefs.GetFloat(LayoutKey + "Sidebar", 250), EditorPrefs.GetFloat(LayoutKey + "Inspector", 300), EditorPrefs.GetFloat(LayoutKey + "Timeline", 240));
            Update();
        }


        internal void Update()
        {
            visiblePanels = CalculateVisible(Window.position.size, preferredPanels);
        }

        internal static Vector3 CalculateVisible(Vector2 size, Vector3 preferredPanels)
        {
            var visiblePanels = Vector3.zero;
            // 전체 창이 잠시 작아져도 사용자가 지정한 패널 크기는 유지함.
            float width = Mathf.Max(1, size.x - 2);
            float scale = Mathf.Min(1, width / (MinSidebar + MinInspector + MinPreviewWidth));
            float sideMin = MinSidebar * scale, inspectorMin = MinInspector * scale;
            float available = width - MinPreviewWidth * scale;
            float side = Mathf.Max(sideMin, preferredPanels.x);
            float inspector = Mathf.Max(inspectorMin, preferredPanels.y);
            float extra = side + inspector - sideMin - inspectorMin;
            float factor = extra > 0 ? Mathf.Clamp01((available - sideMin - inspectorMin) / extra) : 0;
            visiblePanels.x = sideMin + (side - sideMin) * factor;
            visiblePanels.y = inspectorMin + (inspector - inspectorMin) * factor;
            float height = Mathf.Max(1, size.y - MainToolbarHeight);
            float heightScale = Mathf.Min(1, height / (MinTimeline + MinPreviewHeight));
            visiblePanels.z = Mathf.Clamp(preferredPanels.z, MinTimeline * heightScale, height - MinPreviewHeight * heightScale);
            return visiblePanels;
        }


        private Rect PanelDivider(int index)
        {
            float top = Window.position.height - TimelineHeight;
            float right = Window.position.width - Inspector;
            return index switch
            {
                0 => new Rect(Sidebar - 3, MainToolbarHeight, 6, top - MainToolbarHeight - 3),
                1 => new Rect(right - 3, MainToolbarHeight, 6, Window.position.height - MainToolbarHeight),
                _ => new Rect(0, top - 3, right - 3, 6)};
        }


        internal void HandleResize()
        {
            panelControl = GUIUtility.GetControlID("StudioPanelResize".GetHashCode(), FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(panelControl))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || GUIUtility.hotControl != 0)
                        break;
                    for (int i = 0; i < 3; i++)
                    {
                        if (!PanelDivider(i).Contains(e.mousePosition))
                            continue;
                        resizingPanel = i;
                        panelDragStart = e.mousePosition;
                        panelDragSizes = visiblePanels;
                        GUIUtility.hotControl = panelControl;
                        GUIUtility.keyboardControl = 0;
                        EditorGUIUtility.editingTextField = false;
                        e.Use();
                        break;
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != panelControl || resizingPanel < 0)
                        break;
                    Vector2 delta = e.mousePosition - panelDragStart;
                    preferredPanels = panelDragSizes;
                    if (resizingPanel == 0)
                        preferredPanels.x = Mathf.Clamp(panelDragSizes.x + delta.x, MinSidebar, Mathf.Max(MinSidebar, Window.position.width - Inspector - MinPreviewWidth - 2));
                    else if (resizingPanel == 1)
                        preferredPanels.y = Mathf.Clamp(panelDragSizes.y - delta.x, MinInspector, Mathf.Max(MinInspector, Window.position.width - Sidebar - MinPreviewWidth - 2));
                    else
                        preferredPanels.z = Mathf.Clamp(panelDragSizes.z - delta.y, MinTimeline, Mathf.Max(MinTimeline, Window.position.height - MainToolbarHeight - MinPreviewHeight));
                    Update();
                    e.Use();
                    Window.Repaint();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != panelControl || resizingPanel < 0 || e.button != 0)
                        break;
                    ReleaseDrag();
                    e.Use();
                    Window.Repaint();
                    break;
            }
        }


        internal void ReleaseDrag()
        {
            if (resizingPanel < 0)
                return;
            if (GUIUtility.hotControl == panelControl)
                GUIUtility.hotControl = 0;
            resizingPanel = -1;
            EditorPrefs.SetFloat(LayoutKey + "Sidebar", preferredPanels.x);
            EditorPrefs.SetFloat(LayoutKey + "Inspector", preferredPanels.y);
            EditorPrefs.SetFloat(LayoutKey + "Timeline", preferredPanels.z);
        }


        private void DrawPanelResizeCursors()
        {
            for (int i = 0; i < 3; i++)
            {
                var divider = PanelDivider(i);
                EditorGUIUtility.AddCursorRect(divider, i == 2 ? MouseCursor.ResizeVertical : MouseCursor.ResizeHorizontal, panelControl);
                if (resizingPanel == i)
                    EditorGUI.DrawRect(divider, StudioGUI.Accent);
            }
        }
        internal void DrawDividers()
        {
            float timelineTop = Window.position.height - TimelineHeight;
            float inspectorLeft = Window.position.width - Inspector;
            var edge = new Color(.10f, .10f, .10f);
            var rim = new Color(.30f, .30f, .30f);
            // 주변 Unity 패널과 맞추어 경계선을 그림.
            EditorGUI.DrawRect(new Rect(Sidebar - 2, MainToolbarHeight, 4, timelineTop - MainToolbarHeight), edge);
            EditorGUI.DrawRect(new Rect(Sidebar + 2, MainToolbarHeight, 1, timelineTop - MainToolbarHeight), rim);
            EditorGUI.DrawRect(new Rect(inspectorLeft - 2, MainToolbarHeight, 4, Window.position.height - MainToolbarHeight), edge);
            EditorGUI.DrawRect(new Rect(inspectorLeft + 2, MainToolbarHeight, 1, Window.position.height - MainToolbarHeight), rim);
            EditorGUI.DrawRect(new Rect(0, timelineTop - 2, inspectorLeft - 2, 4), edge);
            EditorGUI.DrawRect(new Rect(0, timelineTop + 2, inspectorLeft - 2, 1), rim);
            EditorGUI.DrawRect(new Rect(0, MainToolbarHeight - 1, Window.position.width, 1), edge);
            DrawPanelResizeCursors();
        }
    }
}
