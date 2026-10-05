using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static partial class EnterPlayModeGUI
    {
#if !UNITY_6000_3_OR_NEWER
        internal static void OnGUI(VisualElement element)
        {
            var toggle = new Toggle();
            toggle.name = "EnterPlayModeOption";
            toggle.text = "Enter Play Mode";

            var icon = new Image();
            
            // 사이즈 변경
            icon.style.alignSelf = Align.Center;
            icon.style.width = 16;
            icon.style.height = 16;

            // 이미지 적용
            var iconPath = AssetDatabase.GUIDToAssetPath("c0b301a9175a74304992d875f40f670d");
            icon.image = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/{ToolbarUtility.DarkModePrefix}rush.png");
            
            // 토글 맨 앞에 아이콘 추가
            toggle[0].Insert(0, icon);
            
            // 버튼 스타일 적용
#if UNITY_6000
            // No Apply
#else
            ToolbarUtility.Apply2022ButtonStyle(toggle);
#endif
            
            // 체크박스 삭제 후 뒤에 생성하도록 처리
            var check = toggle[0].Q<VisualElement>("unity-checkmark");
            toggle[0].Remove(check);
            toggle[0].Add(check);

            // 마진 추가
            var label = toggle[0].Q<Label>();
            label.style.marginLeft = 4;
            label.style.marginRight = 4;

            // 초기 값 설정
            toggle.value = EditorSettings.enterPlayModeOptionsEnabled;
            
            // 플레이 중에는 비활성화
            toggle.SetEnabled(ToolbarUtility.IsNotPlaying);
            
            // 값 변경 이벤트 설정
            toggle.RegisterValueChangedCallback(evt => EditorSettings.enterPlayModeOptionsEnabled = evt.newValue);

            // 플레이 중에는 비활성화
            EditorApplication.playModeStateChanged += _ => toggle.SetEnabled(ToolbarUtility.IsNotPlaying);
            
            // 툴바에 추가
            element.Add(toggle);
        }
#endif
    }
}