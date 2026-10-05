#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace CodeStage.Maintainer.Demo
{
	using System;

	[ExecuteAlways]
	public class MaintainerDemoUI : MonoBehaviour
	{
		private bool hasSelected;
		
		private void Update()
		{
			if (!Application.isPlaying && !hasSelected)
			{
				if (SceneManager.GetActiveScene().name == "Maintainer Demo")
				{
					Selection.activeGameObject = gameObject;
					FocusInspectorWindow();
					hasSelected = true;
				}
			}
		}
		
		private static void FocusInspectorWindow()
		{
			Type inspectorWindowType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
			if (inspectorWindowType == null)
				return;

			EditorWindow inspectorWindow = EditorWindow.GetWindow(inspectorWindowType);
			if (inspectorWindow)
				inspectorWindow.Focus();
		}
	}
}
#endif
