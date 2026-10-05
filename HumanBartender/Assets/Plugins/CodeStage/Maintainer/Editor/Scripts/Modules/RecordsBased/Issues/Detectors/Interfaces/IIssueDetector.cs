#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Extension;

	public interface IIssueDetector : IMaintainerExtension
	{
		DetectorInfo Info { get; }
	}
}