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
	using UnityEngine;

	// ReSharper disable once UnusedType.Global since it's used from TypeCache
	internal class SettingsParser : DependenciesParser
	{
		public override Type Type => null;

		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			if (asset.Origin != AssetOrigin.Settings)
				return null;
			
			var result = new List<string>();
			
			if (asset.SettingsKind == AssetSettingsKind.EditorBuildSettings)
			{
				var scenesInBuildGUIDs = CSSceneUtils.GetScenesInBuildGUIDs(true);
				if (scenesInBuildGUIDs != null)
				{
					result.AddRange(scenesInBuildGUIDs);
				}
			}
			
			// Process all object references in settings asset (including configObjects)
			var objectReferences = GetObjectReferencesFromSettingsAsset(asset.Path);
			if (objectReferences != null)
			{
				result.AddRange(objectReferences);
			}
			
			return result;
		}
		
		private IList<string> GetObjectReferencesFromSettingsAsset(string assetPath)
		{
			var result = new List<string>();
			
			var settingsAsset = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			if (settingsAsset != null && settingsAsset.Length > 0)
			{
				var settingsAssetSerialized = new SerializedObject(settingsAsset[0]);

				var sp = settingsAssetSerialized.GetIterator();
				while (sp.Next(true))
				{
					if (sp.propertyType == SerializedPropertyType.ObjectReference)
					{
						var instanceId = CSInstanceIdTools.GetObjectReferenceId(sp);
						if (instanceId != 0)
						{
							var referencePath = CSPathTools.EnforceSlashes(CSInstanceIdTools.GetAssetPath(instanceId));
							if (!string.IsNullOrEmpty(referencePath) && referencePath.StartsWith("Assets"))
							{
								var guid = AssetDatabase.AssetPathToGUID(referencePath);
								if (!string.IsNullOrEmpty(guid))
								{
									result.Add(guid);
								}
							}
						}
					}
				}
			}
			
			return result;
		}
	}
}