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
	internal class InvalidSortingLayerDetector : IssueDetector, IComponentBeginIssueDetector
	{
		public override DetectorInfo Info { get { return 
			DetectorInfo.From(
				IssueGroup.Component,
				DetectorKind.Defect,
				IssueSeverity.Info,
				"Invalid Sorting Layer", 
				"Search for components with invalid Sorting Layer.");
		}}
		
		public Type[] ComponentTypes => new[]
		{
			CSReflectionTools.rendererType,
			CSReflectionTools.sortingGroup,
			CSReflectionTools.canvas,
		};

		public void ComponentBegin(DetectorResults results, ComponentLocation location)
		{
			if (location.Component is Canvas canvas)
			{
				if (canvas.renderMode != RenderMode.WorldSpace)
					return;
			}
			
			var so = new SerializedObject(location.Component);
			var sortingLayerIdProperty = so.FindProperty("m_SortingLayerID");
			if (sortingLayerIdProperty == null)
			{
				Debug.LogError(Maintainer.ErrorForSupport(
					"Couldn't find m_SortingLayerID property at the component " + location.Component,
					IssuesFinder.ModuleName), location.Component);
				return;
			}

			var id = sortingLayerIdProperty.intValue;
			if (!SortingLayer.IsValid(id))
			{
				var narrow = location.Narrow();
				narrow.PropertyOverride("Sorting Layer");
				results.Add(GameObjectIssueRecord.ForProperty(this, IssueKind.InvalidSortingLayer, narrow));
			}
		}
	}
}