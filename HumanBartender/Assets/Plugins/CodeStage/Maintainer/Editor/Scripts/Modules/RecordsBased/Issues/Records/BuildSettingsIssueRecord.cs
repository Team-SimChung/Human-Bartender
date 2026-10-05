using System;
using System.Collections.Generic;
using System.Text;
using CodeStage.Maintainer.Core;
using CodeStage.Maintainer.Core.Scan;
using CodeStage.Maintainer.Issues.Detectors;
using CodeStage.Maintainer.Tools;
using UnityEditor;
using UnityEngine;

namespace CodeStage.Maintainer.Issues
{
	[Serializable]
	public class BuildSettingsIssueRecord : AssetIssueRecord, IShowableRecord
	{
		[field: SerializeField]
		public string ScenePath { get; }

		public override bool IsFixable => true;

		internal static BuildSettingsIssueRecord Create(IIssueDetector detector, AssetLocation location, string scenePath)
		{
			return new BuildSettingsIssueRecord(detector, IssueKind.DeletedBuildScene, location, scenePath);
		}

		internal override bool MatchesFilter(FilterItem newFilter)
		{
			var filters = new[] { newFilter };
			switch (newFilter.kind)
			{
				case Core.FilterKind.Path:
				case Core.FilterKind.Directory:
				case Core.FilterKind.FileName:
					return !string.IsNullOrEmpty(ScenePath) &&
					       CSFilterTools.IsValueMatchesAnyFilterOfKind(ScenePath, filters, newFilter.kind);
				default:
					return false;
			}
		}

		public void Show()
		{
			ShowInLegacyBuildSettings();
		}

		private void ShowInLegacyBuildSettings()
		{
			if (!CSMenuTools.ShowEditorBuildSettings())
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't open EditorBuildSettings!"));
				return;
			}

#if UNITY_2021_3_OR_NEWER
			var path = ScenePath.Replace("Assets/", "").Replace(".unity", "");

#if UNITY_6000_0_OR_NEWER
			EditorApplication.delayCall += () => {
				if (!CSHighlightTools.TryHighlightText(path))
					CSHighlightTools.TryHighlightText("Scene List");
			};
#else
			EditorApplication.delayCall += () => CSHighlightTools.TryHighlightText(path);
#endif
#endif
		}

		protected override void ConstructBody(StringBuilder text)
		{
			text.Append("<b>Scene:</b> ").Append(ScenePath);
			text.Append("\n<b>Source:</b> Build Settings");
		}

		internal override FixResult PerformFix(bool batchMode)
		{
			return FixBuildSettingsScene();
		}

		private FixResult FixBuildSettingsScene()
		{
			var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
			var removed = scenes.RemoveAll(s => string.Equals(CSPathTools.EnforceSlashes(s.path), CSPathTools.EnforceSlashes(ScenePath), StringComparison.OrdinalIgnoreCase)) > 0;

			if (!removed)
			{
				return new FixResult(true);
			}

			EditorBuildSettings.scenes = scenes.ToArray();
			AssetDatabase.SaveAssets();
			return new FixResult(true);
		}

		private BuildSettingsIssueRecord(IIssueDetector detector, IssueKind kind, AssetLocation location, string scenePath) 
			: base(detector, kind, location)
		{
			ScenePath = CSPathTools.EnforceSlashes(scenePath);
		}
	}
}
