#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface ISceneEndScanListener<T> where T : IScanListenerResults
	{
		void SceneEnd(T results, AssetLocation location);
	}
}