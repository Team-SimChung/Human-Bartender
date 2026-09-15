#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Tools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Reflection;
	using Core;
	using UnityEditor;
	using UnityEngine;

	internal static class CSAssetTools
	{
		public static bool IsAssetScriptableObjectWithMissingScript(string path)
		{
			var extension = Path.GetExtension(path);
			return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
			       string.Equals(extension, ".playable", StringComparison.OrdinalIgnoreCase);
		}

		public static long GetMainAssetInstanceID(string path)
		{
#if UNITY_6000_4_OR_NEWER
			// The legacy int-based GetMainAssetInstanceID (below, used pre-6000.4) returns values
			// from a DIFFERENT internal numbering than EntityId/Selection.entityIds on 6000.4+ —
			// proven empirically: reinterpreting one as the other resolves to no asset at all.
			// GetMainAssetEntityId is the real, correct EntityId source here; it's internal, so
			// still requires reflection, same as the legacy call.
			var entityMi = CSReflectionTools.GetGetMainAssetEntityIdMethodInfo();
			if (entityMi != null)
			{
				var entityId = (EntityId)entityMi.Invoke(null, new object[] { path });
				return unchecked((long)EntityId.ToULong(entityId));
			}

			Debug.LogError(Maintainer.ErrorForSupport("Can't retrieve EntityId From path via reflection!"));
			return -1;
#else
			var mi = CSReflectionTools.GetGetMainAssetInstanceIDMethodInfo();
			if (mi != null)
			{
				return (int)mi.Invoke(null, new object[] { path });
			}

			Debug.LogError(Maintainer.ErrorForSupport("Can't retrieve InstanceID From path via reflection!"));
			return -1;
#endif
		}

		public static string[] GetAssetsGUIDs(string[] paths)
		{
			if (paths == null || paths.Length == 0)
			{
				return null;
			}
			
			var guids = new List<string>(paths.Length);
			foreach (var path in paths)
			{
				var guid = AssetDatabase.AssetPathToGUID(path);
				if (!string.IsNullOrEmpty(guid))
				{
					guids.Add(guid);
				}
			}

			return guids.ToArray();
		}
		
		public static string[] GetAssetImporterDependencies(string path)
		{
			var importer = AssetImporter.GetAtPath(path);
			if (importer == null)
			{
				Debug.LogWarning(Maintainer.ConstructLog("Couldn't find AssetImporter for " + path));
				return null;
			}
			
			var method = importer.GetType().GetMethod("GatherDependenciesFromSourceFile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				// this importer does not implement optional GatherDependenciesFromSourceFile message (starting from Unity 2020.1)
				return null;
			}
			
			var items = (string[])method.Invoke(null, new [] { path });
			if (items != null && items.Length > 0)
				return items;
			
			return null;
		}

		public static AssetOrigin GetAssetOrigin(string path)
		{
			if (!Path.IsPathRooted(path))
			{
				if (path.IndexOf("Assets/", StringComparison.Ordinal) == 0)
					return AssetOrigin.AssetsFolder;

				if (path.IndexOf("ProjectSettings/", StringComparison.Ordinal) == 0)
					return AssetOrigin.Settings;

				if (path.IndexOf("Packages/", StringComparison.Ordinal) == 0)
				{
					var projectRelativePath = CSPathTools.EnforceSlashes(CSPathTools.GetProjectRelativePath(Path.GetFullPath(path)));
					return projectRelativePath.IndexOf("Packages/", StringComparison.Ordinal) == 0 ? 
						AssetOrigin.EmbeddedPackage : 
						AssetOrigin.ImmutablePackage;
				}
			}
			else
			{
				if (path.IndexOf("/unity/cache/packages/", StringComparison.OrdinalIgnoreCase) > 0)
					return AssetOrigin.ImmutablePackage;
			}

			return AssetOrigin.Unknown;
		}
	}
}