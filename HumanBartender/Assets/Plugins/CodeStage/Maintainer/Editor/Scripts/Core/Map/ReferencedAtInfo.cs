#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core
{
	using System;
	using System.Collections.Generic;

	[Serializable]
	internal abstract class ReferencedAtInfo
	{
		public ReferencingEntryData[] entries;

		[NonSerialized]
		private List<ReferencingEntryData> entriesAccumulator;

		public void AddNewEntry(ReferencingEntryData newEntry)
		{
			if (entriesAccumulator == null)
				entriesAccumulator = new List<ReferencingEntryData>();
			entriesAccumulator.Add(newEntry);
		}

		/// <summary>
		/// Converts accumulated entries from the internal List to the entries array.
		/// Must be called after all AddNewEntry calls are complete for this info.
		/// </summary>
		public void FlushEntries()
		{
			if (entriesAccumulator != null && entriesAccumulator.Count > 0)
			{
				entries = entriesAccumulator.ToArray();
				entriesAccumulator = null;
			}
		}
	}
}