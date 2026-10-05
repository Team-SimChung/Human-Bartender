#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core;
	using Core.Scan;
	
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class DuplicateLayersDetector : IssueDetector, ISettingsAssetBeginIssueDetector
	{
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.ProjectSettings,
				DetectorKind.Defect,
				IssueSeverity.Info,
				"Duplicate Layers", 
				"Search for the duplicate Layers and Sorting Layers at the 'Tags and Layers' Project Settings.");

		public AssetSettingsKind SettingsKind => AssetSettingsKind.TagManager;

		public void AssetBegin(DetectorResults results, AssetLocation location)
		{
			var issue = SettingsChecker.CheckTagsAndLayers(this, location);
			if (issue != null)
			{
				issue.HeaderPostfix = "at the 'Tags and Layers' settings";
				results.Add(issue);
			}
		}
	}
}