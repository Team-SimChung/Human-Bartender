#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	using System;
	using System.Collections.Generic;
	using Extension;

	public enum PropertyScanDepth
	{
		None = 0,
		VisibleOnly = 2,
		//All = 4
	}
	
	internal interface IPropertyScanner : IMaintainerExtension
	{
		
	}
	
	internal interface IPropertyScanner<T> : IPropertyScanner where T : IScanListenerResults
	{
		void Property(T results, PropertyLocation location);
	}

	internal interface IPropertyScanner<TListener, TResult> : IPropertyScanner<TResult>
		where TListener : IPropertyScanListener
		where TResult : IScanListenerResults
	{
		IList<TListener> ScanListeners { get; }
		Type ScanListenerType { get; }
		void RegisterScanListeners(TListener[] listeners);
	}
}