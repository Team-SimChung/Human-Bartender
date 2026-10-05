#region copyright
// ------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Dependencies
{
	using System;
	using System.Collections.Generic;
	using Tools;
	using UnityEditor;
	using UnityEngine;

	// ReSharper disable once UnusedType.Global since it's used from TypeCache
	internal class MonoScriptParser : DependenciesParser
	{
		public override Type Type => CSReflectionTools.monoScriptType;

		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			var importer = AssetImporter.GetAtPath(asset.Path) as MonoImporter;
			if (importer == null)
				return null;

			var icon = importer.GetIcon();
			if (icon == null)
				return null;

			var iconPath = AssetDatabase.GetAssetPath(icon);
			if (string.IsNullOrEmpty(iconPath))
				return null;

			var iconGuid = AssetDatabase.AssetPathToGUID(iconPath);
			if (string.IsNullOrEmpty(iconGuid))
				return null;

			return new[] { iconGuid };
		}
	}
}