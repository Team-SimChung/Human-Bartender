#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Dependencies
{
	using System;
	using System.Collections.Generic;
	using Tools;

	// ReSharper disable once UnusedType.Global since it's used from TypeCache
	internal class TextAssetParser : DependenciesParser
	{
		public override Type Type => CSReflectionTools.textAssetType;

		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			if (asset.Path.EndsWith(".cginc"))
			{
				// below is an another workaround for dependenciesGUIDs not include #include-ed files, like *.cginc
				return ShaderParser.ScanFileForIncludes(asset.Path);
			}

			return null;
		}
	}
}