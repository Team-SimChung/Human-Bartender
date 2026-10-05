#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.References.Entry
{
	using System.Collections.Generic;
	using Core;
	using Tools;
	using UnityEditor;
	using UnityEngine;
	using UnityEngine.AI;
	using UnityEngine.Rendering;

	internal static class SceneSettingsProcessor
	{
		private const string EnvironmentSettingsPath = "Lighting settings (Environment tab > Environment fold > ";
		private const string OtherSettingsPath = "Lighting settings (Environment tab > Other Settings fold > ";
		
		private static Object lightmapSettings;
		private static SerializedObject lightmapSettingsSo;
		private static SerializedProperty lightmapParametersField;
		
#if !UNITY_6000_0_OR_NEWER
		private static SerializedObject navMeshSettingsSo;
		private static SerializedProperty navMeshDataField;
#endif

		private static Object renderSettings;
		private static SerializedObject renderSettingsSo;
		private static SerializedProperty renderHaloField;
		private static SerializedProperty renderSpotField;

		public static void Process(List<TreeConjunction> conjunctions)
		{
			lightmapSettings = null;
			lightmapSettingsSo = null;
			lightmapParametersField = null;

#if !UNITY_6000_0_OR_NEWER
			navMeshSettingsSo = null;
			navMeshDataField = null;
#endif

			renderSettings = null;
			renderSettingsSo = null;
			renderHaloField = null;
			renderSpotField = null;

			foreach (var conjunction in conjunctions)
			{
				var referencedAsset = conjunction.referencedAsset;
				var referencedAssetObjects = referencedAsset.GetAllAssetObjects();

				foreach (var candidateInstanceId in referencedAssetObjects)
				{
					var candidate = CSInstanceIdTools.IdToObject(candidateInstanceId);

					if (candidate is LightingDataAsset)
					{
						CheckLightingDataAsset(conjunction, candidateInstanceId);
					}
					else if (candidate is LightingSettings)
					{
						CheckLightingSettingsAsset(conjunction, candidateInstanceId);
					}
					else if (candidate is Material)
					{
						CheckSkybox(conjunction, candidateInstanceId);
					}
					else if (candidate is LightmapParameters)
					{
						CheckLightMapSettings(conjunction, candidateInstanceId);
					}
#if !UNITY_2022_1_OR_NEWER
					else if (candidate is Cubemap)
					{
						CheckLightingCubemap(conjunction, candidateInstanceId);
					}
#endif
#if !UNITY_6000_0_OR_NEWER
					else if (candidate is NavMeshData)
					{
						CheckNavMesh(conjunction, candidateInstanceId);
					}
#endif
					else if (candidate is Light)
					{
						CheckSun(conjunction, candidateInstanceId);
					}
					else if (candidate is Texture)
					{
						CheckRenderSettingsTexture(conjunction, candidateInstanceId);
#if UNITY_2022_1_OR_NEWER
						CheckLightingCubemap(conjunction, candidateInstanceId);
#endif
					}
				}
			}
		}

#if !UNITY_6000_0_OR_NEWER
		private static void CheckNavMesh(TreeConjunction conjunction, long candidateInstanceId)
		{
			navMeshSettingsSo = navMeshSettingsSo ?? new SerializedObject(CSSettingsTools.GetInSceneNavigationSettings());
			navMeshDataField = navMeshDataField ?? navMeshSettingsSo.FindProperty("m_NavMeshData");

			if (navMeshDataField != null && navMeshDataField.propertyType == SerializedPropertyType.ObjectReference)
			{
				if (CSInstanceIdTools.GetObjectReferenceId(navMeshDataField) == candidateInstanceId)
				{
					var entry = new ReferencingEntryData
					{
						location = Location.SceneNavigationSettings,
						prefixLabel = "Navigation settings of scene"
					};

					conjunction.referencedAtInfo.AddNewEntry(entry);
				}
			}
			else
			{
				Debug.LogError(
					Maintainer.ErrorForSupport("Can't find m_NavMeshData at the navMeshSettingsObject!"));
			}
		}
#endif

		private static void CheckLightingDataAsset(TreeConjunction conjunction, long candidateInstanceId)
		{
			if (Lightmapping.lightingDataAsset == null || CSInstanceIdTools.GetId(Lightmapping.lightingDataAsset) != candidateInstanceId) return;

			var entry = new ReferencingEntryData
			{
				location = Location.SceneLightingSettings,
				prefixLabel = "Lighting settings (Baked lightmaps tab)"
			};

			conjunction.referencedAtInfo.AddNewEntry(entry);
		}
		
		private static void CheckLightingSettingsAsset(TreeConjunction conjunction, long candidateInstanceId)
		{
			try
			{
				if (Lightmapping.lightingSettings == null ||
					CSInstanceIdTools.GetId(Lightmapping.lightingSettings) != candidateInstanceId)
					return;

			}
			catch (System.Exception)
			{
				// ignoring "System.Exception: Lightmapping.lightingSettings is null. Please assign it to an existing asset or a new instance."
				return;
			}
			
			var entry = new ReferencingEntryData
			{
				location = Location.SceneLightingSettings,
				prefixLabel = "Lighting settings (Scene tab)"
			};

			conjunction.referencedAtInfo.AddNewEntry(entry);
		}
		
		private static void CheckSkybox(TreeConjunction conjunction, long candidateInstanceId)
		{
			if (RenderSettings.skybox == null || CSInstanceIdTools.GetId(RenderSettings.skybox) != candidateInstanceId) return;

			var entry = new ReferencingEntryData
			{
				location = Location.SceneLightingSettings,
				prefixLabel = ConstructEnvironmentSettingPath("Skybox Material")
			};

			conjunction.referencedAtInfo.AddNewEntry(entry);
		}

		private static void CheckLightMapSettings(TreeConjunction conjunction, long candidateInstanceId)
		{
			lightmapSettings = lightmapSettings ? lightmapSettings : CSSettingsTools.GetInSceneLightmapSettings();

			if (lightmapSettings == null) return;

			lightmapSettingsSo = lightmapSettingsSo ?? new SerializedObject(lightmapSettings);
			lightmapParametersField = lightmapParametersField ??
									  lightmapSettingsSo.FindProperty(
										  "m_LightmapEditorSettings.m_LightmapParameters");
			if (lightmapParametersField != null && lightmapParametersField.propertyType ==
				SerializedPropertyType.ObjectReference)
			{
				if (CSInstanceIdTools.GetObjectReferenceId(lightmapParametersField) == candidateInstanceId)
				{
					var entry = new ReferencingEntryData
					{
						location = Location.SceneLightingSettings,
						prefixLabel =
							"Lighting settings (Scene tab > Lightmapping Settings > Lightmap Parameters)"
					};

					conjunction.referencedAtInfo.AddNewEntry(entry);
				}
			}
			else
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't find m_LightmapParameters at the LightmapSettings!"));
			}
		}

		private static void CheckLightingCubemap(TreeConjunction conjunction, long candidateInstanceId)
		{
#if UNITY_2022_1_OR_NEWER
			var reflection = RenderSettings.customReflectionTexture;
#else
			var reflection = RenderSettings.customReflection;
#endif
			
			if (reflection == null ||
				CSInstanceIdTools.GetId(reflection) !=
				candidateInstanceId) return;

			var entry = new ReferencingEntryData { location = Location.SceneLightingSettings };

			if (RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom)
			{
				entry.prefixLabel = ConstructEnvironmentSettingPath("Environment Reflections > Cubemap");
			}
			else
			{
				entry.prefixLabel = ConstructEnvironmentSettingPath("Environment Reflections > Cubemap") + 
									", set Environment Reflections > Source > Custom to see";
			}

			conjunction.referencedAtInfo.AddNewEntry(entry);
		}

		private static void CheckSun(TreeConjunction conjunction, long candidateInstanceId)
		{
			if (RenderSettings.sun == null || CSInstanceIdTools.GetId(RenderSettings.sun) != candidateInstanceId) return;

			var entry = new ReferencingEntryData
			{
				location = Location.SceneLightingSettings,
				prefixLabel = ConstructEnvironmentSettingPath("Sun Source")
			};

			conjunction.referencedAtInfo.AddNewEntry(entry);
		}

		private static void CheckRenderSettingsTexture(TreeConjunction conjunction, long candidateInstanceId)
		{
			renderSettings = renderSettings ? renderSettings : CSSettingsTools.GetInSceneRenderSettings();
			if (renderSettings == null) return;

			renderSettingsSo = renderSettingsSo ?? new SerializedObject(renderSettings);
			renderHaloField = renderHaloField ?? renderSettingsSo.FindProperty("m_HaloTexture");

			if (renderHaloField != null && renderHaloField.propertyType == SerializedPropertyType.ObjectReference)
			{
				if (CSInstanceIdTools.GetObjectReferenceId(renderHaloField) == candidateInstanceId)
				{
					var entry = new ReferencingEntryData
					{
						location = Location.SceneLightingSettings,
						prefixLabel = ConstructOtherSettingsPath("Halo Texture")
					};

					conjunction.referencedAtInfo.AddNewEntry(entry);
				}
			}
			else
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't find m_HaloTexture at the RenderSettings!"));
			}

			renderSpotField = renderSpotField ?? renderSettingsSo.FindProperty("m_SpotCookie");
			if (renderSpotField != null && renderSpotField.propertyType == SerializedPropertyType.ObjectReference)
			{
				if (CSInstanceIdTools.GetObjectReferenceId(renderSpotField) == candidateInstanceId)
				{
					var entry = new ReferencingEntryData
					{
						location = Location.SceneLightingSettings,
						prefixLabel = ConstructOtherSettingsPath("Spot Cookie")
					};

					conjunction.referencedAtInfo.AddNewEntry(entry);
				}
			}
			else
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't find m_SpotCookie at the RenderSettings!"));
			}

			/*var iterator = renderSettingsSo.GetIterator();
			while (iterator.Next(true))
			{
				if (iterator.propertyType == SerializedPropertyType.ObjectReference)
				{
					Debug.Log(iterator.propertyPath + " [" + iterator.objectReferenceValue + "]");
				}
			}*/
		}
		
		private static string ConstructEnvironmentSettingPath(string settingName)
		{
			return EnvironmentSettingsPath + settingName + ")";
		}
		
		private static string ConstructOtherSettingsPath(string settingName)
		{
			return OtherSettingsPath + settingName + ")";
		}
	}
}
