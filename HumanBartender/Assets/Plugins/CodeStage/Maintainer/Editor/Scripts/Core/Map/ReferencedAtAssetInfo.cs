#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core
{
	using System;

	[Serializable]
	internal class ReferencedAtAssetInfo : ReferencedAtInfo
	{
		public AssetInfo assetInfo;
	}
}