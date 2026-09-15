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

	// ReSharper disable once UnusedType.Global since it's used from TypeCache
	internal class AssemblyDefinitionReferenceParser : DependenciesParser
	{
		public override Type Type => CSReflectionTools.assemblyDefinitionReferenceAssetType;
		
		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			if (asset.Origin != AssetOrigin.AssetsFolder)
				return null;
			
			return GetAssetsReferencedFromAssemblyDefinitionReference(asset.Path);
		}
		
		private IList<string> GetAssetsReferencedFromAssemblyDefinitionReference(string assetPath)
		{
			var result = new List<string>();

			var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionReferenceAsset>(assetPath);
			var data = JsonUtility.FromJson<AssemblyDefinitionReferenceData>(asset.text);

			if (!string.IsNullOrEmpty(data.reference))
			{
				var assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyReference(data.reference);
				if (!string.IsNullOrEmpty(assemblyDefinitionPath))
				{
					assemblyDefinitionPath = CSPathTools.EnforceSlashes(assemblyDefinitionPath);
					var guid = AssetDatabase.AssetPathToGUID(assemblyDefinitionPath);
					if (!string.IsNullOrEmpty(guid))
					{
						result.Add(guid);
					}
				}
			}

			data.reference = null;

			return result;
		}

		// ReSharper disable once ClassNeverInstantiated.Local since it's used from JsonUtility.FromJson
		private class AssemblyDefinitionReferenceData
		{
			public string reference;
		}
	}
}