#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues
{
	/// <summary>
	/// Kinds of Issues Finder results items.
	/// </summary>
	public enum IssueKind
	{
		/* object issues */

		MissingComponent = 0,
		DuplicateComponent = 50,
		MissingReference = 100,
		MissingPrefab = 300,
		UnnamedLayer = 800,
		InvalidSortingLayer = 850,
		InvalidRendererMaterials = 875,
		InvalidRendererBatching = 885,
		HugePosition = 900,
		InconsistentTerrainData = 1100,
		ShaderError = 1200,

		/* project settings issues */

		DuplicateLayers = 3010,
		DeletedBuildScene = 3020,
		Error = 5000,
		Other = 100000
	}
}