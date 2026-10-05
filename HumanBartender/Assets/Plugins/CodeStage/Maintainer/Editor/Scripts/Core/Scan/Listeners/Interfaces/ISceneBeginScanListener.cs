#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface ISceneBeginScanListener<T> where T : IScanListenerResults
	{
		void SceneBegin(T results, AssetLocation location);
	}
}