#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Tools
{
	using UnityEditor;
	using UnityEngine;

	internal static class CSMenuTools
	{

		public static bool ShowEditorSettings()
		{
			return CallUnifiedSettings("Editor");
		}

		public static bool ShowProjectSettingsGraphics()
		{
			return CallUnifiedSettings("Graphics");
		}

		public static bool ShowProjectSettingsPhysics()
		{
			return CallUnifiedSettings("Physics");
		}

		public static bool ShowProjectSettingsPhysics2D()
		{
			return CallUnifiedSettings("Physics 2D");
		}

		public static bool ShowProjectSettingsPresetManager()
		{
			return CallUnifiedSettings("Preset Manager");
		}

		public static bool ShowProjectSettingsPlayer()
		{
			return CallUnifiedSettings("Player");
		}

		public static bool ShowProjectSettingsTagsAndLayers()
		{
			return CallUnifiedSettings("Tags and Layers");
		}

		public static bool ShowProjectSettingsVFX()
		{
			return CallUnifiedSettings("VFX");
		}

		public static bool ShowSceneSettingsLighting()
		{
			var success = EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting");
			if (!success)
				Debug.LogError(Maintainer.ErrorForSupport("Can't open Lighting settings!"));

			return success;
		}

		public static bool ShowSceneSettingsNavigation()
		{
			var result = EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
			if (!result)
				Debug.LogError(Maintainer.ErrorForSupport("Can't open Navigation settings!"));
			
			return result;
		}

		private static bool CallUnifiedSettings(string providerName)
		{
			SettingsService.OpenProjectSettings("Project/" + providerName);
			return true;
		}
		
		public static bool ShowEditorBuildSettings()
		{
#if UNITY_6000_0_OR_NEWER
			if (EditorApplication.ExecuteMenuItem("File/Build Profiles"))
				return true;

			// Fallback to classic Build Settings if Build Profiles failed
#endif
			return EditorWindow.GetWindow(CSReflectionTools.buildPlayerWindowType, true);
		}
	}
}