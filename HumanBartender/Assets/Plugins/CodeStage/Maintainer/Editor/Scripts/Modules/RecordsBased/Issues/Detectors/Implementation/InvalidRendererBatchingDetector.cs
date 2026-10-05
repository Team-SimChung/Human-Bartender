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
	using UnityEngine;

	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	// ReSharper disable once UnusedType.Global
	internal class InvalidRendererBatchingDetector : IssueDetector, IComponentBeginIssueDetector
	{
		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.Component,
				DetectorKind.Performance,
				IssueSeverity.Warning,
				"Invalid Renderer Batching", 
				"Search for Renderer components with invalid batching setup.");

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
			if (MaterialsUseInstancingShader(renderer.sharedMaterials))
			{
				var batching = CSSettingsTools.GetBuildTargetBatching(EditorUserBuildSettings.activeBuildTarget);
				var flags = GameObjectUtility.GetStaticEditorFlags(location.Component.gameObject);
				if ((flags & StaticEditorFlags.BatchingStatic) != 0 && batching.staticBatching)
				{
					problem = true;
				}
			}
			
			if (problem)
			{
				var narrow = location.Narrow();
				narrow.PropertyOverride("Materials");
				results.Add(GameObjectIssueRecord.ForProperty(this, IssueKind.InvalidRendererBatching, narrow));
			}
		}

		private bool MaterialsUseInstancingShader(Material[] materials)
		{
			foreach (var material in materials)
			{
				if (material != null && material.enableInstancing && material.shader != null && CSReflectionTools.IsShaderHasInstancing(material.shader))
					return true;
			}

			return false;
		}
	}
}