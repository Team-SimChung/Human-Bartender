#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Scan;
	using UnityEngine;
	
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class InvalidLayerDetector : IssueDetector, IGameObjectBeginIssueDetector
	{
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.GameObject,
				DetectorKind.Defect,
				IssueSeverity.Info,
				"Invalid Layer",
				"Search for Game Objects with invalid (empty) Layers.");

		public void GameObjectBegin(DetectorResults results, GameObjectLocation location)
		{
			var layerIndex = location.GameObject.layer;
			
			if (!string.IsNullOrEmpty(LayerMask.LayerToName(layerIndex))) 
				return;

			var issue = GameObjectIssueRecord.ForGameObject(this, Issues.IssueKind.UnnamedLayer, location);
			issue.HeaderPostfix = "(index: " + layerIndex + ")";
			results.Add(issue);
		}
	}
}