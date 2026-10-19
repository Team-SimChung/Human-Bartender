using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal static class StudioGUI
    {
        // IMGUI 메뉴가 '/'를 하위 메뉴로 해석하지 않도록 일반 문구로 표시함.
        public static void Popup(Rect rect, int selected, string[] labels, EditorWindow owner, System.Action<int> choose)
        {
            if (!GUI.Button(rect, labels[Mathf.Clamp(selected, 0, labels.Length - 1)], EditorStyles.popup))
                return;
            var menu = new GenericDropdownMenu();
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                menu.AddItem(labels[i], i == selected, ChooseMenuItem);
                void ChooseMenuItem()
                {
                    choose(index);
                    owner.Repaint();
                }
            }

            ShowMenu(menu, rect, owner);
        }

        public static void ShowMenu(GenericDropdownMenu menu, Rect rect, EditorWindow owner)
        {
            var screen = GUIUtility.GUIToScreenRect(rect);
            screen.position -= owner.position.position;
            menu.DropDown(screen, owner.rootVisualElement, DropdownMenuSizeMode.Content);
        }

        private static GUIStyle sectionStyle, paddedStyle;
        public static GUIStyle Padded => paddedStyle ??= new GUIStyle
        {
            padding = new RectOffset(20, 10, 0, 10)
        };

        public static bool Section(bool open, string title, bool first = false)
        {
            sectionStyle ??= new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            if (!first)
                GUILayout.Space(6);
            Rect rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            // Inspector 컴포넌트 헤더처럼 내용의 여백까지 헤더를 넓힘.
            float left = Mathf.Max(0, rect.x - Padded.padding.left);
            var header = new Rect(left, rect.y, rect.xMax + Padded.padding.right - left, rect.height);
            var background = EditorGUIUtility.isProSkin ? new Color(.18f, .18f, .18f) : new Color(.68f, .68f, .68f);
            var border = EditorGUIUtility.isProSkin ? new Color(.10f, .10f, .10f) : new Color(.43f, .43f, .43f);
            var contentDivider = EditorGUIUtility.isProSkin ? new Color(.17f, .17f, .17f) : new Color(.56f, .56f, .56f);
            EditorGUI.DrawRect(header, background);
            EditorGUI.DrawRect(new Rect(header.x, header.y, header.width, 1), border);
            EditorGUI.DrawRect(new Rect(header.x, header.yMax - 1, header.width, 1), open ? contentDivider : border);
            bool changed = GUI.changed;
            open = EditorGUI.Foldout(new Rect(header.x + 8, header.y + 3, header.width - 16, header.height - 6), open, title, true, sectionStyle);
            GUI.changed = changed;
            GUILayout.Space(open ? 8 : 0);
            return open;
        }

        public static void Field(SerializedProperty property, string label)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            GUILayout.Space(5);
        }
        internal static readonly Color Accent = new(.24f, .49f, .75f);

        internal static void Panel(Rect rect) => EditorGUI.DrawRect(rect, new Color(.22f, .22f, .22f));

        internal static Rect DrawPanelHeader(Rect area, string title)
        {
            const float height = 36;
            EditorGUI.DrawRect(new Rect(area.x, area.y, area.width, height - 1), new Color(.20f, .20f, .20f));
            EditorGUI.DrawRect(new Rect(area.x, area.y + height - 1, area.width, 1), new Color(.10f, .10f, .10f));
            GUI.Label(new Rect(area.x + 12, area.y + 7, area.width - 24, 22), title, EditorStyles.boldLabel);
            return new Rect(area.x, area.y + height, area.width, Mathf.Max(0, area.height - height));
        }

        internal static Color ClipColor(StudioKind kind)
        {
            return StudioClipSchema.Definition(kind).Color;
        }

        internal static Button ActionButton(string text, Action action, string name = null)
        {
            var button = new Button(action)
            {
                text = text,
                name = name
            };
            button.AddToClassList("studio-button");
            return button;
        }

        internal static VisualElement MakePanel(string name, string title, out ScrollView body)
        {
            var panel = new VisualElement
            {
                name = name
            };
            panel.AddToClassList("studio-panel");
            var header = new Label(title);
            header.AddToClassList("studio-panel-header");
            panel.Add(header);
            body = new ScrollView(ScrollViewMode.Vertical)
            {
                name = name + "-scroll"
            };
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.contentContainer.style.width = Length.Percent(100);
            body.AddToClassList("studio-panel-body");
            panel.Add(body);
            return panel;
        }

        internal static Foldout Section(Dictionary<string, bool> panelFoldouts, VisualElement parent, string title, string key, bool initial, Action<bool> store = null)
        {
            var foldout = new Foldout
            {
                text = title,
                name = key
            };
            foldout.AddToClassList("studio-section");
            foldout.SetValueWithoutNotify(panelFoldouts.TryGetValue(key, out bool open) ? open : initial);
            foldout.RegisterValueChangedCallback(StoreFoldoutState);
            foldout.EnableInClassList("closed", !foldout.value);
            if (parent.childCount == 0)
                foldout.AddToClassList("first-section");
            parent.Add(foldout);
            return foldout;
            void StoreFoldoutState(ChangeEvent<bool> e)
            {
                if (e.target != foldout)
                    return;
                panelFoldouts[key] = e.newValue;
                store?.Invoke(e.newValue);
                foldout.EnableInClassList("closed", !e.newValue);
            }
        }

        internal static void DrawToolbar(StudioSequence sequence, ref bool autosave, Action create, Action<StudioSequence> load, Action<Rect> saveMenu)
        {
            using var row = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(StudioPanelLayout.MainToolbarHeight));
            if (GUILayout.Button("새 컷씬", EditorStyles.toolbarButton))
                create();
            var assetLabel = new GUIContent("현재 작업중인 컷씬 에셋 : ");
            float labelWidth = EditorStyles.label.CalcSize(assetLabel).x;
            var assetRect = GUILayoutUtility.GetRect(labelWidth + 180, labelWidth + 300, 18, 18);
            GUI.Label(new Rect(assetRect.x, assetRect.y, labelWidth, assetRect.height), assetLabel);
            var next = (StudioSequence)EditorGUI.ObjectField(new Rect(assetRect.x + labelWidth, assetRect.y, assetRect.width - labelWidth, assetRect.height), sequence, typeof(StudioSequence), false);
            if (next != sequence)
                load(next);
            GUILayout.FlexibleSpace();
            autosave = GUILayout.Toggle(autosave, "자동 저장", GUILayout.Width(85));
            if (GUILayout.Button("실행 취소", EditorStyles.toolbarButton))
                Undo.PerformUndo();
            if (GUILayout.Button("다시 실행", EditorStyles.toolbarButton))
                Undo.PerformRedo();
            using (new EditorGUI.DisabledScope(sequence == null))
            {
                var saveRect = GUILayoutUtility.GetRect(new GUIContent("저장"), EditorStyles.toolbarButton, GUILayout.Width(95));
                if (GUI.Button(saveRect, "저장", EditorStyles.toolbarButton))
                    saveMenu(saveRect);
            }
        }
    }
}
