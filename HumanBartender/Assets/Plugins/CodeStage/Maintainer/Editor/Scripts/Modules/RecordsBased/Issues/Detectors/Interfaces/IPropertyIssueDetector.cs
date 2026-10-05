#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Scan;

	/// <summary>
	/// Use this interface to detect issues in Serialized Properties inside Components, Assets and other Objects.
	/// </summary>
	public interface IPropertyIssueDetector : IGenericPropertyScanListener<DetectorResults>
	{
		
	}
}