#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IGameObjectEndScanListener<T> where T : IScanListenerResults
	{
		void GameObjectEnd(T results, GameObjectLocation location);
	}
}