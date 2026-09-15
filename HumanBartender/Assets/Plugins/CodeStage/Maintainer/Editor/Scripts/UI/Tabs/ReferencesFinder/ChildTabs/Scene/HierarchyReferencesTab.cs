#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using Core;
	using EditorCommon.Tools;
	using References;
	using Settings;
	using Tools;
	using UnityEditor;
	using UnityEngine;
	using UnityEngine.SceneManagement;
	
#if UNITY_2021_2_OR_NEWER
	using UnityEditor.SceneManagement;
#else
	using UnityEditor.Experimental.SceneManagement;
#endif

	internal class HierarchyReferencesTab : ReferencesChildTab
	{
		public static ReferencingEntryData AutoSelectHierarchyReference { get; set; }

		protected override string CaptionName
		{
			get { return "Hierarchy Objects"; }
		}

		protected override Texture CaptionIcon
		{
			get { return CSEditorIcons.HierarchyView; }
		}

		internal readonly HierarchyReferencesTreePanel treePanel;

		public HierarchyReferencesTab(MaintainerWindow window):base(window)
		{
			treePanel = new HierarchyReferencesTreePanel();
		}

		public void DrawLeftColumnHeader()
		{
			var assetPath = SceneManager.GetActiveScene().path;
			var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
			if (prefabStage != null)
			{
				assetPath = prefabStage.assetPath;
			}
		}

		public void DrawSettings()
		{
			GUILayout.Space(10);

			using (new GUILayout.HorizontalScope())
			{
				using (new GUILayout.VerticalScope())
				{
					EditorGUILayout.HelpBox("Hold 'alt' key while dropping Game Objects to skip their components", MessageType.Info);
					GUILayout.Space(10);

					GUILayout.Label("<size=16><b>Settings</b></size>", UIHelpers.richLabel);
					UIHelpers.Separator();
					GUILayout.Space(10);
					
					UserSettings.References.DeepHierarchySearch = GUILayout.Toggle(
						UserSettings.References.DeepHierarchySearch,
						new GUIContent("Deep search",
							"Includes more items into the search, like fields with [HideInInspector] attribute or hidden system fields. Can reduce search performance and produce confusing results in some scenarios."));

					UserSettings.References.clearHierarchyResults = GUILayout.Toggle(
						UserSettings.References.clearHierarchyResults,
						new GUIContent(@"Clear previous results",
							"Check to automatically clear last results on any new search.\n" +
							"Uncheck to add new results to the last results."));

					GUILayout.Space(10);
				}
			}
		}

		public void Refresh(bool newData)
		{
			treePanel.Refresh(newData);

			if (newData)
			{
				if (AutoSelectHierarchyReference != null)
				{
					EditorApplication.delayCall += () =>
					{
						SelectRow(AutoSelectHierarchyReference);
						AutoSelectHierarchyReference = null;
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
			SearchResultsStorage.HierarchyReferencesSearchResults = null;
			SearchResultsStorage.HierarchyReferencesLastSearched = null;
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

		private void SelectRow(ReferencingEntryData reference)
		{
			treePanel.SelectRow(reference.objectId, reference.componentId);
		}
	}
}