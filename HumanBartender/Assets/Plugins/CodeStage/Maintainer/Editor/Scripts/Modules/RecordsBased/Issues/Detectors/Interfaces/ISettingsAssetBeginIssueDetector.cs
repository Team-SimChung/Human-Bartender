#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core;
	using Core.Scan;

	/// <summary>
	/// Use this interface to detect issues in Project Settings Asset.
	/// </summary>
	public interface ISettingsAssetBeginIssueDetector : IAssetBeginScanListener<DetectorResults>
	{
		AssetSettingsKind SettingsKind { get; }
	}
}