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
	internal class SpriteAtlasParser : DependenciesParser
	{
		public override Type Type => CSReflectionTools.spriteAtlasType;

		public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
		{
			return GetAssetsGUIDsInFoldersReferencedFromSpriteAtlas(asset.Path);
		}
		
		private static IList<string> GetAssetsGUIDsInFoldersReferencedFromSpriteAtlas(string assetPath)
		{
			var result = new List<string>();

			var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(assetPath);
			var so = new SerializedObject(asset);

			// source: SpriteAtlasInspector
			var packablesProperty = so.FindProperty("m_EditorData.packables");
			if (packablesProperty == null || !packablesProperty.isArray)
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't parse UnityEngine.U2D.SpriteAtlas!"));
			}
			else
			{
				var count = packablesProperty.arraySize;
				for (var i = 0; i < count; i++)
				{
					var packable = packablesProperty.GetArrayElementAtIndex(i);
					var objectReferenceValue = packable.objectReferenceValue;
					if (objectReferenceValue != null)
					{
						var path = AssetDatabase.GetAssetOrScenePath(objectReferenceValue);
						if (AssetDatabase.IsValidFolder(path))
						{
							var packableGUIDs = CSPathTools.GetAllPackableAssetsGUIDsRecursive(path);
							if (packableGUIDs != null && packableGUIDs.Length > 0)
							{
								result.AddRange(packableGUIDs);
							}
						}
					}
				}
			}

			return result;
		}
	}
}