#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

using UnityEditor;

namespace CodeStage.Maintainer.Tools
{
	using UnityEngine;

	internal static class CSSettingsTools
	{
		public static Object GetInSceneLightmapSettings()
		{
			var mi = CSReflectionTools.GetGetLightmapSettingsMethodInfo();
			if (mi != null)
			{
				return (Object)mi.Invoke(null, null);
			}

			Debug.LogError(Maintainer.ErrorForSupport("Can't retrieve LightmapSettings object via reflection!"));
			return null;
		}

		public static Object GetInSceneRenderSettings()
		{
			var mi = CSReflectionTools.GetGetRenderSettingsMethodInfo();
			if (mi != null)
			{
				return (Object)mi.Invoke(null, null);
			}

			Debug.LogError(Maintainer.ErrorForSupport("Can't retrieve RenderSettings object via reflection!"));
			return null;
		}
		
		public static (bool staticBatching, bool dynamicBatching) GetBuildTargetBatching(BuildTarget target)
		{
			var mi = CSReflectionTools.GetGetBatchingForPlatformMethodInfo();
			if (mi != null)
			{
				object[] parameters = { target, null, null };
				mi.Invoke(null, parameters);
				
				var staticBatching = (int)parameters[1] != 0;
				var dynamicBatching = (int)parameters[2] != 0;

				return (staticBatching, dynamicBatching);
			}

			Debug.LogError(Maintainer.ErrorForSupport("Can't retrieve LightmapSettings object via reflection!"));
			return (false,false);
		}

#if !UNITY_6000_0_OR_NEWER
		public static Object GetInSceneNavigationSettings()
		{
			return UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject;
		}
#endif
	}
}