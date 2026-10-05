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
	using UnityEditor;
	using UnityEditor.Compilation;
	using UnityEngine;
	
	// ReSharper disable once UnusedType.Global ClassNeverInstantiated.Global since it's used from TypeCache
	internal class AssemblyDefinitionParser : DependenciesParser
	{
		private static string editorPlatformName;

		private static string EditorPlatformName
		{
			get
			{
				if (string.IsNullOrEmpty(editorPlatformName))
				{
					var platforms = CompilationPipeline.GetAssemblyDefinitionPlatforms();
					foreach (var platform in platforms)
					{
						if (platform.BuildTarget == BuildTarget.NoTarget)
						{
							editorPlatformName = platform.Name;
							break;
						}
					}
				}

				return editorPlatformName;
			}
		}
		
		public override Type Type => CSReflectionTools.assemblyDefinitionAssetType;

		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			if (asset.Origin != AssetOrigin.AssetsFolder)
				return null;
			
			return GetAssetsReferencedFromAssemblyDefinition(asset.Path);
		}

		public static bool IsEditorOnlyAssembly(string asmdefPath)
		{
			var data = ParseAssemblyDefinitionAsset(asmdefPath);
			if (data.includePlatforms == null || data.includePlatforms.Length == 0)
				return false;

			return data.includePlatforms.Length == 1 && data.includePlatforms[0] == EditorPlatformName;
		}
		
		private static IList<string> GetAssetsReferencedFromAssemblyDefinition(string assetPath)
		{
			var result = new List<string>();
			var data = ParseAssemblyDefinitionAsset(assetPath);

			if (data.references != null && data.references.Length > 0)
			{
				foreach (var reference in data.references)
				{
					var assemblyDefinitionFilePathFromAssemblyName = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyReference(reference);
					if (!string.IsNullOrEmpty(assemblyDefinitionFilePathFromAssemblyName))
					{
						assemblyDefinitionFilePathFromAssemblyName = CSPathTools.EnforceSlashes(assemblyDefinitionFilePathFromAssemblyName);
						var guid = AssetDatabase.AssetPathToGUID(assemblyDefinitionFilePathFromAssemblyName);
						if (!string.IsNullOrEmpty(guid))
						{
							result.Add(guid);
						}
					}
				}
			}

			data.references = null; // to workaround compiler warning

			return result;
		}

		private static AssemblyDefinitionData ParseAssemblyDefinitionAsset(string assetPath)
		{
			var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(assetPath);
			return JsonUtility.FromJson<AssemblyDefinitionData>(asset.text);
		}

		[Serializable]
		private class AssemblyDefinitionData
		{
			public string[] references;
			public string[] includePlatforms = default;
		}
	}
}