#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Scan
{
	public interface IGameObjectBeginScanListener<T> where T : IScanListenerResults
	{
		void GameObjectBegin(T results, GameObjectLocation location);
	}
}