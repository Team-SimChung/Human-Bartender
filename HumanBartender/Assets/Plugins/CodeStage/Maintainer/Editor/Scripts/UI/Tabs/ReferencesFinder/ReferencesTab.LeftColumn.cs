#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using System;
	using EditorCommon.Tools;
	using References;
	using Settings;
	using UnityEditor;
	using UnityEngine;

	internal partial class ReferencesTab
	{
		protected override void DrawLeftColumnHeader()
		{
			using (new GUILayout.VerticalScope())
			{
				GUILayout.Space(10);

				using (new GUILayout.HorizontalScope())
				{
					GUILayout.Label("<size=16><b>Search scope</b></size>", UIHelpers.richLabel);
					GUILayout.FlexibleSpace();

					using (new GUILayout.VerticalScope())
					{	
						GUILayout.Space(6);
						if (GUILayout.Button(CSEditorIcons.Help, UIHelpers.BuiltinIconButtonStyle))
						{
							if (!Maintainer.SuppressDialogs)
							{
								EditorUtility.DisplayDialog(ReferencesFinder.ModuleName + " scopes help",
									"Use " + projectTab.Caption.text + " scope to figure out where any specific asset is referenced in whole project.\n\n" +
									"Use " + hierarchyTab.Caption.text + " scope to figure out where any specific Game Object or component is referenced in active scene or opened prefab.",
									"OK");
							}
						}
					}
				}

				using (new GUILayout.HorizontalScope())
				{
					UIHelpers.Separator();
				}

				GUILayout.Space(10);

				EditorGUI.BeginChangeCheck();
				using (new GUILayout.HorizontalScope())
				{
					currentTab = (ReferenceFinderTab)GUILayout.SelectionGrid((int)currentTab, tabsCaptions, 1,
						GUILayout.Height(56), GUILayout.ExpandWidth(true));
				}

				if (EditorGUI.EndChangeCheck())
				{
					UserSettings.Instance.referencesFinder.selectedTab = currentTab;
					Refresh(false);
				}

				switch (currentTab)
				{
					case ReferenceFinderTab.Project:
						projectTab.DrawLeftColumnHeader();
						break;
					case ReferenceFinderTab.Scene:
						hierarchyTab.DrawLeftColumnHeader();
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		protected override void DrawLeftColumnBody()
		{
			using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
			{
				switch (currentTab)
				{
					case ReferenceFinderTab.Project:
						projectTab.DrawSettings();
						break;
					case ReferenceFinderTab.Scene:
						hierarchyTab.DrawSettings();
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
	}
}