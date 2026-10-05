#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	internal abstract class ReferencesChildTab : BaseTab
	{
		protected ReferencesChildTab(MaintainerWindow window) : base(window) {}

		internal abstract void CollapseAllElements();
		internal abstract void ExpandAllElements();
		internal abstract void ClearResults();
	}
}