#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using References;
	using UnityEngine;
	using Settings;
	using System;
	using EditorCommon.Tools;
	using Tools;
	using UnityEditor;

	public enum ReferenceFinderTab
	{
		Project = 0,
		Scene = 1,
	}

	internal partial class ReferencesTab : TwoColumnsTab
	{
		[NonSerialized]
		private ReferenceFinderTab currentTab;

		[NonSerialized]
		private readonly GUIContent[] tabsCaptions;
		
		[NonSerialized]
		private readonly GUIContent[] tabsIcons;

		[NonSerialized]
		private readonly ProjectReferencesTab projectTab;

		[NonSerialized]
		private readonly HierarchyReferencesTab hierarchyTab;

		protected override string CaptionName
		{
			get { return ReferencesFinder.ModuleName; }
		}

		protected override Texture CaptionIcon
		{
			get { return CSEditorIcons.Search; }
		}

		public ReferencesTab(MaintainerWindow window) : base(window)
		{
			projectTab = new ProjectReferencesTab(window);
			hierarchyTab = new HierarchyReferencesTab(window);
			tabsCaptions = new[] { projectTab.Caption, hierarchyTab.Caption };
			tabsIcons = new[] { new GUIContent(projectTab.Caption.image, projectTab.Caption.text), 
				new GUIContent(hierarchyTab.Caption.image, hierarchyTab.Caption.text) };

			projectTab.treePanel.DrawGUIBeforeSearchAction = () =>
			{
				DrawToolbar();
				GUILayout.Space(5);
			};
			hierarchyTab.treePanel.DrawGUIBeforeSearchAction = () =>
			{
				DrawToolbar();
				GUILayout.Space(5);
			};
		}

		private void DrawToolbar()
		{
			using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				// Draw tab switcher if panel is collapsed
				if (IsLeftPanelCollapsed)
				{
					using var change = new EditorGUI.ChangeCheckScope();

					var projectToggle = currentTab == ReferenceFinderTab.Project;
					projectToggle = GUILayout.Toggle(projectToggle, new GUIContent(tabsIcons[0]), UIHelpers.MiniButton, GUILayout.Width(UIHelpers.ToolbarButtonWidth));
					projectToggle = !GUILayout.Toggle(!projectToggle, new GUIContent(tabsIcons[1]), UIHelpers.MiniButton, GUILayout.Width(UIHelpers.ToolbarButtonWidth));
					
					if (change.changed)
					{
						currentTab = projectToggle ? ReferenceFinderTab.Project : ReferenceFinderTab.Scene;
						UserSettings.Instance.referencesFinder.selectedTab = currentTab;
						Refresh(false);
					}
				}

				var hasLastSearchResults = currentTab == ReferenceFinderTab.Project ? 
					SearchResultsStorage.ProjectReferencesLastSearched.Length > 0 :
					SearchResultsStorage.HierarchyReferencesLastSearched.Length > 0;

				GUI.enabled = hasLastSearchResults;

				if (UIHelpers.ImageButton("Refresh", CSIcons.Repeat, EditorStyles.toolbarButton, GUILayout.Width(UIHelpers.ToolbarButtonWidth)))
				{
					if (Event.current.control && Event.current.shift)
					{
						ReferencesFinder.debugMode = true;
						Event.current.Use();
					}
					else
					{
						ReferencesFinder.debugMode = false;
					}

					if (currentTab == ReferenceFinderTab.Project)
					{
						EditorApplication.delayCall += () =>
						{
							ProjectScopeReferencesFinder.FindAssetsReferences(SearchResultsStorage.ProjectReferencesLastSearched, null);
						};
					}
					else
					{
						EditorApplication.delayCall += () =>
						{
							var sceneObjects = CSObjectTools.GetObjectsFromInstanceIds(SearchResultsStorage.HierarchyReferencesLastSearched);
							HierarchyScopeReferencesFinder.FindHierarchyObjectsReferences(sceneObjects, null);
						};
					}
				}

				var hasResults = currentTab == ReferenceFinderTab.Project ? 
					SearchResultsStorage.ProjectReferencesSearchResults.Length >= 2 :
					SearchResultsStorage.HierarchyReferencesSearchResults.Length >= 2;

				GUI.enabled = hasResults;

				if (UIHelpers.ImageButton("Collapse all", CSIcons.Collapse, EditorStyles.toolbarButton, GUILayout.Width(UIHelpers.ToolbarButtonWidth)))
				{
					if (currentTab == ReferenceFinderTab.Project)
						projectTab.CollapseAllElements();
					else
						hierarchyTab.CollapseAllElements();
				}

				if (UIHelpers.ImageButton("Expand all", CSIcons.Expand, EditorStyles.toolbarButton, GUILayout.Width(UIHelpers.ToolbarButtonWidth)))
				{
					if (currentTab == ReferenceFinderTab.Project)
						projectTab.ExpandAllElements();
					else
						hierarchyTab.ExpandAllElements();
				}

				if (UIHelpers.ImageButton("Clear results", CSIcons.Clear, EditorStyles.toolbarButton, GUILayout.Width(UIHelpers.ToolbarButtonWidth)))
				{
					if (currentTab == ReferenceFinderTab.Project)
						projectTab.ClearResults();
					else
						hierarchyTab.ClearResults();
				}

				GUI.enabled = true;
			}
		}

		public override void Refresh(bool newData)
		{
			base.Refresh(newData);

			currentTab = UserSettings.Instance.referencesFinder.selectedTab;

			switch (currentTab)
			{
				case ReferenceFinderTab.Project:
					projectTab.Refresh(newData);
					break;
				case ReferenceFinderTab.Scene:
					hierarchyTab.Refresh(newData);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override bool DrawRightColumnCenter()
		{
			switch (currentTab)
			{
				case ReferenceFinderTab.Project:
					projectTab.DrawRightColumn();
					break;
				case ReferenceFinderTab.Scene:
					hierarchyTab.DrawRightColumn();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			return true;
		}

		protected override void DrawRightColumnBottom()
		{
			switch (currentTab)
			{
				case ReferenceFinderTab.Project:
					projectTab.DrawFooter();
					break;
				case ReferenceFinderTab.Scene:
					hierarchyTab.DrawFooter();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
