#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IAssetBeginScanListener<T> where T : IScanListenerResults
	{
		void AssetBegin(T results, AssetLocation location);
	}
}