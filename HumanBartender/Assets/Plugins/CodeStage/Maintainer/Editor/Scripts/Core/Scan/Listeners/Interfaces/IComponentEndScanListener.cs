#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IComponentEndScanListener<T> where T : IScanListenerResults
	{
		void ComponentEnd(T results, ComponentLocation location);
	}
}