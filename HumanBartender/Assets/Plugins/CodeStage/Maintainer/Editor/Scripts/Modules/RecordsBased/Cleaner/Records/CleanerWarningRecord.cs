#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Cleaner
{
	using System;
	using System.Text;
	using Core;
	using Core.Scan;
	using UnityEngine;

	/// <summary>
	/// Project Cleaner search results item representing search warning.
	/// </summary>
	[Serializable]
	public class CleanerWarningRecord : CleanerRecord
	{
		/// <summary>
		/// Text of the error.
		/// </summary>
		public string WarningText
		{
			get { return errorText; }
		}

		[SerializeField] internal string errorText;

		protected CleanerWarningRecord(string errorText) : base(RecordType.Warning, LocationGroup.Unknown)
		{
			this.errorText = errorText;
		}

		internal static CleanerWarningRecord Create(string text)
		{
			return new CleanerWarningRecord(text);
		}

		internal override bool MatchesFilter(FilterItem newFilter)
		{
			return false;
		}

		protected override void ConstructCompactLine(StringBuilder text)
		{
			text.Append(errorText);
		}

		protected override void ConstructBody(StringBuilder text)
		{
			text.Append(errorText);
		}

		protected override bool PerformClean()
		{
			return false;
		}
	}
}