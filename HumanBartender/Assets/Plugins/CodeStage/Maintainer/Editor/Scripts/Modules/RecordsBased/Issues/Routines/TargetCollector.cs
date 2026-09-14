#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Routines
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Core;
	using EditorCommon.Tools;
	using Settings;
	using Tools;
	using UnityEditor;
	using UnityEngine;

	internal static class TargetCollector
	{
		internal static AssetInfo[] CollectTargetAssets(out bool canceled)
		{
			var map = AssetsMap.GetUpdated(out canceled);
			if (map == null)
			{
				if (!canceled)
					Debug.LogError(Maintainer.ConstructLog("Couldn't get updated assets map!"));
				return null;
			}

			EditorUtility.DisplayProgressBar(IssuesFinder.ModuleName, "Collecting input data...", 0);

			var supportedKinds = new List<AssetOrigin> {AssetOrigin.AssetsFolder, AssetOrigin.Settings, AssetOrigin.EmbeddedPackage};
			var assets = CSFilterTools.GetAssetInfosWithKinds(map.assets, supportedKinds);
			
			var result = new HashSet<AssetInfo>();

			try
			{
				var targetAssetTypes = new List<TypeFilter>();

				if (ProjectSettings.Issues.lookInScenes)
				{
					switch (ProjectSettings.Issues.scenesSelection)
					{
						case IssuesFinderSettings.ScenesSelection.All:
						{
							targetAssetTypes.Add(new TypeFilter(CSReflectionTools.sceneAssetType, false));
							TryIncludeUntitledScene(result);
							
							break;
						}
						case IssuesFinderSettings.ScenesSelection.IncludedOnly:
						{
							if (ProjectSettings.Issues.includeScenesInBuild)
							{
								var paths = CSSceneTools.GetBuildProfilesScenesPaths(!ProjectSettings.Issues.includeOnlyEnabledScenesInBuild);
								result.UnionWith(CSFilterTools.GetAssetInfosWithPaths(assets, paths));
							}

							var assetInfos = CSFilterTools.FilterAssetInfos(assets, ProjectSettings.Issues.sceneIncludesFilters);
							result.UnionWith(assetInfos);

							break;
						}
						case IssuesFinderSettings.ScenesSelection.OpenedOnly:
						{
							var paths = CSSceneUtils.GetScenesSetup().Select(scene => scene.path).ToArray();
							result.UnionWith(CSFilterTools.GetAssetInfosWithPaths(assets, paths));
							TryIncludeUntitledScene(result);

							break;
						}
						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				if (ProjectSettings.Issues.lookInAssets)
				{
					targetAssetTypes.Add(new TypeFilter(CSReflectionTools.scriptableObjectType));
					targetAssetTypes.Add(new TypeFilter(CSReflectionTools.textAssetType));
					targetAssetTypes.Add(new TypeFilter(CSReflectionTools.objectType, new []{CSReflectionTools.gameObjectType, CSReflectionTools.sceneAssetType}));
					targetAssetTypes.Add(new TypeFilter(null, false));
					
					if (ProjectSettings.Issues.scanGameObjects)
					{
						targetAssetTypes.Add(new TypeFilter(CSReflectionTools.gameObjectType, false));
					}
				}

				var filtered = CSFilterTools.FilterAssetInfos(
					assets,
					targetAssetTypes,
					CSFilterTools.GetEnabledFilters(ProjectSettings.Issues.pathIncludesFilters),
					CSFilterTools.GetEnabledFilters(ProjectSettings.Issues.pathIgnoresFilters)
				);

				result.UnionWith(filtered);

				if (ProjectSettings.Issues.lookInProjectSettings)
				{
					result.UnionWith(CSFilterTools.GetAssetInfosWithKind(assets, AssetOrigin.Settings));
				}
			}
			catch (Exception e)
			{
				Debug.LogError(Maintainer.ConstructLog($"Error collecting target assets: {e.Message}"));
				throw;
			}

			var resultArray = new AssetInfo[result.Count];
			result.CopyTo(resultArray);
			return resultArray;
		}

		private static void TryIncludeUntitledScene(HashSet<AssetInfo> result)
		{
			var untitledScene = CSSceneUtils.GetUntitledScene();
			if (!untitledScene.IsValid())
				return;

			var asset = AssetInfo.CreateUntitledScene();
			result.Add(asset);
		}
	}
}