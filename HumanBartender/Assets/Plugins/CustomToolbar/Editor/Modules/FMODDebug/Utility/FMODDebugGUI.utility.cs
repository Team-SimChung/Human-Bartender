#if USE_FMOD
using System.Reflection;
using FMODUnity;
using UnityEditor;

internal static partial class FMODDebugGUI
{
    /// <summary>
    /// FMOD 디버그가 활성화되어 있는지 확인합니다.
    /// </summary>
    private static bool IsFMODDebugActive => FMODDebugOverlay == TriStateBool.Enabled;

    /// <summary>
    /// FMOD 디버그 오버레이 값을 설정합니다.
    /// </summary>
    /// <param name="isEnabled">설정할 값입니다.</param>
    private static void SetFMODDebugOverlay(bool isEnabled)
    {
        var editorPlatform = Settings.Instance.PlayInEditorPlatform;

        // Lastly, access the Overlay property.
        var properties = editorPlatform.GetType().GetField("Properties",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (properties == null)
            return;

        var editorPlatformSettings = properties.GetValue(Settings.Instance.PlayInEditorPlatform);

        var overlayProperty = editorPlatformSettings.GetType().GetField("Overlay",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (overlayProperty == null)
            return;

        var overlayValue = overlayProperty.GetValue(editorPlatformSettings);

        var valueProperty = overlayValue.GetType().GetField("Value",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (valueProperty != null)
        {
            TriStateBool nextOverlayState = isEnabled ? TriStateBool.Enabled : TriStateBool.Disabled;
            valueProperty.SetValue(overlayValue, nextOverlayState);
        }

        EditorUtility.SetDirty(editorPlatform);
    }

    /// <summary>
    /// FMOD 디버그 오버레이 값을 가져옵니다.
    /// </summary>
    /// <returns>TriStateBool 값입니다.</returns>
    private static TriStateBool FMODDebugOverlay => Settings.Instance.PlayInEditorPlatform.Overlay;
}
#endif