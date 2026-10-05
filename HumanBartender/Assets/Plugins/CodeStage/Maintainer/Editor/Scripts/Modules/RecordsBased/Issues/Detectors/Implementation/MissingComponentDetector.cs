#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using System;
	using Core.Scan;
	using Tools;
	using UnityEditor;
	
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class MissingComponentDetector : IssueDetector, 
		IGameObjectBeginIssueDetector
	{
		public static MissingComponentDetector Instance { get; private set; }
		
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.Component,
				DetectorKind.Defect,
				IssueSeverity.Error,
				"Missing Component", 
				"Search for the missing Components on the Game Objects or Scriptable Objects.");

		public Type[] AssetTypes => null; // we are checking all assets
		public Type[] ComponentTypes => null; // we are checking all components

		public MissingComponentDetector()
		{
			Instance = this;
		}
		
		public bool AssetHasIssue(DetectorResults results, AssetLocation location)
		{
			if (location.Asset.Type != null)
				return false;

			if (!CSAssetTools.IsAssetScriptableObjectWithMissingScript(location.Asset.Path))
				return false;

			if (results != null)
			{
				var record = UnityObjectAssetIssueRecord.Create(this, IssueKind.MissingComponent, location);
				results.Add(record);
			}

			return true;
		}

		public void GameObjectBegin(DetectorResults results, GameObjectLocation location)
		{
			var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(location.GameObject);
			var issue = TryGenerateIssue(missingCount, location);
			if (issue != null)
				results.Add(issue);
		}

		private IssueRecord TryGenerateIssue(int missingCount, GameObjectLocation location)
		{
			if (missingCount == 0)
				return null;

			var narrow = location.Narrow();
			narrow.ComponentOverride(null, null, -1);
			
			var record = GameObjectIssueRecord.ForComponent(this, Issues.IssueKind.MissingComponent,  narrow);
			if (missingCount > 1)
			{
				record.Header = $"{missingCount} missing components";
			}
			
			return record;
		}
	}
}