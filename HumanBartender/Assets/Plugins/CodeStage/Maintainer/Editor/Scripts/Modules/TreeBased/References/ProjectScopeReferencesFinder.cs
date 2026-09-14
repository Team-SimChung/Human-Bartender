#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.References
{
	using Core;
	using Entry;
	using Routines;
	using Settings;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using Tools;
	using UI;
	using UnityEditor;
	using UnityEngine;
	using Debug = UnityEngine.Debug;
	using Object = UnityEngine.Object;

	internal static class ProjectScopeReferencesFinder
	{
		private static readonly List<AssetConjunctions> ConjunctionInfoList = new List<AssetConjunctions>();

		public static AssetConjunctions currentAssetConjunctions;

		public static ProjectReferenceItem[] FindAssetReferencesFromResults(string asset)
		{
			return FindAssetsReferences(new []{FilterItem.Create(asset, FilterKind.Path)}, true, true);
		}

		public static ProjectReferenceItem[] FindAssetsReferences(FilterItem[] assets, bool ignoreClearOption, bool showResults)
		{
			if (UserSettings.References.selectedFindClearsProjectResults && !ignoreClearOption)
			{
				SearchResultsStorage.ProjectReferencesLastSearched = new FilterItem[0];
				SearchResultsStorage.ProjectReferencesSearchResults = new ProjectReferenceItem[0];
			}

			var lastSearched = SearchResultsStorage.ProjectReferencesLastSearched;
			var newItem = false;

			foreach (var asset in assets)
			{
				newItem |= CSFilterTools.TryAddNewItemToFilters(ref lastSearched, asset);
			}

			if (assets.Length == 1)
			{
				ProjectReferencesTab.AutoSelectPath = assets[0].value;
			}

			if (newItem)
			{
				return FindAssetsReferences(lastSearched, assets, showResults);
			}

			if (showResults)
				MaintainerWindow.ShowAssetReferences();

			return SearchResultsStorage.ProjectReferencesSearchResults;
		}

		public static ProjectReferenceItem[] FindAssetsReferences(FilterItem[] allTargetAssets, FilterItem[] newTargetAssets, bool showResults = true)
		{
			var results = new List<ProjectReferenceItem>();

			ConjunctionInfoList.Clear();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

			try
			{
				var sw = Stopwatch.StartNew();
				var initialAllocatedMemory = CSEditorTools.GetCurrentAllocatedMemoryBytes();
				var peakAllocatedMemory = initialAllocatedMemory;

				CSEditorTools.lastRevealSceneOpenResult = null;
				EntryGenerator.ResetCachedObjects();

				var searchCanceled = LookForAssetsReferences(allTargetAssets, results);
				CSEditorTools.CapturePeakAllocatedMemory(ref peakAllocatedMemory);
				sw.Stop();

				EditorUtility.ClearProgressBar();

				if (!searchCanceled)
				{
					var resultsCount = results.Count;
					if (resultsCount <= 1)
					{
						ProjectReferencesTab.AutoSelectPath = null;
						MaintainerWindow.ShowNotification("Nothing found!");
					}
					else
					{
						if (newTargetAssets != null && newTargetAssets.Length > 0)
						{
							var found = false;
							foreach (var result in results)
							{
								if (result.depth == 0 && CSFilterTools.IsValueMatchesAnyFilter(result.assetPath, newTargetAssets))
								{
									found = true;
									break;
								}
							}

							if (!found)
							{
								ProjectReferencesTab.AutoSelectPath = null;
								MaintainerWindow.ShowNotification("Nothing found!");
							}
							else
							{
								MaintainerWindow.ClearNotification();
							}
						}
						else
						{
							MaintainerWindow.ClearNotification();
						}
					}

					Debug.Log(Maintainer.ConstructLog("Results: " + (resultsCount - 1) +
													  " items found in " + sw.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
													  " seconds, " + CSEditorTools.FormatMaxReservedMemory(initialAllocatedMemory, peakAllocatedMemory) + ".", ReferencesFinder.ModuleName));
					
					SaveLastSearched(results);
					SearchResultsStorage.ProjectReferencesSearchResults = results.ToArray();
				}
				else
				{
					Debug.Log(Maintainer.ConstructLog("Search canceled by user!", ReferencesFinder.ModuleName));
				}
				
				if (showResults)
					MaintainerWindow.ShowAssetReferences();
			}
			catch (Exception e)
			{
				Maintainer.PrintExceptionForSupport("Something went wrong while looking for asset references!", ReferencesFinder.ModuleName, e);
				EditorUtility.ClearProgressBar();
			}

			EntryGenerator.ResetCachedObjects();
			
			return results.ToArray();
		}

		public static string[] GetSelectedAssets()
		{
			var selectedIDs = CSInstanceIdTools.GetSelectionIds();
			return GetAssetsFromInstances(selectedIDs);
		}

		public static string[] GetAssetsFromInstances(long[] instanceIDs)
		{
			var paths = new List<string>(instanceIDs.Length);

			foreach (var id in instanceIDs)
			{
				if (CSInstanceIdTools.IsSubAsset(id)) continue;
				var path = CSInstanceIdTools.GetAssetPath(id);
				path = CSPathTools.EnforceSlashes(path);
				if (!File.Exists(path) && !Directory.Exists(path)) continue;
				paths.Add(path);
			}

			return paths.ToArray();
		}

		private static bool LookForAssetsReferences(FilterItem[] selectedAssets, List<ProjectReferenceItem> results)
		{
			var canceled = !CSSceneUtils.SaveCurrentModifiedScenes(false);

			if (!canceled)
			{
				var map = AssetsMap.GetUpdated(out canceled);
				if (map == null) return canceled;

				var count = map.assets.Count;
				var root = new ProjectReferenceItem
				{
					id = results.Count,
					name = "root",
					depth = -1
				};
				results.Add(root);

				for (var i = 0; i < count; i++)
				{
					if (EditorUtility.DisplayCancelableProgressBar(
						    string.Format(ReferencesFinder.ProgressCaption, 1, ReferencesFinder.PhasesCount),
						    string.Format(ReferencesFinder.ProgressText, "Building references tree", i + 1, count),
						    (float) i / count))
					{
						canceled = true;
						break;
					}

					var assetInfo = map.assets[i];

					// excludes settings assets from the list depth 0 items
					if (assetInfo.Origin == AssetOrigin.Settings) continue;

					// excludes all assets except selected ones from the list depth 0 items, if any was selected
					if (selectedAssets != null)
					{
						if (!CSFilterTools.IsValueMatchesAnyFilter(assetInfo.Path, selectedAssets)) continue;
					}

					if (CSFilterTools.HasEnabledFilters(ProjectSettings.References.pathIncludesFilters))
					{
						// excludes all root assets except included ones
						if (!CSFilterTools.IsValueMatchesAnyFilter(assetInfo.Path, ProjectSettings.References.pathIncludesFilters)) continue;
					}

					// excludes ignored root asset
					if (CSFilterTools.IsValueMatchesAnyFilter(assetInfo.Path, ProjectSettings.References.pathIgnoresFilters)) continue;

					var branchElements = new List<ProjectReferenceItem>();
					ProjectScopeReferencesTreeBuilder.BuildTreeBranch(assetInfo, 0, results.Count, ConjunctionInfoList, branchElements);
					results.AddRange(branchElements);
				}
			}

			if (!canceled)
				canceled = ProjectEntryFinder.FillProjectScopeReferenceEntries(ConjunctionInfoList, TryAddEntryToMatchedConjunctions);

			// Release cached asset objects to free memory after reference filling is complete
			foreach (var conjunctionInfo in ConjunctionInfoList)
			{
				foreach (var conjunction in conjunctionInfo.conjunctions)
					conjunction.referencedAsset?.ClearAllAssetObjectsCache();
			}
			EditorUtility.UnloadUnusedAssetsImmediate();

			// TODO: remove this work-around when this bug will be fixed:
			// https://issuetracker.unity3d.com/issues/assets-used-in-components-of-a-nested-prefab-are-counted-as-direct-dependencies-of-all-higher-level-nested-prefabs
			for (var i = results.Count - 1; i >= 0; i--)
			{
				var result = results[i];
				var resultEntries = result.referencingEntries;
				if (resultEntries == null || resultEntries.Length == 0)
					continue;

				foreach (var referencingEntry in resultEntries)
				{
					if (referencingEntry.Location == Location.NotFound && result.assetTypeName == "GameObject")
					{
						results.RemoveAt(i);
						break;
					}
				}
			}

			if (!canceled)
				AssetsMap.Save();

			if (canceled)
				ProjectReferencesTab.AutoSelectPath = null;

			return canceled;
		}

		private static void TryAddEntryToMatchedConjunctions(Object lookAt, long lookForInstanceId, EntryAddSettings settings)
		{
			var lookAtGameObject = lookAt as GameObject;

			for (var i = 0; i < currentAssetConjunctions.conjunctions.Count; i++)
			{
				var conjunction = currentAssetConjunctions.conjunctions[i];
				var referencedAssetObjects = conjunction.referencedAsset.GetAllAssetObjects();

				var match = false;
				for (var j = 0; j < referencedAssetObjects.Length; j++)
				{
					if (referencedAssetObjects[j] != lookForInstanceId) continue;

					match = true;
					break;
				}

				if (!match) continue;

				var newEntry = EntryGenerator.CreateNewReferenceEntry(EntryFinder.currentLocation, lookAt, lookAtGameObject, settings);

				conjunction.referencedAtInfo.AddNewEntry(newEntry);
			}
		}

		private static void SaveLastSearched(List<ProjectReferenceItem> results)
		{
			var resultsCount = results.Count;
			var showProgress = resultsCount > 500000;

			if (showProgress) 
				EditorUtility.DisplayProgressBar(ReferencesFinder.ModuleName, "Parsing results...", 0);

			var rootItems = new List<FilterItem>(resultsCount);
			for (var i = 0; i < resultsCount; i++)
			{
				if (showProgress) EditorUtility.DisplayProgressBar(ReferencesFinder.ModuleName, "Parsing results...", (float)i / resultsCount);

				var result = results[i];
				if (result.depth != 0) 
					continue;
				rootItems.Add(FilterItem.Create(result.assetPath, FilterKind.Path));
			}
			SearchResultsStorage.ProjectReferencesLastSearched = rootItems.ToArray();
		}
	}
}
