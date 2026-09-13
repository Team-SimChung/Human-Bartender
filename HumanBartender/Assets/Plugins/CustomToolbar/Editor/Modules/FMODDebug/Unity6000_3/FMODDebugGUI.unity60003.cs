#if USE_FMOD
using System;
using JetBrains.Annotations;
using NKStudio;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

internal static partial class FMODDebugGUI
{
#if UNITY_6000_3_OR_NEWER
    /// <summary>
    /// Initializes the FMOD debug toggle UI.
    /// </summary>
    /// <param name="element">The MainToolbarWindow root visual element.</param>
    public static void Init(VisualElement element)
    {
        if (element == null)
            return;

        if (TryGetToggle(element, out var toggle))
        {
            toggle.value = IsFMODDebugActive;
            toggle.style.maxWidth = new StyleLength(StyleKeyword.Auto);

            var icon = toggle.Q<Image>();
            icon.style.width = 50;
            icon.style.height = 16;
        }
    }

    /// <summary>
    /// Sets the enabled state of the FMOD debug toggle.
    /// </summary>
    /// <param name="element">The MainToolbarWindow root visual element.</param>
    /// <param name="enable">Whether the toggle should be enabled.</param>
    public static void SetEnable(VisualElement element, bool enable)
    {
        if (element == null)
            return;

        if (TryGetToggle(element, out var toggle))
            toggle.SetEnabled(enable);
    }
    
    [MainToolbarElement("NK Toolbar/FMOD Debugger Toggle", defaultDockPosition = MainToolbarDockPosition.Left,
        defaultDockIndex = 13), UsedImplicitly]
    private static MainToolbarElement OnGUI()
    {
        // Text
        const string text = "FMOD Debug";

        // Icon
        var iconPath = AssetDatabase.GUIDToAssetPath("f0de615af202e44a190b20daca134fc4");
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/{ToolbarUtility.DarkModePrefix}fmod.png");

        MainToolbarContent content = new MainToolbarContent(text, tooltip: string.Empty, image: icon);
        MainToolbarToggle dropdown = new MainToolbarToggle(content, false, OnValueChanged);
        
        EditorApplication.delayCall += SetupToolbarElement;

        // PlayMode가 되면 드롭다운 비활성화 유도
        EditorApplication.playModeStateChanged += _ =>
        {
            dropdown.enabled = !EditorApplication.isPlaying;
        };
        
        // Init
        SetupToolbarElement();

        return dropdown;
    }

    /// <summary>
    /// Toolbar Element가 생성되고 나서 초기화를 합니다.
    /// </summary>
    private static void SetupToolbarElement()
    {
        try
        {
            if (ToolbarSystem.TryFindMainToolbarWindow(out var window)) 
                Init(window);
        }
        catch (Exception e)
        {
            // ignored
        }
    }

    /// <summary>
    /// Handles value changes of the FMOD debug toggle.
    /// </summary>
    /// <param name="newValue">The new toggle value.</param>
    private static void OnValueChanged(bool newValue)
    {
        SetFMODDebugOverlay(newValue);
    }

    /// <summary>
    /// Attempts to get the FMOD debug toggle from the toolbar.
    /// </summary>
    /// <param name="element">The root visual element.</param>
    /// <param name="toggle">The found toggle, if successful.</param>
    /// <returns>True if the toggle was found, false otherwise.</returns>
    private static bool TryGetToggle(VisualElement element, out EditorToolbarToggle toggle)
    {
        var parent = element.Q<VisualElement>("NKToolbar/FMODDebuggerToggle");

        if (parent == null)
        {
            toggle = null;
            return false;
        }

        toggle = parent.Q<EditorToolbarToggle>();

        if (toggle != null)
            return true;

        return false;
    }

#endif
}
#endif