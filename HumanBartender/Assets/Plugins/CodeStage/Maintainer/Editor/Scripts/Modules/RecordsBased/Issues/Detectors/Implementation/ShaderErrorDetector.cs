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
	internal class ShaderErrorDetector : IssueDetector, IAssetBeginIssueDetector
	{

		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.Asset,
				DetectorKind.Defect,
				IssueSeverity.Error,
				"Shader with error(s)", 
				"Search for Shaders with compilation errors.");

		public Type[] AssetTypes => new[] { CSReflectionTools.shaderType };

		public void AssetBegin(DetectorResults results, AssetLocation location)
		{
			var loadedShader = AssetDatabase.LoadAssetAtPath<Shader>(location.Asset.Path);
			if (!ShaderUtil.ShaderHasError(loadedShader))
				return;
			
			var issue = ShaderIssueRecord.Create(this, location);
			results.Add(issue);
		}
	}
}