#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using Routines;
	using UnityEditor;
	using Debug = UnityEngine.Debug;
	using Settings;
	using Tools;
	using UI;

	/// <summary>
	/// Allows to find issues in your Unity project. See readme for details.
	/// </summary>
	public static class IssuesFinder
	{
		internal const string ModuleName = "Issues Finder";
		private const string ProgressCaption = ModuleName + ": phase {0} of {1} item {2} of {3}";

		internal static bool operationCanceled;

		internal static CSSceneUtils.OpenSceneResult lastOpenSceneResult;

		private static int recordsToFixCount;

		#region public methods

		/////////////////////////////////////////////////////////////////////////
		// public methods
		/////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// Starts issues search in opened scenes excluding file assets and project settings.
		/// </summary>
		/// Changes issues search settings and calls StartSearch() after that.
		/// <param name="showResults">Shows results in the Maintainer window if true.</param>
		/// <returns>Array of IssueRecords in case you wish to manually iterate over them and make custom report.</returns>
		public static IssueRecord[] StartSearchInOpenedScenes(bool showResults)
		{
			ProjectSettings.Issues.scenesSelection = IssuesFinderSettings.ScenesSelection.OpenedOnly;
			ProjectSettings.Issues.lookInScenes = true;
			ProjectSettings.Issues.lookInAssets = false;
			ProjectSettings.Issues.scanGameObjects = true;
			ProjectSettings.Issues.lookInProjectSettings = false;
			
			return StartSearch(showResults);
		}
		
		/// <summary>
		/// Starts issues search in the specified path only (file or folder).
		/// </summary>
		/// <param name="path">The path to scan (e.g., "Assets/MyFolder", "Assets/MyScript.cs", or "Packages/com.test.package")</param>
		/// <param name="showResults">Shows results in the Maintainer window if true.</param>
		/// <param name="includeSubfolders">If true, scans subfolders; if false, only scans direct children (default: true). Only applies to folder paths.</param>
		/// <returns>Array of IssueRecords in case you wish to manually iterate over them and make custom report.</returns>
		/// <remarks>
		/// Changes issues search settings to scan only the specified path and calls StartSearch() after that.
		/// Automatically detects whether the path is a file or folder and applies appropriate filtering.
		/// For folders, includes scenes in the specified path but excludes build scenes.
		/// All original settings are restored after the scan completes.
		/// </remarks>
		public static IssueRecord[] StartSearchInPath(string path, bool showResults, bool includeSubfolders = true)
		{
			if (string.IsNullOrEmpty(path))
			{
				MaintainerWindow.ShowNotification("Path cannot be empty!");
				return null;
			}
			
			return StartSearchInPaths(new[] { path }, showResults, includeSubfolders);
		}
		
		/// <summary>
		/// Starts issues search in the specified paths only (files or folders).
		/// </summary>
		/// <param name="paths">The paths to scan (e.g., ["Assets/MyFolder", "Assets/MyScript.cs", "Packages/com.test.package"])</param>
		/// <param name="showResults">Shows results in the Maintainer window if true.</param>
		/// <param name="includeSubfolders">If true, scans subfolders; if false, only scans direct children (default: true). Only applies to folder paths.</param>
		/// <returns>Array of IssueRecords in case you wish to manually iterate over them and make custom report.</returns>
		/// <remarks>
		/// Changes issues search settings to scan only the specified paths and calls StartSearch() after that.
		/// Automatically detects whether each path is a file or folder and applies appropriate filtering.
		/// For folders, includes scenes in the specified paths but excludes build scenes.
		/// All original settings are restored after the scan completes.
		/// More efficient than calling StartSearchInPath multiple times as it performs a single scan operation.
		/// </remarks>
		public static IssueRecord[] StartSearchInPaths(string[] paths, bool showResults, bool includeSubfolders = true)
		{
			if (paths == null || paths.Length == 0)
			{
				MaintainerWindow.ShowNotification("Paths array cannot be null or empty!");
				return null;
			}
			
			// Filter out null, empty, or whitespace-only paths
			var validPaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray();
			if (validPaths.Length == 0)
			{
				MaintainerWindow.ShowNotification("No valid paths provided!");
				return null;
			}
			
			// Save original settings
			var originalPathIncludesFilters = ProjectSettings.Issues.pathIncludesFilters;
			var originalSceneIncludesFilters = ProjectSettings.Issues.sceneIncludesFilters;
			var originalLookInAssets = ProjectSettings.Issues.lookInAssets;
			var originalLookInScenes = ProjectSettings.Issues.lookInScenes;
			var originalLookInProjectSettings = ProjectSettings.Issues.lookInProjectSettings;
			var originalScanGameObjects = ProjectSettings.Issues.scanGameObjects;
			var originalScenesSelection = ProjectSettings.Issues.scenesSelection;
			var originalIncludeScenesInBuild = ProjectSettings.Issues.includeScenesInBuild;
			
			try
			{
				// Configure settings for path-specific scan
				ProjectSettings.Issues.lookInAssets = true;
				ProjectSettings.Issues.lookInScenes = true; // Include scenes in the specified paths
				ProjectSettings.Issues.lookInProjectSettings = false;
				ProjectSettings.Issues.scanGameObjects = true;
				ProjectSettings.Issues.scenesSelection = IssuesFinderSettings.ScenesSelection.IncludedOnly; // Only scan included scenes
				ProjectSettings.Issues.includeScenesInBuild = false; // Exclude build scenes
				
				// Create filters for all paths
				var pathFilters = new List<Core.FilterItem>();
				var sceneFilters = new List<Core.FilterItem>();
				foreach (var path in validPaths)
				{
					Core.FilterItem pathFilter;
					Core.FilterItem sceneFilter;
					if (System.IO.Directory.Exists(path))
					{
						// It's a folder - use Directory filter
						pathFilter = Core.FilterItem.Create(path, Core.FilterKind.Directory, false, !includeSubfolders);
						sceneFilter = Core.FilterItem.Create(path, Core.FilterKind.Directory, false, !includeSubfolders);
					}
					else
					{
						// It's a file - use Path filter for exact match
						pathFilter = Core.FilterItem.Create(path, Core.FilterKind.Path, false, true);
						sceneFilter = Core.FilterItem.Create(path, Core.FilterKind.Path, false, true);
					}
					pathFilters.Add(pathFilter);
					sceneFilters.Add(sceneFilter);
				}
				
				ProjectSettings.Issues.pathIncludesFilters = pathFilters.ToArray();
				ProjectSettings.Issues.sceneIncludesFilters = sceneFilters.ToArray();
				
				return StartSearch(showResults);
			}
			finally
			{
				// Restore original settings
				ProjectSettings.Issues.pathIncludesFilters = originalPathIncludesFilters;
				ProjectSettings.Issues.sceneIncludesFilters = originalSceneIncludesFilters;
				ProjectSettings.Issues.lookInAssets = originalLookInAssets;
				ProjectSettings.Issues.lookInScenes = originalLookInScenes;
				ProjectSettings.Issues.lookInProjectSettings = originalLookInProjectSettings;
				ProjectSettings.Issues.scanGameObjects = originalScanGameObjects;
				ProjectSettings.Issues.scenesSelection = originalScenesSelection;
				ProjectSettings.Issues.includeScenesInBuild = originalIncludeScenesInBuild;
			}
		}
		
		/// <summary>
		/// Starts issues search and generates report. Maintainer window is not shown.
		/// Useful when you wish to integrate Maintainer in your build pipeline.
		/// </summary>
		/// <returns>Issues report, similar to the exported report from the Maintainer window.</returns>
		public static string SearchAndReport()
		{
			var foundIssues = StartSearch(false);
			return ReportsBuilder.GenerateReport(ModuleName, foundIssues);
		}

		/// <summary>
		/// Starts search with current settings.
		/// </summary>
		/// <param name="showResults">Shows results in the Maintainer window if true.</param>
		/// <returns>Array of IssueRecords in case you wish to manually iterate over them and make custom report.</returns>
		public static IssueRecord[] StartSearch(bool showResults)
		{
			if (!ProjectSettings.Issues.lookInScenes && !ProjectSettings.Issues.lookInAssets &&
			    !ProjectSettings.Issues.lookInProjectSettings)
			{
				MaintainerWindow.ShowNotification("Nowhere to search!");
				return null;
			}
			
			if (ProjectSettings.Issues.lookInScenes && ProjectSettings.Issues.scenesSelection != IssuesFinderSettings.ScenesSelection.OpenedOnly)
            {
				if (!CSSceneUtils.SaveCurrentModifiedScenes(true))
				{
					Debug.Log(Maintainer.ConstructLog("Issues search canceled by user!"));
					return null;
				}
			}
			
			var issues = new List<IssueRecord>();

			PrepareToBatchOperation();

			try
			{
				var sw = Stopwatch.StartNew();
				var initialAllocatedMemory = CSEditorTools.GetCurrentAllocatedMemoryBytes();
				var peakAllocatedMemory = initialAllocatedMemory;

				CSTraverseTools.ClearStats();
				
				var targetAssets = TargetCollector.CollectTargetAssets(out operationCanceled);
				CSEditorTools.CapturePeakAllocatedMemory(ref peakAllocatedMemory);
				/*foreach (var targetAsset in targetAssets)
				{
					Debug.Log(targetAsset.Path);
				}*/
				
				if (!operationCanceled)
				{
					TargetProcessor.SetIssuesList(issues);
					TargetProcessor.ProcessTargetAssets(targetAssets);
					CSEditorTools.CapturePeakAllocatedMemory(ref peakAllocatedMemory);
				}
				
				sw.Stop();
				
				if (!operationCanceled)
				{
					var checkedAssets = targetAssets.Length;
					var traverseStats = CSTraverseTools.GetStats();
					
					var result = string.Format(CultureInfo.InvariantCulture,
						"Results: {0} issues found in {1:0.000} seconds, {2}; Assets: {3}; Game Objects: {4}; Components: {5}; Properties: {6}",
						issues.Count, sw.Elapsed.TotalSeconds, CSEditorTools.FormatMaxReservedMemory(initialAllocatedMemory, peakAllocatedMemory),
						checkedAssets, traverseStats.gameObjectsTraversed, traverseStats.componentsTraversed,
						traverseStats.propertiesTraversed);

					Debug.Log(Maintainer.ConstructLog(result, ModuleName));
					
					SearchResultsStorage.IssuesSearchResults = issues.ToArray();
					if (showResults)
					{
						MaintainerWindow.ShowIssues();
						if (issues.Count == 0)
							MaintainerWindow.ShowNotification("No issues found!");
					}
				}
				else
				{
					Debug.Log(Maintainer.ConstructLog("Search canceled by user!", ModuleName));
				}
			}
			catch (Exception e)
			{
				Maintainer.PrintExceptionForSupport("Something went wrong while looking for issues!", ModuleName, e);
			}

			EditorUtility.ClearProgressBar();

			return issues.ToArray();
		}

		/// <summary>
		/// Starts fix of the issues found with StartSearch() method.
		/// </summary>
		/// <param name="recordsToFix">Pass records you wish to fix here or leave null to let it load last search results.</param>
		/// <param name="showResults">Shows results in the Maintainer window if true.</param>
		/// <param name="showConfirmation">Shows confirmation dialog before performing fix if true.</param>
		/// <returns>Array of IssueRecords which were fixed up.</returns>
		public static IssueRecord[] StartFix(IssueRecord[] recordsToFix = null, bool showResults = true,
			bool showConfirmation = true)
		{
			var records = recordsToFix;
			if (records == null)
			{
				records = SearchResultsStorage.IssuesSearchResults;
			}

			if (records.Length == 0)
			{
				Debug.Log(Maintainer.ConstructLog("Nothing to fix!"));
				return null;
			}

			recordsToFixCount = 0;

			foreach (var record in records)
			{
				if (record.selected) recordsToFixCount++;
			}

			if (recordsToFixCount == 0)
			{
				if (!Maintainer.SuppressDialogs)
				{
					EditorUtility.DisplayDialog(ModuleName, "Please select issues to fix!", "Ok");
				}
				return null;
			}

			if (!CSSceneUtils.SaveCurrentModifiedScenes(false))
			{
				Debug.Log(Maintainer.ConstructLog("Issues batch fix canceled by user!"));
				return null;
			}

			if (showConfirmation)
			{
				var shouldProceed = false;
				if (Maintainer.SuppressDialogs)
				{
					shouldProceed = true;
				}
				else if (EditorUtility.DisplayDialog("Confirmation",
						"Do you really wish to let Maintainer automatically fix " + recordsToFixCount + " issues?\n" +
						Maintainer.DataLossWarning, "Go for it!", "Cancel"))
				{
					shouldProceed = true;
				}

				if (!shouldProceed)
				{
					return null;
				}
			}

			try
			{
				var sw = Stopwatch.StartNew();

				var fixedRecords = new List<IssueRecord>(records.Length);
				var notFixedRecords = new List<IssueRecord>(records.Length);

				PrepareToBatchOperation();

				lastOpenSceneResult = null;
				CSEditorTools.lastRevealSceneOpenResult = null;

				IssuesFixer.FixRecords(records);

				foreach (var record in records)
				{
					if (record.fixResult != null && record.fixResult.Success)
					{
						fixedRecords.Add(record);
					}
					else
					{
						notFixedRecords.Add(record);
					}
				}

				records = notFixedRecords.ToArray();

				sw.Stop();

				if (!operationCanceled)
				{
					var results = fixedRecords.Count +
								  " issues fixed in " + sw.Elapsed.TotalSeconds.ToString("0.000") +
								  " seconds";

					Debug.Log(Maintainer.ConstructLog("Results: " + results, ModuleName));
					MaintainerWindow.ShowNotification(results);
				}
				else
				{
					Debug.Log(Maintainer.ConstructLog("Fix canceled by user!", ModuleName));
				}

				if (lastOpenSceneResult != null)
				{
					CSSceneUtils.SaveScene(lastOpenSceneResult.scene);
					CSSceneUtils.CloseOpenedSceneIfNeeded(lastOpenSceneResult);
					lastOpenSceneResult = null;
				}

				SearchResultsStorage.IssuesSearchResults = records;
				if (showResults) 
					MaintainerWindow.ShowIssues();

				return fixedRecords.ToArray();
			}
			catch (Exception e)
			{
				Maintainer.PrintExceptionForSupport("Something went wrong while fixing issues!", ModuleName, e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
			
			return null;
		}

		#endregion

		internal static bool ShowProgressBar(int currentPhase, int totalPhases, int currentItem, int totalItems, string info)
		{
			return ShowProgressBar(currentPhase, totalPhases, currentItem, totalItems, info, (float)currentItem / totalItems);
		}

		internal static bool ShowProgressBar(int currentPhase, int totalPhases, int currentItem, int totalItems, string info, float progress)
		{
			return EditorUtility.DisplayCancelableProgressBar(string.Format(ProgressCaption, currentPhase, totalPhases, currentItem + 1, totalItems), info, progress);
		}

		private static void PrepareToBatchOperation()
		{
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

			lastOpenSceneResult = null;
			CSEditorTools.lastRevealSceneOpenResult = null;
			operationCanceled = false;
		}
	}
}