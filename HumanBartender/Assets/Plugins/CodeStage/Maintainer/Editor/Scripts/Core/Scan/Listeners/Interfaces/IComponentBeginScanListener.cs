#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IComponentBeginScanListener<T> where T : IScanListenerResults
	{
		void ComponentBegin(T results, ComponentLocation location);
	}
}