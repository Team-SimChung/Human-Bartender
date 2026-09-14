using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
using System.Linq;
#endif

namespace CodeStage.EditorCommon.Tools
{
	internal static class CSSceneTools
	{
		public static Scene[] GetOpenedScenes()
		{
			var openScenes = new Scene[SceneManager.sceneCount];
			for (var i = 0; i < SceneManager.sceneCount; i++)
			{
				openScenes[i] = SceneManager.GetSceneAt(i);
			}
			return openScenes;
		}

		public static string[] GetOpenedValidScenesPaths()
		{
			var openedScenes = GetOpenedScenes();
			var scenePaths = new List<string>();
			foreach (var scene in openedScenes)
			{
				if (scene.IsValid())
					scenePaths.Add(scene.path);
			}
			return scenePaths.ToArray();
		}

		public static string[] GetBuildProfilesScenesPaths(bool includeDisabled = false)
		{
#if UNITY_6000_0_OR_NEWER
			var scenePaths = new HashSet<string>(GetBuildSettingsSceneList(includeDisabled)); // uses EditorBuildSettings.scenes
			var allBuildProfiles = AssetDatabase.FindAssets("t:BuildProfile");
			foreach (var buildProfileGuid in allBuildProfiles)
			{
				var buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(buildProfileGuid));
#if UNITY_6000_1_OR_NEWER
				var overrideGlobalSceneList = buildProfile.overrideGlobalScenes;
#else
				var overrideGlobalSceneList = false;
				try
				{
					var overrideGlobalSceneListProperty = typeof(BuildProfile).GetProperty("overrideGlobalSceneList", 
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					overrideGlobalSceneList = overrideGlobalSceneListProperty != null && 
												  (bool)overrideGlobalSceneListProperty.GetValue(buildProfile);
				}
				catch { // ignored
				}
#endif
				if (!overrideGlobalSceneList)
					continue;

				foreach (var overridenSceneListScene in buildProfile.scenes)
				{
					scenePaths.Add(overridenSceneListScene.path);
				}
			}
			return scenePaths.ToArray();
#else
			return GetBuildSettingsSceneList().ToArray();
#endif
		}

		private static List<string> GetBuildSettingsSceneList(bool includeDisabled = false)
		{
			var scenes = EditorBuildSettings.scenes;
			var scenePaths = new List<string>();
			foreach (var scene in scenes)
			{
				if (includeDisabled || scene.enabled)
					scenePaths.Add(scene.path);
			}
			return scenePaths;
		}
	}
}