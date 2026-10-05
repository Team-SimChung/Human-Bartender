#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Scan;

	/// <summary>
	/// Use this interface to detect issues in serialized Unity Events.
	/// </summary>
	public interface IUnityEventIssueDetector : IUnityEventScanListener<DetectorResults>
	{
		
	}
}