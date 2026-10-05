#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using System;
	using Cleaner;
	using EditorCommon.Tools;
	using Settings;
	using UnityEditor;
	using UnityEngine;

	internal class MaintainerWindow : EditorWindow
	{
		internal enum MaintainerTab
		{
			Issues = 0,
			Cleaner = 1,
			References = 2,
			About = 3
		}

		private static MaintainerWindow windowInstance;

		[NonSerialized]
		private MaintainerTab currentTab;

		[NonSerialized]
		private GUIContent[] tabsCaptions;

		[NonSerialized]
		private IssuesTab issuesTab;

		[NonSerialized]
		private CleanerTab cleanerTab;

		[NonSerialized]
		private ReferencesTab referencesTab;

		[NonSerialized]
		private AboutTab aboutTab;

		[NonSerialized]
		private RateUsWidget rateUsWidget;

		[NonSerialized]
		private GUIStyle tabFirstStyle;

		[NonSerialized]
		private GUIStyle tabMidStyle;

		[NonSerialized]
		private GUIStyle tabLastStyle;

		[NonSerialized]
		private bool inited;

		public static MaintainerWindow Create()
		{
			windowInstance = GetWindow<MaintainerWindow>(false, "Maintainer", true);
			windowInstance.titleContent = new GUIContent(" Maintainer", CSIcons.Maintainer);
			windowInstance.Focus();

			return windowInstance;
		}

		public static void ShowForScreenshot()
		{
			var window = Create();
			window.minSize = new Vector2(768, 768);
		}

		public static void ShowIssues()
		{
			Create(MaintainerTab.Issues).Repaint();
		}

		public static void ShowCleaner()
		{
			AssetPreview.SetPreviewTextureCacheSize(50);
			ShowProjectCleanerWarning();

			Create(MaintainerTab.Cleaner).Repaint();
		}

		public static void ShowAssetReferences()
		{
			UserSettings.Instance.referencesFinder.selectedTab = ReferenceFinderTab.Project;
			Create(MaintainerTab.References).Repaint();
		}

		public static void ShowObjectReferences()
		{
			UserSettings.Instance.referencesFinder.selectedTab = ReferenceFinderTab.Scene;
			Create(MaintainerTab.References).Repaint();
		}

		public static void ShowAbout()
		{
			Create(MaintainerTab.About).Repaint();
		}

		public static void ShowNotification(string text)
		{
			EditorApplication.delayCall += () =>
			{
				if (windowInstance)
				{
					windowInstance.ShowNotification(new GUIContent(text));
				}
			};
		}

		public static void ClearNotification()
		{
			if (windowInstance)
			{
				windowInstance.RemoveNotification();
			}
		}

		public static void RepaintInstance()
		{
			if (windowInstance)
			{
				windowInstance.Repaint();
			}
		}

		private static MaintainerWindow Create(MaintainerTab tab)
		{
			windowInstance = Create();

			if (windowInstance.currentTab != tab)
			{
				windowInstance.currentTab = UserSettings.Instance.selectedTab = tab;
			}
			windowInstance.Refresh(true);

			return windowInstance;
		}

		private static void ShowProjectCleanerWarning()
		{
			if (UserSettings.Cleaner.firstTime)
			{
				if (!Maintainer.SuppressDialogs)
				{
					EditorUtility.DisplayDialog(ProjectCleaner.ModuleName, "Please note, this module can remove files and folders physically from your system.\nPlease always make a backup of your project before using Project Cleaner!\nUse it on your own peril, author is not responsible for any damage made due to the module usage!\nThis message shows only once.", "Dismiss");
				}
				UserSettings.Cleaner.firstTime = false;
			}
		}

		private void Init()
		{
			if (inited) return;

			CreateTabs();

			rateUsWidget = new RateUsWidget(
				"CodeStage.Maintainer.RateUs",
				MaintainerLinks.UasReviewsLink,
				MaintainerLinks.FeedbackFormLink,
				Repaint
			);

			Repaint();
			currentTab = UserSettings.Instance.selectedTab;

			Refresh(false);
			inited = true;
		}

		private void CreateTabs()
		{
			if (issuesTab == null)
				issuesTab = new IssuesTab(this);

			if (cleanerTab == null)
				cleanerTab = new CleanerTab(this);

			if (referencesTab == null)
				referencesTab = new ReferencesTab(this);

			if (aboutTab == null)
				aboutTab = new AboutTab(this);

			if (tabsCaptions == null)
			{
				tabsCaptions = new[] { issuesTab.Caption, cleanerTab.Caption, referencesTab.Caption, aboutTab.Caption };
			}
		}

		public void Refresh(bool newData)
		{
			switch (currentTab)
			{
				case MaintainerTab.Issues:
					issuesTab.Refresh(newData);
					break;
				case MaintainerTab.Cleaner:
					cleanerTab.Refresh(newData);
					break;
				case MaintainerTab.References:
					referencesTab.Refresh(newData);
					break;
				case MaintainerTab.About:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void Awake()
		{
			EditorApplication.quitting += OnQuit;
		}

		private void OnEnable()
		{
			hideFlags = HideFlags.HideAndDontSave;
			wantsMouseMove = true;
			windowInstance = this;
			Init();

			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
		}

		private void OnEditorUpdate()
		{
			if (rateUsWidget != null && rateUsWidget.NeedsRepaint)
				Repaint();
		}

		private void OnLostFocus()
		{
			ProjectSettings.Save();
		}

		private void OnGUI()
		{
			UIHelpers.SetupStyles();

			GUILayout.Space(-1);
			using (new GUILayout.HorizontalScope())
			{
				EditorGUI.BeginChangeCheck();
				DrawTabsToolbar();
				if (EditorGUI.EndChangeCheck())
				{
					if (currentTab == MaintainerTab.Cleaner) ShowProjectCleanerWarning();
					UserSettings.Instance.selectedTab = currentTab;

					Refresh(false);
				}

				GUILayout.FlexibleSpace();
				if (rateUsWidget.DrawToolbar())
					GUILayout.Space(5f);
			}

			UserSettings.Instance.scroll =
				GUILayout.BeginScrollView(UserSettings.Instance.scroll, false, false);

			switch (currentTab)
			{
				case MaintainerTab.Issues:
					issuesTab.Draw();
					break;
				case MaintainerTab.Cleaner:
					cleanerTab.Draw();
					break;
				case MaintainerTab.References:
					referencesTab.Draw();
					break;
				case MaintainerTab.About:
					aboutTab.Draw();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			GUILayout.EndScrollView();
		}

		private void DrawTabsToolbar()
		{
			if (tabFirstStyle == null)
			{
				var buttonName = GUI.skin.button.name;
				tabFirstStyle = GUI.skin.FindStyle(buttonName + "left") ?? GUI.skin.button;
				tabMidStyle = GUI.skin.FindStyle(buttonName + "mid") ?? GUI.skin.button;
				tabLastStyle = GUI.skin.FindStyle(buttonName + "right") ?? GUI.skin.button;
			}

			for (var i = 0; i < tabsCaptions.Length; i++)
			{
				GUIStyle style;
				if (tabsCaptions.Length == 1) style = GUI.skin.button;
				else if (i == 0) style = tabFirstStyle;
				else if (i == tabsCaptions.Length - 1) style = tabLastStyle;
				else style = tabMidStyle;

				if (GUILayout.Toggle((int)currentTab == i, tabsCaptions[i], style, GUILayout.Width(135), GUILayout.Height(21)))
					currentTab = (MaintainerTab)i;
			}
		}

		private void OnQuit()
		{
			ProjectSettings.Save();
		}
	}
}
