using UnityEditor;
using UnityEngine.UIElements;

namespace NKStudio
{
    public static class ToolbarUtility
    {
        private const string ToolbarDarkStyleSheetPathGUID = "70d5ed84aacee41e8b5ad32f631a10fb";
        private const string ToolbarLightStyleSheetPathGUID = "f7504feab7f654b218f493aad2f15192";

        /// <summary>
        /// 에디터가 플레이 모드가 아닌 상태인지 확인하는 유틸리티 프로퍼티입니다.
        /// </summary>
        internal static bool IsNotPlaying => !EditorApplication.isPlaying;

        /// <summary>
        /// 에디터의 다크 모드 접두사를 반환하는 유틸리티 프로퍼티입니다.
        /// </summary>
        internal static string DarkModePrefix => EditorGUIUtility.isProSkin ? "d_" : "";

        /// <summary>
        /// Unity 2022 스타일의 버튼 스타일을 적용하는 메서드입니다.
        /// </summary>
        /// <param name="element">스타일을 적용할 VisualElement입니다.</param>
        internal static void Apply2022ButtonStyle(VisualElement element)
        {
            var path = AssetDatabase.GUIDToAssetPath(EditorGUIUtility.isProSkin
                ? ToolbarDarkStyleSheetPathGUID
                : ToolbarLightStyleSheetPathGUID);

            var styleSheet = AssetDatabase
                .LoadAssetAtPath<StyleSheet>(path);
            element.styleSheets.Add(styleSheet);

            element.AddToClassList("unity-toolbar-frame");
        }
    }
}