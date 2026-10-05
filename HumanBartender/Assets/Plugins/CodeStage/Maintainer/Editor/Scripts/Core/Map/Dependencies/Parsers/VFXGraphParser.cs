#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

#if MAINTAINER_VFX_GRAPH
namespace CodeStage.Maintainer.Core.Dependencies
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text.RegularExpressions;
	using UnityEditor;

	// ReSharper disable once UnusedType.Global ClassNeverInstantiated.Global since it's used from TypeCache
	internal class VFXGraphParser : DependenciesParser
	{
		// Supported VFX file extensions
		private static readonly string[] VfxExtensions = { ".vfx", ".vfxblock", ".vfxoperator" };
		
		// Standard Unity YAML format: guid: c9bbe7948cd7f2c489b1c8eff673345e
		private static readonly Regex YamlGuidRegex = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
		
		// JSON serialized format: "guid":"c9bbe7948cd7f2c489b1c8eff673345e" or \"guid\":\"c9bbe7948cd7f2c489b1c8eff673345e\"
		// This is used for embedded object references in m_SerializableObject fields
		private static readonly Regex JsonGuidRegex = new Regex(@"[""\\]+guid[""\\]+\s*:\s*[""\\]+([0-9a-fA-F]{32})[""\\]+", RegexOptions.Compiled);

		// Type is null to allow this parser to run for all assets;
		// we filter by extension inside GetDependenciesGUIDs to handle .vfx, .vfxblock, and .vfxoperator
		public override Type Type => null;

		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			// Check if this is a VFX asset by extension
			var extension = Path.GetExtension(asset.Path);
			var isVfxAsset = false;
			foreach (var vfxExt in VfxExtensions)
			{
				if (extension.Equals(vfxExt, StringComparison.OrdinalIgnoreCase))
				{
					isVfxAsset = true;
					break;
				}
			}
			
			if (!isVfxAsset)
				return null;
			
			var referencesGUIDs = new List<string>();

			// VFX Graph asset dependencies (such as Shader Graph VFX Assets, textures, meshes) may not be fully
			// reported by AssetDatabase.GetDependencies, so we parse the source file for GUID references
			var content = File.ReadAllText(asset.Path);
			var assetGUID = asset.GUID;
			
			// Match standard YAML format GUIDs
			CollectGUIDs(YamlGuidRegex.Matches(content), referencesGUIDs, assetGUID);
			
			// Match JSON serialized format GUIDs (used for embedded object references)
			CollectGUIDs(JsonGuidRegex.Matches(content), referencesGUIDs, assetGUID);

			return referencesGUIDs;
		}
		
		private static void CollectGUIDs(MatchCollection matches, List<string> referencesGUIDs, string assetGUID)
		{
			foreach (Match match in matches)
			{
				var guid = match.Groups[1].Value;
				if (guid == assetGUID)
					continue;

				if (referencesGUIDs.Contains(guid))
					continue;

				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path))
					continue;

				referencesGUIDs.Add(guid);
			}
		}
	}
}
#endif
