#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using Core;
	using EditorCommon.Tools;
	using Filters;
	using References;
	using Settings;
	using UnityEditor;
	using UnityEngine;

	internal class ProjectReferencesTab : ReferencesChildTab
	{
		public static string AutoSelectPath { get; set; }

		protected override string CaptionName
		{
			get { return "Project Assets"; }
		}

		protected override Texture CaptionIcon
		{
			get { return CSEditorIcons.ProjectView; }
		}

		internal readonly ProjectReferencesTreePanel treePanel;

		public ProjectReferencesTab(MaintainerWindow window) : base(window)
		{
			treePanel = new ProjectReferencesTreePanel(window);
		}

		public void DrawLeftColumnHeader()
		{
			using (new GUILayout.HorizontalScope())
			{
				using (new GUILayout.VerticalScope())
				{
					GUILayout.Space(10);
					GUILayout.Label("<size=16><b>Search references of:</b></size>", UIHelpers.richLabel);
					UIHelpers.Separator();
					GUILayout.Space(10);

					if (UIHelpers.ImageButton("All assets",
						"Traverses whole project to find where all assets are referenced.", CSEditorIcons.Search))
					{
						if (Event.current.control && Event.current.shift)
						{
							ReferencesFinder.debugMode = true;
							AssetsMap.Delete();
							Event.current.Use();
						}
						else
						{
							ReferencesFinder.debugMode = false;
						}

						EditorApplication.delayCall += StartProjectReferencesScan;
					}

					if (ProjectScopeReferencesFinder.GetSelectedAssets().Length == 0)
					{
						GUI.enabled = false;
					}

					if (UIHelpers.ImageButton("Selected assets",
						"Adds selected Project View assets to the current search results.", CSEditorIcons.Search))
					{
						EditorApplication.delayCall += () => ReferencesFinder.FindSelectedAssetsReferences();
					}
				}
			}

			GUI.enabled = true;
		}

		public void DrawSettings()
		{
			GUILayout.Space(10);
			using (new GUILayout.HorizontalScope(/*UIHelpers.panelWithBackground*/))
			{
				using (new GUILayout.VerticalScope())
				{
					GUILayout.Label("<size=16><b>Settings</b></size>", UIHelpers.richLabel);
					UIHelpers.Separator();
					GUILayout.Space(10);

					if (UIHelpers.ImageButton("Filters (" + ProjectSettings.References.GetFiltersCount() + ")",
						CSIcons.Filter))
					{
						ReferencesFiltersWindow.Create();
					}

					using (new GUILayout.VerticalScope())
					{
						GUILayout.Space(5);

						using (var change = new EditorGUI.ChangeCheckScope())
						{
							var deepProjectSearch = GUILayout.Toggle(
								UserSettings.References.DeepProjectSearch,
								new GUIContent("Deep search",
									"Includes more items into the search, like fields with [HideInInspector] attribute or hidden system fields.\n" +
									"Can reduce search performance and produce confusing results in some scenarios.\n" +
									"Changing this setting will trigger Exact References Entries cleanup."));

							if (change.changed)
								UserSettings.References.DeepProjectSearch = deepProjectSearch;
						}

						using (var change = new EditorGUI.ChangeCheckScope())
						{
							UserSettings.References.showAssetsWithoutReferences = GUILayout.Toggle(
								UserSettings.References.showAssetsWithoutReferences,
								new GUIContent("Show assets without references",
									"Check to see all scanned assets in the list even if there was no any references to the asset found in project."));
							
							if (change.changed)
								Refresh(true);
						}

						UserSettings.References.selectedFindClearsProjectResults = GUILayout.Toggle(
							UserSettings.References.selectedFindClearsProjectResults,
							new GUIContent(@"Clear previous results",
								"Check to automatically clear last results on selected assets find both from context menu and main window.\n" +
								"Uncheck to add new results to the last results."));

						GUILayout.Space(3);
					}

					GUILayout.Space(10);
				}
			}
		}

		public void Refresh(bool newData)
		{
			treePanel.Refresh(newData);

			if (newData)
			{
				if (!string.IsNullOrEmpty(AutoSelectPath))
				{
					EditorApplication.delayCall += () =>
					{
						SelectItemWithPath(AutoSelectPath);
						AutoSelectPath = null;
					};
				}
			}
		}

		public void DrawRightColumn()
		{
			treePanel.Draw();
		}

		public void DrawFooter()
		{
			
		}

		internal override void ClearResults()
		{
			SearchResultsStorage.ProjectReferencesSearchResults = null;
			SearchResultsStorage.ProjectReferencesLastSearched = null;
			Refresh(true);
		}

		internal override void CollapseAllElements()
		{
			treePanel.CollapseAll();
		}

		internal override void ExpandAllElements()
		{
			treePanel.ExpandAll();
		}

		private void SelectItemWithPath(string autoSelectPath)
		{
			treePanel.SelectItemWithPath(autoSelectPath);
		}

		private void StartProjectReferencesScan()
		{
			window.RemoveNotification();
			ReferencesFinder.FindAllAssetsReferences();
			window.Focus();
		}
	}
}