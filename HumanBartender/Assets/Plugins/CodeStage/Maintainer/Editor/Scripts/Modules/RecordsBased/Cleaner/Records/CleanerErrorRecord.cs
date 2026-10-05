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
	/// Project Cleaner search results item representing search error.
	/// </summary>
	[Serializable]
	public class CleanerErrorRecord : CleanerRecord
	{
		/// <summary>
		/// Text of the error.
		/// </summary>
		public string ErrorText
		{
			get { return errorText; }
		}

		[SerializeField] internal string errorText;

		protected CleanerErrorRecord(string errorText) : base(RecordType.Error, LocationGroup.Unknown)
		{
			this.errorText = errorText;
		}

		internal static CleanerErrorRecord Create(string text)
		{
			return new CleanerErrorRecord(text);
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