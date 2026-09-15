#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Scan;

	/// <summary>
	/// Use this interface to detect issues in Scenes before scanning their contents.
	/// </summary>
	public interface ISceneBeginIssueDetector : ISceneBeginScanListener<DetectorResults>
	{
		
	}
}