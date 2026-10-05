#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IAssetEndScanListener<T> where T : IScanListenerResults
	{
		void AssetEnd(T results, AssetLocation location);
	}
}