#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using System;
	using Core.Scan;
	using UnityEngine;
	
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class InconsistentTerrainDataDetector : IssueDetector, IComponentBeginIssueDetector, IGameObjectEndIssueDetector
	{
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.GameObject,
				DetectorKind.Defect,
				IssueSeverity.Warning,
				"Inconsistent Terrain Data", 
				"Search for Game Objects where Terrain and TerrainCollider have different Terrain Data.");

		public Type[] ComponentTypes =>
			new[] { 
				typeof(Terrain), 
				typeof(TerrainCollider) 
			};

		private TerrainData terrainData;
		private TerrainData colliderTerrainData;
		private bool terrainChecked;
		private bool colliderChecked;

		private Type componentType;
		private string componentName;
		private int componentIndex;
		
		public void ComponentBegin(DetectorResults results, ComponentLocation location)
		{
			switch (location.Component)
			{
				case Terrain terrain:
					ProcessTerrainComponent(terrain, location.ComponentType, location.ComponentName, location.ComponentIndex);
					break;
				case TerrainCollider collider:
					ProcessTerrainColliderComponent(collider);
					break;
				default:
					Debug.LogError(Maintainer.ErrorForSupport("Unexpected component: " + location.Component + " (" + location.ComponentType + ")"));
					break;
			}
		}

		public void GameObjectEnd(DetectorResults results, GameObjectLocation location)
		{
			if (terrainChecked && colliderChecked && colliderTerrainData != terrainData)
			{
				var narrow = location.Narrow();
				narrow.ComponentOverride(componentType, componentName, componentIndex);
				var issue = GameObjectIssueRecord.ForComponent(this, Issues.IssueKind.InconsistentTerrainData, narrow);
				issue.HeaderPostfix = "at Terrain and TerrainCollider";
				results.Add(issue);
			}
			
			terrainChecked = false;
			colliderChecked = false;

			terrainData = null;
			colliderTerrainData = null;
		}

		private void ProcessTerrainComponent(Terrain component, Type type, string name, int index)
		{
			componentType = type;
			componentName = name;
			componentIndex = index;

			terrainData = component.terrainData;
			terrainChecked = true;
		}

		private void ProcessTerrainColliderComponent(TerrainCollider component)
		{
			colliderTerrainData = component.terrainData;
			colliderChecked = true;
		}
	}
}