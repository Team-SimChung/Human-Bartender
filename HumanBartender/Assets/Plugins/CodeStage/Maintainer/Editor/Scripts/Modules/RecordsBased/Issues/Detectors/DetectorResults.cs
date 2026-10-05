#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using System.Collections.Generic;
	using Core.Scan;
	
	public class DetectorResults : ScanListenerResults<IssueRecord>
	{
		internal void Set(ref List<IssueRecord> overrideResults)
		{
			results = overrideResults;
		}
	}
}