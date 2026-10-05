#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static class SceneSwitchFirstSceneGUI
    {
#if !UNITY_6000_3_OR_NEWER
        /// <summary>
        /// 첫 번째 씬으로 이동하는 툴바 버튼을 생성하고 초기화합니다.
        /// </summary>
        /// <param name="element">버튼을 추가할 비주얼 엘리먼트</param>
        internal static void OnGUI(VisualElement element)
        {
            // 툴바 버튼 생성
            ToolbarButton button = new ToolbarButton
            {
                name = "InitSceneButton",
                text = "Start None",
                style =
                {
                    // 버튼 스타일 설정
                    marginRight = 3,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
                
            };

            // 시스템 언어에 따른 툴팁 설정
            string tooltip = Application.systemLanguage == SystemLanguage.Korean
                ? "최초 세팅 씬부터 게임을 진입합니다."
                : "Start from the first scene.";
            
            button.tooltip = tooltip;
            
            // 플레이 모드가 아닐 때만 버튼 활성화
            button.SetEnabled(ToolbarUtility.IsNotPlaying);
            
            // 1초마다 실행하여 첫 번째 씬 이름으로 버튼 텍스트 업데이트
            button.schedule.Execute(() => button.text = $"Start '{SceneHelper.FirstSceneName}'").Every(1000);
            
            // 플레이 모드가 변경될 때 버튼 활성화 상태 갱신
            EditorApplication.playModeStateChanged += _ => button.SetEnabled(ToolbarUtility.IsNotPlaying);
            
            // 버튼 클릭 시 타겟 씬으로 이동
            button.clicked += () => SceneHelper.StartScene(SceneHelper.TargetScenePath, true);
            
            // 툴바에 버튼 등록
            element.Add(button);
        }
#endif
    }

    /// <summary>
    /// 씬 선택 이동 드롭다운 메뉴를 담당하는 클래스
    /// </summary>
    internal static class SceneSwitchListMoveGUI
    {
#if !UNITY_6000_3_OR_NEWER
        /// <summary>
        /// 씬 선택 드롭다운 메뉴를 생성하고 초기화합니다.
        /// </summary>
        /// <param name="element">드롭다운을 추가할 비주얼 엘리먼트</param>
        public static void OnGUI(VisualElement element)
        {
            // 드롭다운 생성
            var dropdown = new EditorToolbarDropdown
            {
                name = "MoveSceneDropdown",
                text = "씬 선택 이동",
                style =
                {
                    // 버튼 스타일 설정
                    marginLeft = 3
                }
            };

            // 플레이 모드가 아닐 때만 드롭다운 활성화
            dropdown.SetEnabled(ToolbarUtility.IsNotPlaying);

            // 드롭다운 클릭 시 메뉴 표시
            dropdown.clicked += () => SceneHelper.ShowMenuItem(dropdown);

            // 플레이 모드가 변경될 때 드롭다운 활성화 상태 갱신
            EditorApplication.playModeStateChanged += _ => dropdown.SetEnabled(ToolbarUtility.IsNotPlaying);

            // 툴바에 드롭다운 등록
            element.Add(dropdown);
        }
#endif
    }
}

#endif