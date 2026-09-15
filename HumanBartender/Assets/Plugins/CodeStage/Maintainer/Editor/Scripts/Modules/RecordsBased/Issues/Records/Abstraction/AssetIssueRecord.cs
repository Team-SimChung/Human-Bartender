#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues
{
	using System;
	using System.Text;
	using Core.Scan;
	using Detectors;
	using Tools;
	using UnityEngine;

	[Serializable]
	public abstract class AssetIssueRecord : IssueRecord
	{
		[field: SerializeField]
		public string Path { get; private set; }

		internal AssetIssueRecord(IIssueDetector detector, IssueKind kind, AssetLocation location) : base(detector, kind, location)
		{
			Path = location.Asset.Path;
		}

		protected void AppendPropertyInfo(StringBuilder text, string propertyPath, string humanReadablePropertyName)
		{
			if (string.IsNullOrEmpty(propertyPath))
				return;

			// Use human-readable name if available, otherwise fall back to nice property path
			var propertyName = !string.IsNullOrEmpty(humanReadablePropertyName) 
				? humanReadablePropertyName 
				: CSObjectTools.GetNicePropertyPath(propertyPath);
			text.Append("\n<b>Property:</b> ").Append(propertyName);
			
			// For reports, also include the internal property path if we have a human-readable name
			if (!string.IsNullOrEmpty(humanReadablePropertyName))
			{
				text.Append(" (Internal: ").Append(propertyPath).Append(")");
			}
		}
	}
}
