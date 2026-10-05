#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using UnityEngine;

	public struct DetectorInfo
	{
		public IssueGroup Group { get; }
		public DetectorKind Kind { get; }
		public IssueSeverity Severity { get; }
		public string Name { get; }
		public string Tooltip { get; }
		
		private DetectorInfo(IssueGroup group, DetectorKind kind, IssueSeverity severity, string name, string tooltip = null) : this()
		{
			Group = group;
			Kind = kind;
			Severity = severity;
			Name = name;
			Tooltip = tooltip;
		}

		public GUIContent GetGUIContent()
		{
			return new GUIContent(Name, Tooltip);
		}

		public static DetectorInfo From(IssueGroup group, DetectorKind kind, IssueSeverity severity, string name, string tooltip = null)
		{
			return new DetectorInfo(group, kind, severity, name, tooltip);
		}
	}
}