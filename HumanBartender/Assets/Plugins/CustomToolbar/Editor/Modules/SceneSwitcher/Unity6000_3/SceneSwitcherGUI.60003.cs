#if UNITY_EDITOR
using System;
using UnityEngine;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static partial class SceneSwitcherGUI
    {
#if UNITY_6000_3_OR_NEWER
        /// <summary>
        /// 툴바 버튼의 초기화를 수행합니다.
        /// </summary>
        /// <param name="element">MainToolbarWindow의 루트 비주얼 엘리먼트</param>
        public static void Init(VisualElement element)
        {
            if (element == null)
                return;

            // 첫 번째 씬 버튼을 찾아서 최대 너비를 자동으로 설정
            if (TryGetFirstSceneButton(element, out var firstSceneButton))
                firstSceneButton.style.maxWidth = new StyleLength(StyleKeyword.Auto);
        }

        /// <summary>
        /// 첫 번째 씬 버튼의 텍스트를 업데이트합니다.
        /// </summary>
        /// <param name="element">MainToolbarWindow의 루트 비주얼 엘리먼트</param>
        public static void UpdateText(VisualElement element)
        {
            if (element == null)
                return;

            // 첫 번째 씬 이름으로 버튼 텍스트 갱신
            if (TryGetFirstSceneButton(element, out var firstSceneButton))
                firstSceneButton.text = $"Start '{SceneHelper.FirstSceneName}'";
        }

        /// <summary>
        /// 씬 전환 버튼들의 활성화 상태를 설정합니다.
        /// </summary>
        /// <param name="element">MainToolbarWindow의 루트 비주얼 엘리먼트</param>
        /// <param name="enable">활성화 여부</param>
        public static void SetEnable(VisualElement element, bool enable)
        {
            // 첫 번째 씬으로 이동 버튼 활성화 설정
            if (TryGetFirstSceneButton(element, out var firstSceneButton))
                firstSceneButton.SetEnabled(enable);

            // 씬 선택 이동 버튼 활성화 설정
            if (TryGetMoveToSceneButton(element, out var moveToSceneButton))
                moveToSceneButton.SetEnabled(enable);
        }

        /// <summary>
        /// 첫 번째 씬으로 이동하는 툴바 버튼을 생성합니다.
        /// </summary>
        /// <returns>생성된 툴바 버튼</returns>
        [UsedImplicitly]
        [MainToolbarElement("NK Toolbar/Move to First Scene", defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -1)]
        public static MainToolbarElement InitSceneButton()
        {
            // 버튼 콘텐츠 생성
            var content = new MainToolbarContent
            {
                text = $"Start {SceneHelper.FirstSceneName}",
                tooltip = Application.systemLanguage == SystemLanguage.Korean
                    ? "최초 세팅 씬부터 게임을 진입합니다."
                    : "Start from the first scene."
            };

            // 버튼 생성 및 클릭 이벤트 연결
            var button = new MainToolbarButton(content, SceneHelper.MoveFirstScene);
            
            // Init
            EditorApplication.delayCall += SetupToolbarElement;

            return button;
        }

        /// <summary>
        /// Performs toolbar element setup and initializes it asynchronously by awaiting the next frame.
        /// </summary>
        private static void SetupToolbarElement()
        {
            if (ToolbarSystem.TryFindMainToolbarWindow(out var window))
                Init(window);
        }
        
        /// <summary>
        /// 씬 선택 드롭다운 메뉴를 생성합니다.
        /// </summary>
        /// <returns>생성된 툴바 드롭다운</returns>
        [UsedImplicitly]
        [MainToolbarElement("NK Toolbar/Move To Scene", defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = 1)]
        public static MainToolbarElement MoveSceneButton()
        {
            // 드롭다운 콘텐츠 생성
            var content = new MainToolbarContent
            {
                text = "씬 선택 이동"
            };

            // 드롭다운 생성 및 클릭 이벤트 연결
            var dropdown = new MainToolbarDropdown(content, SceneHelper.ShowMenuItem);

            return dropdown;
        }

        /// <summary>
        /// 지정된 경로에서 툴바 버튼을 찾습니다.
        /// </summary>
        /// <param name="element">루트 비주얼 엘리먼트</param>
        /// <param name="path">버튼의 부모 엘리먼트 경로</param>
        /// <param name="button">찾은 버튼 (성공 시)</param>
        /// <returns>버튼을 찾았으면 true, 아니면 false</returns>
        private static bool TryGetButton(VisualElement element, string path, out EditorToolbarButton button)
        {
            // 엘리먼트가 null이면 실패
            if (element == null)
            {
                button = null;
                return false;
            }

            // 지정된 경로에서 부모 엘리먼트 검색
            var parent = element.Q<VisualElement>(path);

            // 부모 엘리먼트가 없으면 실패
            if (parent == null)
            {
                button = null;
                return false;
            }

            // 부모 엘리먼트에서 EditorToolbarButton 검색
            button = parent.Q<EditorToolbarButton>();

            // 버튼을 찾지 못하면 실패
            if (button == null)
                return false;

            return true;
        }

        /// <summary>
        /// 첫 번째 씬으로 이동하는 버튼을 찾습니다.
        /// </summary>
        /// <param name="element">루트 비주얼 엘리먼트</param>
        /// <param name="button">찾은 버튼 (성공 시)</param>
        /// <returns>버튼을 찾았으면 true, 아니면 false</returns>
        private static bool TryGetFirstSceneButton(VisualElement element, out EditorToolbarButton button)
        {
            return TryGetButton(element, "NKToolbar/MovetoFirstScene", out button);
        }

        /// <summary>
        /// 씬 선택 이동 버튼을 찾습니다.
        /// </summary>
        /// <param name="element">루트 비주얼 엘리먼트</param>
        /// <param name="button">찾은 버튼 (성공 시)</param>
        /// <returns>버튼을 찾았으면 true, 아니면 false</returns>
        private static bool TryGetMoveToSceneButton(VisualElement element, out EditorToolbarButton button)
        {
            return TryGetButton(element, "NKToolbar/MoveToScene", out button);
        }
#endif
    }
}

#endif