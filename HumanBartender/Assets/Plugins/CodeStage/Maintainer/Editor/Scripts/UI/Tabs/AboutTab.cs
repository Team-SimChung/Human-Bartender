#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using System.Diagnostics;
	using System.Globalization;
	using Core;
	using Core.Map.Session;
	using Core.Map.ChangeTracking;
	using EditorCommon.Tools;
	using Settings;
	using Tools;

	using UnityEditor;
	using UnityEngine;
	using Debug = UnityEngine.Debug;

	internal class AboutTab : BaseTab
	{
		private bool showDebug = false;

		protected override string CaptionName
		{
			get { return "About"; }
		}

		protected override Texture CaptionIcon
		{
			get { return CSEditorIcons.Help; }
		}

		public AboutTab(MaintainerWindow window) : base(window) {}

		public void Draw()
		{
			using (new GUILayout.HorizontalScope())
			{
				DrawLogo();
				DrawButtons();
			}
		}

		private void DrawLogo()
		{
			using (new GUILayout.VerticalScope(UIHelpers.panelWithBackground, GUILayout.ExpandHeight(true),
				GUILayout.ExpandWidth(true)))
			{
				GUILayout.FlexibleSpace();

				using (new GUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();

					var logo = CSImages.Logo;
					if (logo != null)
					{
						logo.wrapMode = TextureWrapMode.Clamp;
						var logoRect = EditorGUILayout.GetControlRect(GUILayout.Width(logo.width),
							GUILayout.Height(logo.height));
						GUI.DrawTexture(logoRect, logo);
						GUILayout.Space(5);
					}

					GUILayout.FlexibleSpace();
				}

				GUILayout.FlexibleSpace();
			}
		}
		
		private void DrawButtons()
		{
			using (new GUILayout.HorizontalScope(UIHelpers.panelWithBackground, GUILayout.ExpandHeight(true),
					GUILayout.ExpandWidth(true)))
				{
					GUILayout.Space(10);

					using (new GUILayout.VerticalScope())
					{
						GUILayout.Space(10);

						DrawCommonButtons();

						GUILayout.Space(10);

						//GUILayout.Space(10);
						//GUILayout.Label("Asset Store links", UIHelpers.centeredLabel);
						UIHelpers.Separator();
						GUILayout.Space(10);

						DrawAssetStoreButtons();

						DrawDebugButtons();
					}
					GUILayout.Space(10);
				}
		}

		private void DrawCommonButtons()
		{
			GUILayout.Label("<size=18>Maintainer</size>",
				UIHelpers.centeredLabel);
			GUILayout.Label("<b>Version " + Maintainer.Version + "</b>",
				UIHelpers.centeredLabel);
			GUILayout.Space(10);
			GUILayout.Label("Developed by Dmitry Yuhanov\n" +
							"Logo by Daniele Giardini\n" +
							"Icons by Google, Austin Andrews, Cody", UIHelpers.centeredLabel);
			GUILayout.Space(10);
			UIHelpers.Separator();
			GUILayout.Space(10);
			if (UIHelpers.ImageButton("Homepage", CSIcons.Home))
			{
				Application.OpenURL(MaintainerLinks.HomePage);
			}
			
			GUILayout.Space(10);

			using (new GUILayout.HorizontalScope())
			{
				if (UIHelpers.ImageButton("Discord", CSIcons.Discord))
				{
					Application.OpenURL(MaintainerLinks.DiscordLink);
				}
				GUILayout.Space(5);
				if (UIHelpers.ImageButton("Support", CSIcons.Support))
				{
					Application.OpenURL(MaintainerLinks.SupportContact);
				}
			}

			GUILayout.Space(10);
			if (UIHelpers.ImageButton("Full changelog (online)", CSIcons.Log))
			{
				Application.OpenURL(MaintainerLinks.ChangelogLink);
			}
		}
		
		private void DrawAssetStoreButtons()
		{
			if (UIHelpers.ImageButton("Code Stage at the Asset Store", CSIcons.Publisher))
			{
				Application.OpenURL(MaintainerLinks.UasProfileLink);
			}
			GUILayout.Space(10);
			if (UIHelpers.ImageButton("Maintainer at the Asset Store", CSIcons.Maintainer))
			{
				Application.OpenURL(MaintainerLinks.UasLink);
			}
			GUILayout.Space(10);
		}
		
		private void DrawDebugButtons()
		{
			if (Event.current.isKey && Event.current.type == EventType.KeyDown && Event.current.control && Event.current.keyCode == KeyCode.D)
			{
				showDebug = !showDebug;
				Event.current.Use();
			}

			if (showDebug)
			{
				GUILayout.Space(5);
				UIHelpers.Separator();
				GUILayout.Space(5);
				GUILayout.Label("Welcome to secret debug mode =D");
				if (GUILayout.Button("Remove Assets Map"))
				{
					AssetsMap.Delete();
				}

				if (GUILayout.Button("Measure Assets Map build time"))
				{
					var sw = Stopwatch.StartNew();
					var initialAllocatedMemory = CSEditorTools.GetCurrentAllocatedMemoryBytes();
					var peakAllocatedMemory = initialAllocatedMemory;
					var canceled = false;
					var map = AssetsMap.CreateNew(out canceled);
					CSEditorTools.CapturePeakAllocatedMemory(ref peakAllocatedMemory);
					sw.Stop();
					Debug.Log(Maintainer.ConstructLog("Asset Map build took " +
											  sw.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
											  " seconds, assets found: " + map.assets.Count +
											  ", " + CSEditorTools.FormatMaxReservedMemory(initialAllocatedMemory, peakAllocatedMemory)));
				}
				
				if (GUILayout.Button("Measure Assets Map update time"))
				{
					var sw = Stopwatch.StartNew();
					var initialAllocatedMemory = CSEditorTools.GetCurrentAllocatedMemoryBytes();
					var peakAllocatedMemory = initialAllocatedMemory;
					var canceled = false;
					var map = AssetsMap.GetUpdated(out canceled);
					CSEditorTools.CapturePeakAllocatedMemory(ref peakAllocatedMemory);
					sw.Stop();
					Debug.Log(Maintainer.ConstructLog("Asset Map update took " +
											  sw.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
											  " seconds, assets found: " + map.assets.Count +
											  ", " + CSEditorTools.FormatMaxReservedMemory(initialAllocatedMemory, peakAllocatedMemory)));
				}
							
				if (GUILayout.Button("Remove User Settings and Close"))
				{
					window.Close();
					UserSettings.Delete();
				}

				if (GUILayout.Button("Remove All Settings and Close"))
				{
					window.Close();
					ProjectSettings.Delete();
				}

				if (GUILayout.Button("Re-save all scenes in project"))
				{
					CSSceneUtils.ReSaveAllScenes();
				}
				
				if (GUILayout.Button("Reload settings"))
				{
					ProjectSettings.Reload();
				}

				if (GUILayout.Button("Reset Rate Us widget"))
				{
					EditorPrefs.DeleteKey("CodeStage.Maintainer.RateUs.HasRated");
				}
				
				GUILayout.Space(5);
				GUILayout.Label("Session Cache & Change Tracking:");
				
				if (GUILayout.Button("Clear Session Map Cache"))
				{
					AssetsMapSessionCache.ClearSessionCache();
					AssetsChangeTracker.ClearChanges();
					Debug.Log(Maintainer.ConstructLog("Session cache and change tracker cleared"));
				}
				
				if (GUILayout.Button("Show Change Tracker State"))
				{
					var info = AssetsChangeTracker.GetDiagnosticInfo();
					Debug.Log(Maintainer.ConstructLog(info));
				}
			}
		}
	}
}
