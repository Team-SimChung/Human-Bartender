#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Scan;
	using Tools;
	
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class MissingPrefabDetector : IssueDetector, IGameObjectBeginIssueDetector
	{
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.GameObject,
				DetectorKind.Defect,
				IssueSeverity.Error,
				"Missing Prefab", 
				"Search for instances of Prefabs which were removed from project.");

		public bool IssueFound { get; private set; }
		
		public void GameObjectBegin(DetectorResults results, GameObjectLocation location)
		{
			IssueFound = false;
			if (!CSPrefabTools.IsMissingPrefabInstance(location.GameObject)) 
				return;

			IssueFound = true;
			var issue = GameObjectIssueRecord.ForGameObject(this, IssueKind.MissingPrefab, location);
			results.Add(issue);
		}
	}
}