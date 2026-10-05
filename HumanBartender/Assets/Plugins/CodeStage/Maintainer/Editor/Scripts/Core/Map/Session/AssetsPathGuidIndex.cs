namespace CodeStage.Maintainer.Core.Map.Session
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using UnityEditor;
	using UnityEngine;
	using Core;

	/// <summary>
	/// Maintains a lightweight path to GUID index to avoid deserializing the full assets map.
	/// </summary>
	internal static class AssetsPathGuidIndex
	{
		private const string IndexRelativePath = "Library/MaintainerPathGuidIndex.dat";

		private static readonly string IndexFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../", IndexRelativePath));
		private static Dictionary<string, string> pathToGuidLookup;
		private static bool isLoaded;
		private static bool isDirty;
		private static bool flushScheduled;

		internal static int Count
		{
			get
			{
				EnsureLoaded();
				return pathToGuidLookup?.Count ?? 0;
			}
		}

		internal static void EnsureLoaded()
		{
			if (isLoaded)
				return;

			pathToGuidLookup = LoadFromDisk();
			isLoaded = true;
		}

		internal static void RebuildFromAssets(List<AssetInfo> assets)
		{
			if (assets == null)
				return;

			var count = assets.Count;
			pathToGuidLookup = new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase);

			for (var i = 0; i < count; i++)
			{
				var asset = assets[i];
				if (asset == null)
					continue;

				var path = asset.Path;
				var guid = asset.GUID;
				if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(guid))
					continue;

				pathToGuidLookup[path] = guid;
			}

			SaveToDisk(pathToGuidLookup);
			isLoaded = true;
			isDirty = false;
		}

		internal static bool TryGetGuid(string normalizedPath, out string guid)
		{
			EnsureLoaded();
			if (pathToGuidLookup == null)
			{
				guid = null;
				return false;
			}

			return pathToGuidLookup.TryGetValue(normalizedPath, out guid);
		}

		internal static bool ContainsPath(string normalizedPath)
		{
			EnsureLoaded();
			return pathToGuidLookup != null && pathToGuidLookup.ContainsKey(normalizedPath);
		}

		internal static void Set(string normalizedPath, string guid)
		{
			if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(guid))
				return;

			EnsureLoaded();
			pathToGuidLookup ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			if (pathToGuidLookup.TryGetValue(normalizedPath, out var existing))
			{
				if (existing == guid)
					return;
			}

			pathToGuidLookup[normalizedPath] = guid;
			MarkDirty();
		}

		internal static bool Remove(string normalizedPath)
		{
			if (string.IsNullOrEmpty(normalizedPath))
				return false;

			EnsureLoaded();
			if (pathToGuidLookup == null)
				return false;

			var removed = pathToGuidLookup.Remove(normalizedPath);
			if (removed)
			{
				MarkDirty();
			}

			return removed;
		}

		internal static void Clear()
		{
			pathToGuidLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			isLoaded = true;
			isDirty = false;
			flushScheduled = false;

			try
			{
				if (File.Exists(IndexFullPath))
				{
					File.Delete(IndexFullPath);
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to clear path GUID index: {e.Message}"));
			}
		}

		private static void MarkDirty()
		{
			if (!isLoaded)
				return;

			isDirty = true;
			if (flushScheduled)
				return;

			flushScheduled = true;
			EditorApplication.delayCall += Flush;
		}

		private static void Flush()
		{
			flushScheduled = false;
			if (!isDirty)
				return;

			EnsureLoaded();
			SaveToDisk(pathToGuidLookup);
			isDirty = false;
		}

		private static Dictionary<string, string> LoadFromDisk()
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				if (!File.Exists(IndexFullPath))
					return result;

				using (var stream = new FileStream(IndexFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = new BinaryReader(stream))
				{
					var version = reader.ReadString();
					if (version != Maintainer.Version)
						return result;

					var count = reader.ReadInt32();
					for (var i = 0; i < count; i++)
					{
						var path = reader.ReadString();
						var guid = reader.ReadString();
						if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(guid))
							continue;

						result[path] = guid;
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to load path GUID index: {e.Message}"));
			}

			return result;
		}

		private static void SaveToDisk(Dictionary<string, string> source)
		{
			if (source == null)
				return;

			try
			{
				var directory = Path.GetDirectoryName(IndexFullPath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				using (var stream = new FileStream(IndexFullPath, FileMode.Create, FileAccess.Write, FileShare.None))
				using (var writer = new BinaryWriter(stream))
				{
					writer.Write(Maintainer.Version);
					writer.Write(source.Count);
					foreach (var pair in source)
					{
						writer.Write(pair.Key);
						writer.Write(pair.Value);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to save path GUID index: {e.Message}"));
			}
		}
	}
}


