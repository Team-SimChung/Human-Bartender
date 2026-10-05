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
	/// Use this interface to detect issues in Assets before scanning their contents.
	/// </summary>
	public interface IAssetBeginIssueDetector : IAssetBeginScanListener<DetectorResults>
	{
		/// <summary>
		/// Specifies which asset types this detector should check.
		/// </summary>
		/// Set null to check all types; checked using Type.IsAssignableFrom() API.
		Type[] AssetTypes { get; }
	}
}