#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using System;
	using System.Collections.Generic;
	using EditorCommon.Tools;
	using Filters;
	using Issues;
	using Issues.Detectors;
	using Settings;
	using Tools;
	using UnityEditor;
	using UnityEngine;

	internal partial class IssuesTab
	{
		private static SortedDictionary<IssueGroup, IList<IIssueDetector>> detectorsGroups;
		private static SortedDictionary<IssueGroup, bool> detectorsGroupsFoldouts;
		
		private const string ScanButtonLabel = "Scan Project";
		private const string FixButtonLabel = "Fix fixable selected issues";

		private static readonly Texture ScanButtonIcon = CSEditorIcons.Search;
		private static readonly Texture FixButtonIcon = CSIcons.AutoFix;

		private readonly GUIContent[] collapsedIcons =
		{
			new GUIContent(ScanButtonIcon, ScanButtonLabel),
			new GUIContent(FixButtonIcon, FixButtonLabel),
		};
		
		protected override void DrawLeftColumnHeader()
		{
			using (new GUILayout.VerticalScope())
			{
				GUILayout.Space(10);
				if (UIHelpers.ImageButton(ScanButtonLabel, ScanButtonIcon))
				{
					EditorApplication.delayCall += StartSearch;
				}

				GUILayout.Space(5);

				if (UIHelpers.ImageButton(FixButtonLabel, FixButtonIcon))
				{
					EditorApplication.delayCall += StartFix;
				}
				GUILayout.Space(10);
			}
		}

		protected override void DrawLeftColumnBody()
		{
			// -----------------------------------------------------------------------------
			// filtering settings
			// -----------------------------------------------------------------------------

			using (new GUILayout.VerticalScope())
			{
				DrawWhereSection(); 
				GUILayout.Space(5);
				DrawWhatSection();
				GUILayout.Space(10);
				
				using (new GUILayout.HorizontalScope())
				{
					if (UIHelpers.ImageButton("Reset Settings", "Resets settings to defaults.", CSIcons.Restore))
					{
						ProjectSettings.Issues.Reset();
						UserSettings.Issues.Reset();
					}
				}
			}
		}
		
		private void DrawWhereSection()
		{
			using (new GUILayout.VerticalScope())
			{
				GUILayout.Label("<b><size=16>Where</size></b>", UIHelpers.richLabel);
				UIHelpers.Separator();
			}

			using (new GUILayout.VerticalScope())
			{
				GUILayout.Space(10);

				if (UIHelpers.ImageButton("Filters (" + ProjectSettings.Issues.GetFiltersCount() + ")", CSIcons.Filter))
				{
					IssuesFiltersWindow.Create();
				}

				GUILayout.Space(5);

				using (new GUILayout.VerticalScope())
				{
					DrawScenesGUI();
					DrawGameObjectsGUI();
				}

				GUILayout.Space(10);
			}
		}

		private void DrawScenesGUI()
		{
			using (new GUILayout.HorizontalScope())
			{
				ProjectSettings.Issues.lookInScenes = EditorGUILayout.ToggleLeft(new GUIContent("Scenes",
						"Uncheck to exclude all scenes from search or select filtering level:\n\n" +
						"All Scenes: all project scenes with respect to configured filters.\n" +
						"Included Scenes: scenes included via Manage Filters > Scene Includes.\n" +
						"Current Scene: currently opened scene including any additional loaded scenes."),
					ProjectSettings.Issues.lookInScenes, GUILayout.Width(70));
				GUI.enabled = ProjectSettings.Issues.lookInScenes;
				ProjectSettings.Issues.scenesSelection = (IssuesFinderSettings.ScenesSelection)EditorGUILayout.EnumPopup(ProjectSettings.Issues.scenesSelection);
				GUI.enabled = true;
			}

			ProjectSettings.Issues.lookInAssets = EditorGUILayout.ToggleLeft(new GUIContent("File assets", "Uncheck to exclude all file assets like prefabs, ScriptableObjects and such from the search. Check readme for additional details."), ProjectSettings.Issues.lookInAssets);
			ProjectSettings.Issues.lookInProjectSettings = EditorGUILayout.ToggleLeft(new GUIContent("Project Settings", "Uncheck to exclude project settings file assets like PlayerSettings and such from the search."), ProjectSettings.Issues.lookInProjectSettings);
		}

		private void DrawGameObjectsGUI()
		{
			UIHelpers.Separator(5);

			var canScanGamObjects = ProjectSettings.Issues.lookInScenes || ProjectSettings.Issues.lookInAssets;
			GUI.enabled = canScanGamObjects;
			var scanGameObjects = UIHelpers.ToggleFoldout(ref ProjectSettings.Issues.scanGameObjects, ref UserSettings.Issues.scanGameObjectsFoldout, new GUIContent("Game Objects", "Specify if you wish to look for GameObjects issues."), GUILayout.Width(110));
			GUI.enabled = scanGameObjects && canScanGamObjects;
			if (UserSettings.Issues.scanGameObjectsFoldout)
			{
				UIHelpers.IndentLevel();
				ProjectSettings.Issues.touchInactiveGameObjects = EditorGUILayout.ToggleLeft(new GUIContent("Inactive Game Objects", "Uncheck to exclude all inactive Game Objects from the search."), ProjectSettings.Issues.touchInactiveGameObjects);
				ProjectSettings.Issues.touchDisabledComponents = EditorGUILayout.ToggleLeft(new GUIContent("Disabled Components", "Uncheck to exclude all disabled Components from the search."), ProjectSettings.Issues.touchDisabledComponents);
				UIHelpers.UnIndentLevel();
			}
			GUI.enabled = true;
		}

		private void DrawWhatSection()
		{
			InitDetectorsGroups();
				
			// change vars affecting OnGUI layout at the EventType.Layout
			// to avoid errors while painting OnGUI
			if (Event.current.type == EventType.Layout)
			{
				FillDetectorsGroups(IssuesFinderDetectors.detectors?.extensions);
				FillDetectorsGroupsFoldouts();
			}
			
			using (new GUILayout.VerticalScope())
			{
				DrawSettingsSearchSectionHeader(SettingsSearchSection.All, "<b><size=16>What</size></b>");
				UIHelpers.Separator();
				
				using (new GUILayout.VerticalScope())
				{
					GUILayout.Space(10);
					DrawDetectors();
					GUILayout.Space(10);
				}
			}
		}

		private void DrawDetectors()
		{
			if (detectorsGroups == null || detectorsGroupsFoldouts == null)
				return;
			
			foreach (var detectorsGroup in detectorsGroups)
			{
				if (detectorsGroup.Key != IssueGroup.Global)
				{
					if (!DrawGroupFoldout(detectorsGroup.Key, detectorsGroup.Value))
						continue;
					
					UIHelpers.IndentLevel();
					GUILayout.Space(5);
				}
				
				foreach (var detector in detectorsGroup.Value)
				{
					DrawDetectorSetting(detector);
				}

				if (detectorsGroup.Key != IssueGroup.Global)
					UIHelpers.UnIndentLevel();
				
				GUILayout.Space(5);
			}
		}

		private bool DrawGroupFoldout(IssueGroup group, IList<IIssueDetector> detectorsInGroup)
		{
			bool foldout;
			using (new GUILayout.HorizontalScope(UIHelpers.inspectorTitlebar))
			{
				var allEnabled = true;
				var allDisabled = true;

				foreach (var detector in detectorsInGroup)
				{
					if (!detector.Enabled)
						allEnabled = false;
					else
						allDisabled = false;
				}

				var mixedSelection = !(allEnabled || allDisabled);
				
				var guiContent = new GUIContent(" " + CSEditorTools.NicifyName(group.ToString()) + " Issues",
					GetGroupIcon(group));
				foldout = detectorsGroupsFoldouts[group];
				var toggle = mixedSelection || allEnabled;

				UIHelpers.ToggleFoldout(ref toggle, mixedSelection, ref foldout, 
					out var toggleChanged, out var foldoutChanged,
					guiContent,
					UIHelpers.richFoldout,
					GUILayout.ExpandWidth(true));
				
				if (toggleChanged)
					ProjectSettings.Issues.SwitchDetectors(detectorsInGroup, toggle);
				
				if (foldoutChanged)
					UserSettings.Issues.SetGroupFoldout(group, foldout);
				
				var (enabled, total) = GetDetectorsCount(detectorsInGroup);
				var rect = GUILayoutUtility.GetLastRect();
				var counterRect = rect;
				counterRect.xMin = counterRect.xMax - 50;
				DrawDetectorsCounter(counterRect, enabled, total);
			}

			return foldout;
		}

		private void DrawDetectorSetting(IIssueDetector detector)
		{
			using (var change = new EditorGUI.ChangeCheckScope())
			{
				using (new GUILayout.HorizontalScope())
				{
					var enabled = detector.Enabled;
					
					//DrawSeverityIcon(detector.Info.Severity, CSColorTools.DimmedColor);
					
					var originalLabelWidth = EditorGUIUtility.labelWidth;
					var originalFieldWidth = EditorGUIUtility.fieldWidth;
					EditorGUIUtility.labelWidth = 1;
					EditorGUIUtility.fieldWidth = 1;
					
					var guiContent = detector.Info.GetGUIContent();
					var value = EditorGUILayout.ToggleLeft(guiContent, enabled, GUILayout.ExpandWidth(true));
					
					EditorGUIUtility.fieldWidth = originalFieldWidth;
					EditorGUIUtility.labelWidth = originalLabelWidth;
					
					if (change.changed)
						detector.Enabled = value;
					
					DrawSeverityIcon(detector.Info.Severity, CSColorTools.BrightGreyDimmed);
					
					GUILayout.Space(2);
				}
			}
		}

		private void InitDetectorsGroups()
		{
			detectorsGroups ??= new SortedDictionary<IssueGroup, IList<IIssueDetector>>();
		}

		private void FillDetectorsGroups(IList<IIssueDetector> detectors)
		{
			if (detectorsGroups == null || detectorsGroups.Count > 0)
				return;
			
			foreach (var detector in detectors)
			{
				if (!detectorsGroups.ContainsKey(detector.Info.Group))
				{
					detectorsGroups.Add(detector.Info.Group, new List<IIssueDetector>());
				}
				detectorsGroups[detector.Info.Group].Add(detector);
			}
		}
		
		private void FillDetectorsGroupsFoldouts()
		{
			detectorsGroupsFoldouts ??= new SortedDictionary<IssueGroup, bool>();
			detectorsGroupsFoldouts.Clear();

			foreach (var group in detectorsGroups)
			{
				detectorsGroupsFoldouts[group.Key] = UserSettings.Issues.GetGroupFoldout(group.Key);
			}
		}

		private void DrawSettingsSearchSectionHeader(SettingsSearchSection section, string caption)
		{
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.Label(caption, UIHelpers.richLabel, GUILayout.Width(100));
				GUILayout.FlexibleSpace();
				
				var (enabled, total) = GetDetectorsCount();
				var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(50));
				rect.height += 2;
				DrawDetectorsCounter(rect, enabled, total);
				
				var menuContent = new GUIContent(CSEditorIcons.Menu);
				var menuRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(20));
				menuRect.y += 2; // Adjust vertical position to center the icon
				
				if (GUI.Button(menuRect, menuContent, UIHelpers.BuiltinIconButtonStyle))
				{
					var menu = new GenericMenu();
					menu.AddItem(new GUIContent("Enable All"), false, () => SettingsSectionGroupSwitch(section, true));
					menu.AddItem(new GUIContent("Disable All"), false, () => SettingsSectionGroupSwitch(section, false));
					menu.ShowAsContext();
				}
			}

			using (new GUILayout.HorizontalScope())
			{
				GUILayout.Space(10);
				UIHelpers.Separator();
				GUILayout.Space(10);
			}
		}

		private void SettingsSectionGroupSwitch(SettingsSearchSection section, bool enable)
		{
			switch (section)
			{
				case SettingsSearchSection.Common:
					ProjectSettings.Issues.SwitchCommon(enable);
					break;
				case SettingsSearchSection.Neatness:
					ProjectSettings.Issues.SwitchNeatness(enable);
					break;
				case SettingsSearchSection.All:
					ProjectSettings.Issues.SwitchAllIssues(enable);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		
		private Texture GetGroupIcon(IssueGroup group)
		{
			switch (group)
			{
				case IssueGroup.Asset:
					return CSIcons.Reveal;
				case IssueGroup.GameObject:
					return CSEditorIcons.GameObject;
				case IssueGroup.Component:
					return CSEditorIcons.Script;
				case IssueGroup.ProjectSettings:
					return CSEditorIcons.Settings;
				default:
					return null;
			}
		}

		private (int enabled, int total) GetDetectorsCount(IList<IIssueDetector> detectors = null)
		{
			if (detectors == null)
				detectors = IssuesFinderDetectors.detectors?.extensions;
				
			if (detectors == null)
				return (0, 0);
				
			var enabled = 0;
			var total = detectors.Count;
			
			foreach (var detector in detectors)
			{
				if (detector.Enabled)
					enabled++;
			}
			
			return (enabled, total);
		}

		private void DrawDetectorsCounter(Rect rect, int enabled, int total)
		{
			var content = $"{enabled} / {total}";
			var color = GUI.color;
			GUI.color = CSColorTools.DimmedColor;
			var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
			GUI.Label(rect, content, style);
			GUI.color = color;
		}
	}
}