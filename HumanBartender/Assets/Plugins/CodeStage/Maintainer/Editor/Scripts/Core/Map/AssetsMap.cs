#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;
	using Map.Storage;
	using Map.Progress;
	using Map.Processing;
	using Map.Session;

	[Serializable]
	public class AssetsMap
	{
		private const string MapPath = "Library/MaintainerMap.dat";
		private static readonly IAssetsMapStorage storage = new BinaryAssetsMapStorage();
		private static readonly IProgressReporter progressReporter = new MapUpdateProgressReporter();
		private static readonly AssetsMapProcessor processor = new AssetsMapProcessor(progressReporter);

		private static AssetsMap cachedMap;

		internal readonly List<AssetInfo> assets = new List<AssetInfo>();
		public string version;

		[NonSerialized]
		internal bool isDirty;

		[NonSerialized]
		private Dictionary<string, AssetInfo> guidToAssetCache;

		public static AssetsMap CreateNew(out bool canceled)
		{
			Delete();
			return GetUpdated(out canceled);
		}

		public static void Delete()
		{
			cachedMap = null;
			storage.Delete(MapPath);
			AssetsMapSessionCache.ClearSessionCache();
		}

		public static AssetsMap GetUpdated(out bool canceled)
		{
			canceled = false;
			
			if (cachedMap == null)
			{
				cachedMap = AssetsMapSessionCache.TryRestoreCachedMap() ?? storage.Load(MapPath);
			}

			if (cachedMap == null)
			{
				cachedMap = new AssetsMap {version = Maintainer.Version};
				cachedMap.isDirty = true;
			}

			try
			{
				if (processor.ProcessMap(cachedMap))
				{
					if (cachedMap.isDirty)
					{
						storage.Save(MapPath, cachedMap);
						cachedMap.isDirty = false;
					}
					
					// Update session cache after successful processing
					AssetsMapSessionCache.SaveToSessionCache(cachedMap);
				}
				else
				{
					canceled = true;
					cachedMap.assets.Clear();
					cachedMap.InvalidateGuidCache();
					cachedMap = null;
				}
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}

			return cachedMap;
		}

		public static void Save()
		{
			if (cachedMap != null)
			{
				storage.Save(MapPath, cachedMap);
				// Update session cache after saving
				AssetsMapSessionCache.SaveToSessionCache(cachedMap);
			}
		}

		internal static void ResetReferenceEntries()
		{
			if (cachedMap == null)
				cachedMap = storage.Load(MapPath);

			if (cachedMap?.assets == null || cachedMap.assets.Count == 0)
				return;

			var dirty = false;
			
			foreach (var assetInfo in cachedMap.assets)
			{
				foreach (var info in assetInfo.referencedAtInfoList)
				{
					if (info.entries != null && info.entries.Length > 0)
					{
						dirty = true;
						info.entries = null;
					}
				}
			}
			
			if (dirty)
			{
				storage.Save(MapPath, cachedMap);
				// Update session cache to keep it in sync with disk
				AssetsMapSessionCache.SaveToSessionCache(cachedMap);
			}
		}

		internal static AssetInfo GetAssetInfoWithGUID(string guid, out bool canceled)
		{
			var map = cachedMap;
			canceled = false;

			map ??= GetUpdated(out canceled);

			if (map == null)
				return null;

			map.EnsureGuidCacheBuilt();
			return map.guidToAssetCache.TryGetValue(guid, out var assetInfo) ? assetInfo : null;
		}

		internal void InvalidateGuidCache()
		{
			guidToAssetCache = null;
		}

		private void EnsureGuidCacheBuilt()
		{
			if (guidToAssetCache != null)
				return;

			var count = assets.Count;
			guidToAssetCache = new Dictionary<string, AssetInfo>(count);
			for (var i = 0; i < count; i++)
			{
				var asset = assets[i];
				if (asset?.GUID != null)
				{
					guidToAssetCache[asset.GUID] = asset;
				}
			}
		}
	}
}
