#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

using UnityEditor;

namespace CodeStage.Maintainer.Demo
{
	using UI;
	using UnityEngine;

	[CustomEditor(typeof(MaintainerDemoUI))]
	public class MaintainerDemoUIEditor : Editor
	{
		private GUIStyle titleStyle;
		private GUIStyle subtitleStyle;
		private GUIStyle descriptionStyle;
		private GUIStyle buttonStyle;

		public override void OnInspectorGUI()
		{
			InitializeStyles();
			
			GUILayout.BeginVertical();

			// Title
			GUILayout.Label("Greetings!", titleStyle);
			GUILayout.Space(10);

			// Issues Finder Section
			GUILayout.Label("Issues Finder", subtitleStyle);
			GUILayout.Label("Find and fix issues like missing scripts, missing references, and more.",
				descriptionStyle);
			if (GUILayout.Button("Check project for Issues", buttonStyle))
			{
				MaintainerWindow.ShowIssues();
			}

			GUILayout.Space(10);

			// References Finder Section
			GUILayout.Label("References Finder", subtitleStyle);
			GUILayout.Label("Locate references in your project by assets or objects in the scene.", descriptionStyle);
			if (GUILayout.Button("Find Asset References", buttonStyle))
			{
				MaintainerWindow.ShowAssetReferences();
			}

			if (GUILayout.Button("Find Object References", buttonStyle))
			{
				MaintainerWindow.ShowObjectReferences();
			}

			GUILayout.Space(10);

			// Project Cleaner Section
			GUILayout.Label("Project Cleaner", subtitleStyle);
			GUILayout.Label("Find unused assets in your project and clean them up with a few clicks.",
				descriptionStyle);
			if (GUILayout.Button("Let's clean it up!", buttonStyle))
			{
				MaintainerWindow.ShowCleaner();
			}

			GUILayout.Space(20);
			
			GUILayout.Label("You'll find more options at the Hierarchy & Project context menus and at the Top-level menu.",
				descriptionStyle);

			GUILayout.EndVertical();
		}

		private void InitializeStyles()
		{
			titleStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold
			};
			
			subtitleStyle = new GUIStyle(titleStyle)
			{
				fontSize = 18
			};

			descriptionStyle = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true, alignment = TextAnchor.MiddleCenter, fontSize = 14, richText = true
			};

			buttonStyle = new GUIStyle(GUI.skin.button)
			{
				fontSize = 16, alignment = TextAnchor.MiddleCenter, fixedHeight = 30
			};
		}
	}
}
