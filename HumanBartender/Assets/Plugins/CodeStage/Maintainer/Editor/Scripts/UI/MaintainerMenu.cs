#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using System.Collections.Generic;
	using System.IO;
	using Cleaner;
	using Issues;
	using Tools;
	using References;
	using UnityEditor;
	using UnityEngine;
	using System.Linq;

	internal static class MaintainerMenu
	{
		private const string MenuSection = "⚙ Maintainer";

		private const string HierarchyMenu = "GameObject/";
		private const string ContextMenu = "CONTEXT/";
		private const string CodeStage = "Code Stage/";
		private const string SearchMaintainer = "🔍 Maintainer";

		private const string ReferencesFinderMenuName = "🔗 Find References";
		private const string DependenciesReferencesFinderMenuName = "🔗 Find Dependencies References";
		private const string IssuesFinderMenuName = "🐛 Find Issues";
		private const string ContextComponentMenu = ContextMenu + "Component/";
		private const string ScriptReferencesContextMenuName = SearchMaintainer + ": Script File References";
		private const string ComponentContextSceneReferencesMenuName = SearchMaintainer + ": References In Scene";
		private const string HierarchyContextSceneReferencesMenuName = SearchMaintainer + "/Selected Object References In Scene";
		private const string HierarchyContextOpenedScenesIssuesMenuName = SearchMaintainer + "/Find Issues In Opened Scenes";
		private const string HierarchyContextSceneReferencesWithComponentsMenuName = SearchMaintainer + "/Selected Object && Components References In Scene";
		private const string ScriptReferencesContextMenu = ContextComponentMenu + ScriptReferencesContextMenuName;
		private const string SceneReferencesContextMenu = ContextComponentMenu + ComponentContextSceneReferencesMenuName;
		private const string SceneReferencesHierarchyMenu = HierarchyMenu + HierarchyContextSceneReferencesMenuName;
		private const string SceneReferencesWithComponentsHierarchyMenu = HierarchyMenu + HierarchyContextSceneReferencesWithComponentsMenuName;
		private const string OpenedScenesIssuesMenu = HierarchyMenu + HierarchyContextOpenedScenesIssuesMenuName;
		private const string ProjectBrowserContextStart = "Assets/";
		private const string ProjectBrowserContextReferencesFinderName = MenuSection + "/" + ReferencesFinderMenuName;
		private const string ProjectBrowserContextReferencesFinderNoHotKey = ProjectBrowserContextStart + ProjectBrowserContextReferencesFinderName;
		private const string ProjectBrowserContextReferencesFinder = ProjectBrowserContextReferencesFinderNoHotKey + " %#&s";
		private const string ProjectBrowserContextDependenciesReferencesFinder = ProjectBrowserContextStart + MenuSection + "/" + DependenciesReferencesFinderMenuName;
		private const string ProjectBrowserContextIssuesFinderName = MenuSection + "/" + IssuesFinderMenuName;
		private const string ProjectBrowserContextIssuesFinder = ProjectBrowserContextStart + ProjectBrowserContextIssuesFinderName;
		private const string MainMenu = "Tools/" + CodeStage + MenuSection + "/";

		private static float lastMenuCallTimestamp;

		[MenuItem(MainMenu + "Show %#&`", false, 900)]
		private static void ShowWindow()
		{
			MaintainerWindow.Create();
		}

		[MenuItem(MainMenu + "About", false, 901)]
		private static void ShowAbout()
		{
			MaintainerWindow.ShowAbout();
		}

		[MenuItem(MainMenu + "Find Issues %#&f", false, 1000)]
		private static void FindAllIssues()
		{
			IssuesFinder.StartSearch(true);
		}
		
		[MenuItem(MainMenu + "Find Issues In Opened Scenes", false, 1002)]
		private static void FindAllIssuesInCurrentScenes()
		{
			IssuesFinder.StartSearchInOpenedScenes(true);
		}

		[MenuItem(MainMenu + "Find Garbage %#&g", false, 1015)]
		private static void FindAllGarbage()
		{
			ProjectCleaner.StartSearch(true);
		}

		[MenuItem(MainMenu + "Find All Assets References %#&r", false, 1020)]
		private static void FindAllReferences()
		{
			ReferencesFinder.FindAllAssetsReferences();
		}

		[MenuItem(ProjectBrowserContextReferencesFinder, true, 39)]
		public static bool ValidateFindReferences()
		{
			return ProjectScopeReferencesFinder.GetSelectedAssets().Length > 0;
		}

		[MenuItem(ProjectBrowserContextReferencesFinder, false, 39)]
		public static void FindReferences()
		{
			ReferencesFinder.FindSelectedAssetsReferences();
		}

		[MenuItem(ProjectBrowserContextDependenciesReferencesFinder, true, 40)]
		public static bool ValidateFindDependenciesReferences()
		{
			return ProjectScopeReferencesFinder.GetSelectedAssets().Length > 0;
		}

		[MenuItem(ProjectBrowserContextDependenciesReferencesFinder, false, 40)]
		public static void FindDependenciesReferences()
		{
			var selectedAssets = ProjectScopeReferencesFinder.GetSelectedAssets();
			var dependencies = AssetDatabase.GetDependencies(selectedAssets, false);
			
			// Filter out the original selected assets
			dependencies = dependencies.Where(d => !selectedAssets.Contains(d)).ToArray();
			
			if (dependencies.Length == 0)
			{
				MaintainerWindow.ShowAssetReferences();
				MaintainerWindow.ShowNotification("No direct dependencies found for selected assets");
				return;
			}
			
			ReferencesFinder.FindAssetsReferences(dependencies);
		}

		[MenuItem(ProjectBrowserContextIssuesFinder, true, 60)]
		public static bool ValidateFindIssues()
		{
			return ProjectScopeReferencesFinder.GetSelectedAssets().Length > 0;
		}

		[MenuItem(ProjectBrowserContextIssuesFinder, false, 60)]
		public static void FindIssues()
		{
			var selectedAssets = ProjectScopeReferencesFinder.GetSelectedAssets();
			
			if (selectedAssets.Length == 0)
			{
				MaintainerWindow.ShowNotification("No assets selected!");
				return;
			}
			
			// Use the new StartSearchInPaths API for efficient multi-path scanning
			IssuesFinder.StartSearchInPaths(selectedAssets, true);
		}

		[MenuItem(ScriptReferencesContextMenu, true, 144445)]
		public static bool ValidateFindScriptReferences(MenuCommand command)
		{
			var scriptPath = CSObjectTools.GetScriptPathFromObject(command.context);
			return !string.IsNullOrEmpty(scriptPath) && Path.GetExtension(scriptPath).ToLower() != ".dll";
		}

		[MenuItem(ScriptReferencesContextMenu, false, 144445)]
		public static void FindScriptReferences(MenuCommand command)
		{
			var scriptPath = CSObjectTools.GetScriptPathFromObject(command.context);
			ReferencesFinder.FindAssetReferences(scriptPath);
		}

		[MenuItem(SceneReferencesContextMenu, true, 144444)]
		public static bool ValidateFindComponentReferences(MenuCommand command)
		{
			return command.context is Component && !AssetDatabase.Contains(command.context);
		}

		[MenuItem(SceneReferencesContextMenu, false, 144444)]
		public static void FindComponentReferences(MenuCommand command)
		{
			HierarchyScopeReferencesFinder.FindComponentReferencesInHierarchy(command.context as Component);
		}
		
		[MenuItem(SceneReferencesHierarchyMenu, true, -100)]
		public static bool ValidateFindGameObjectReferences()
		{
			return Selection.gameObjects.Length > 0;
		}

		[MenuItem(SceneReferencesHierarchyMenu, false, -100)]
		public static void FindGameObjectReferences()
		{
			if (Time.unscaledTime.Equals(lastMenuCallTimestamp)) return;
			if (Selection.gameObjects.Length == 0) return;

			ReferencesFinder.FindObjectsReferencesInHierarchy(Selection.gameObjects);

			lastMenuCallTimestamp = Time.unscaledTime;
		}
		
		[MenuItem(SceneReferencesWithComponentsHierarchyMenu, true, -99)]
		public static bool ValidateFindGameObjectWithComponentsReferences()
		{
			return Selection.gameObjects.Length > 0;
		}

		[MenuItem(SceneReferencesWithComponentsHierarchyMenu, false, -99)]
		public static void FindGameObjectWithComponentsReferences()
		{
			if (Time.unscaledTime.Equals(lastMenuCallTimestamp)) return;
			if (Selection.gameObjects.Length == 0) return;

			ReferencesFinder.FindObjectsReferencesInHierarchy(Selection.gameObjects, true);

			lastMenuCallTimestamp = Time.unscaledTime;
		}
		
		[MenuItem(OpenedScenesIssuesMenu, false, -70)]
		public static void FindScenesIssues()
		{
			if (Time.unscaledTime.Equals(lastMenuCallTimestamp)) return;

			IssuesFinder.StartSearchInOpenedScenes(true);

			lastMenuCallTimestamp = Time.unscaledTime;
		}
	}
}