using System;
using System.IO;
using CodeStage.Maintainer.Core;
using CodeStage.Maintainer.Core.Scan;
using CodeStage.Maintainer.Tools;
using UnityEditor;

namespace CodeStage.Maintainer.Issues.Detectors
{
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class DeletedBuildSceneDetector : IssueDetector, ISettingsAssetBeginIssueDetector
	{
		public override DetectorInfo Info => DetectorInfo.From(
			IssueGroup.ProjectSettings,
			DetectorKind.Defect,
			IssueSeverity.Warning,
			"Deleted build scenes",
			"Finds scenes referenced in build settings that were deleted from the project.");

		public AssetSettingsKind SettingsKind => AssetSettingsKind.EditorBuildSettings;

		public void AssetBegin(DetectorResults results, AssetLocation location)
		{
			AddDeletedFromBuildSettings(results, location);
		}

		private void AddDeletedFromBuildSettings(DetectorResults results, AssetLocation location)
		{
			var scenes = EditorBuildSettings.scenes;
			if (scenes == null || scenes.Length == 0)
				return;

			foreach (var scene in scenes)
			{
				var scenePath = CSPathTools.EnforceSlashes(scene.path);
				if (string.IsNullOrEmpty(scenePath))
					continue;

				if (File.Exists(scenePath))
					continue;

				var issue = BuildSettingsIssueRecord.Create(this, location, scenePath);
				results.Add(issue);
			}
		}

	}
}
