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
	using UnityEngine;

	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	// ReSharper disable once UnusedType.Global
	internal class InvalidRendererMaterialsDetector : IssueDetector, IComponentBeginIssueDetector
	{
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.Component,
				DetectorKind.Defect,
				IssueSeverity.Warning,
				"Invalid Renderer Materials", 
				"Search for Renderer components with invalid Materials setup.");

		public Type[] ComponentTypes => new[]
		{
			CSReflectionTools.rendererType,
		};

		public void ComponentBegin(DetectorResults results, ComponentLocation location)
		{
			var renderer = location.Component as Renderer;
			if (renderer == null)
				return;
			
			var problem = false;
			if (location.Component.TryGetComponent(out MeshFilter mf))
			{
				problem = mf != null && mf.sharedMesh != null && renderer.sharedMaterials.Length > mf.sharedMesh.subMeshCount;
			}
			
			if (problem)
			{
				var narrow = location.Narrow();
				narrow.PropertyOverride("Materials");
				results.Add(GameObjectIssueRecord.ForProperty(this, IssueKind.InvalidRendererMaterials, narrow));
			}
		}
	}
}