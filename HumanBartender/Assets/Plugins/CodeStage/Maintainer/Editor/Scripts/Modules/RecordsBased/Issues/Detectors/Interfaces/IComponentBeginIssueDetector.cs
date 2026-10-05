#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using System;
	using Core.Scan;

	/// <summary>
	/// Use this interface to detect issues in Components before scanning their contents.
	/// </summary>
	public interface IComponentBeginIssueDetector : IComponentBeginScanListener<DetectorResults>
	{
		/// <summary>
		/// Specifies which Component Types this detector should check.
		/// </summary>
		/// Set null to check all types; checked using Type.IsAssignableFrom() API.
		Type[] ComponentTypes { get; }
	}
}