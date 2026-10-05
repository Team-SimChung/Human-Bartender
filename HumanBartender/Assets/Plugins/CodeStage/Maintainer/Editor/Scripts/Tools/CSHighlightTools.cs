#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

// before 2021.3, Highlighter was way too glitchy
#if UNITY_2021_3_OR_NEWER

namespace CodeStage.Maintainer.Tools
{
	using Core;
	using UnityEditor;
	using UnityEngine;
	using System;

	internal static class CSHighlightTools
	{
		private const float HighlightDuration = 3f;
		
		private static EditorApplication.CallbackFunction currentHighlightTimer;

		[InitializeOnLoadMethod]
		private static void SubscribeToEvents()
		{
			Selection.selectionChanged += StopHighlight;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			AssemblyReloadEvents.beforeAssemblyReload += StopHighlight;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode || 
			    state == PlayModeStateChange.EnteredPlayMode)
				StopHighlight();
		}

		public static bool TryHighlightText(string text)
		{
			if (string.IsNullOrEmpty(text))
				return false;

			StopHighlight();

			var focusedWindow = EditorWindow.focusedWindow;
			if (focusedWindow == null)
				return false;

			var title = focusedWindow.titleContent.text;
			return Highlight(title, text);
		}

		public static void TryHighlightProperty(ReferencingEntryData referencingEntry)
		{
			if (string.IsNullOrEmpty(referencingEntry.PropertyPath))
				return;

			StopHighlight();

			EditorApplication.delayCall += () =>
			{
                EditorApplication.delayCall += () =>
			    {
                    EditorApplication.delayCall += () =>
                    {
                        var focusedWindow = EditorWindow.focusedWindow;
                        if (focusedWindow == null)
                            return;

                        var title = focusedWindow.titleContent.text;

						Highlight(title, referencingEntry.PropertyPath);
                        
                        var startTime = EditorApplication.timeSinceStartup;

                        currentHighlightTimer = StopHighlightTimer;
                        EditorApplication.update += currentHighlightTimer;
                        return;

                        void StopHighlightTimer()
                        {
                            var elapsed = EditorApplication.timeSinceStartup - startTime;
                            if (elapsed >= HighlightDuration)
                                StopHighlight();
                        }
                    };
                };
            };
		}
		
		public static void StopHighlight()
		{
			if (currentHighlightTimer == null)
				return;

			Highlighter.Stop();
			EditorApplication.update -= currentHighlightTimer;
			currentHighlightTimer = null;
		}

		private static bool Highlight(string title, string text)
		{
			try
			{
				Debug.unityLogger.logEnabled = false;
				return Highlighter.Highlight(title, text);
			}
			catch (Exception) {
				// ignored
			}
			finally
			{
				Debug.unityLogger.logEnabled = true;
			}

			return false;
		}
	}
}

#endif