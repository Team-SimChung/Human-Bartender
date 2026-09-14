#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IPropertyScanListener
	{
		
	}
	
	public interface IGenericPropertyScanListener<in T> : IPropertyScanListener where T : IScanListenerResults
	{
		PropertyScanDepth GetPropertyScanDepth(ComponentLocation location);
		void Property(T results, PropertyLocation location);
	}
	
	public interface IUnityEventScanListener<T> : IPropertyScanListener where T : IScanListenerResults
	{
		void UnityEventProperty(T results, PropertyLocation location, UnityEventScanPhase phase);
	}
}