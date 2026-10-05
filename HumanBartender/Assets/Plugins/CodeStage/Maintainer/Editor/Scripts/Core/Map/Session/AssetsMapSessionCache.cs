#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Session
{
	using System;
	using System.IO;
	using Storage;
	using UnityEngine;

	/// <summary>
	/// Manages session-based caching of AssetsMap to persist across domain reloads.
	/// Writes the serialized map into Unity's temp folder and restores it on demand.
	/// </summary>
	internal static class AssetsMapSessionCache
	{
		private const string CacheFileName = "MaintainerMapSession.dat";
		private static readonly string CacheFilePath = Path.Combine(Application.temporaryCachePath, CacheFileName);

		internal static AssetsMap TryRestoreCachedMap()
		{
			try
			{
				if (!File.Exists(CacheFilePath))
					return null;

				bool versionMismatch;
				AssetsMap map;

				using (var stream = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = new BinaryReader(stream))
				{
					map = AssetsMapBinarySerializer.Read(reader, out versionMismatch);
				}

				if (versionMismatch)
				{
					ClearSessionCache();
					return null;
				}

				return map;
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to restore session cache: {e.Message}"));
				ClearSessionCache();
				return null;
			}
		}

		/// <summary>
		/// Saves the AssetsMap to session cache.
		/// </summary>
		/// <param name="map">The map to cache</param>
		public static void SaveToSessionCache(AssetsMap map)
		{
			if (map == null) return;

			try
			{
				var directory = Path.GetDirectoryName(CacheFilePath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				using (var stream = new FileStream(CacheFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
				using (var writer = new BinaryWriter(stream))
				{
					AssetsMapBinarySerializer.Write(writer, map);
				}

				AssetsPathGuidIndex.RebuildFromAssets(map.assets);
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to save session cache: {e.Message}"));
			}
		}

		/// <summary>
		/// Clears the session cache.
		/// </summary>
		public static void ClearSessionCache()
		{
			try
			{
				if (File.Exists(CacheFilePath))
				{
					File.Delete(CacheFilePath);
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to clear session cache: {e.Message}"));
			}

			AssetsPathGuidIndex.Clear();
		}
	}
}
