#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.References
{
	using System.Collections.Generic;
	using Core;

	internal class TreeConjunction
	{
		public readonly List<ProjectReferenceItem> treeElements = new List<ProjectReferenceItem>();
		public AssetInfo referencedAsset;
		public ReferencedAtInfo referencedAtInfo;
	}
}