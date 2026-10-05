#if USE_FMOD
using NKStudio;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal static partial class FMODDebugGUI
{
#if !UNITY_6000_3_OR_NEWER
    /// <summary>
    /// FMOD 디버그 토글을 GUI에 추가합니다.
    /// </summary>
    /// <param name="element">추가할 VisualElement입니다.</param>
    internal static void OnGUI(VisualElement element)
    {
        var fmodDebugToggle = new Toggle
        {
            name = "FMODDebugToggle",
            text = "FMOD Debug"
        };

#if UNITY_6000
        var icon = new Image
        {
            style =
            {
                width = 54,
                height = 16,
                alignSelf = Align.Center
            }
        };
        
        // 아이콘 이미지 적용
        var iconPath = AssetDatabase.GUIDToAssetPath("f0de615af202e44a190b20daca134fc4");
        icon.image = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/{ToolbarUtility.DarkModePrefix}fmod.png");

        // 토글 맨 앞에 아이콘 추가
        fmodDebugToggle[0].Insert(0, icon);
#endif

        // 버튼 스타일 적용
#if UNITY_6000
            // No Apply
#else
        ToolbarUtility.Apply2022ButtonStyle(fmodDebugToggle);
#endif

        // 체크박스 위치 변경
        var checkmark = fmodDebugToggle[0].Q<VisualElement>("unity-checkmark");
        fmodDebugToggle[0].Remove(checkmark);
        fmodDebugToggle[0].Add(checkmark);

        // 마진 추가
        var label = fmodDebugToggle[0].Q<Label>();
        label.style.marginLeft = 4;
        label.style.marginRight = 4;

        // 초기 값 설정
        fmodDebugToggle.value = IsFMODDebugActive;

        // 플레이 중에는 비활성화
        fmodDebugToggle.SetEnabled(ToolbarUtility.IsNotPlaying);

        // 값 변경 이벤트 설정
        fmodDebugToggle.RegisterValueChangedCallback(evt =>
        {
            var newValue = evt.newValue;
            SetFMODDebugOverlay(newValue);
        });

        // 플레이 중에는 비활성화
        EditorApplication.playModeStateChanged += _ => fmodDebugToggle.SetEnabled(ToolbarUtility.IsNotPlaying);

        // 툴바에 추가
        element.Add(fmodDebugToggle);
    }
#endif
}
#endif