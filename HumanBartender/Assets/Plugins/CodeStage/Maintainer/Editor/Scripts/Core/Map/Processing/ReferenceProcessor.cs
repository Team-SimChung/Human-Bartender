#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Processing
{
	using System;
	using System.Collections.Generic;
	using Progress;
	using Debug = UnityEngine.Debug;

	internal class ReferenceProcessor
	{
		private readonly IProgressReporter progressReporter;
		private Dictionary<string, AssetInfo> assetsByGuid;
		private List<AssetReferenceInfo> referenceInfosPool;
		private List<ReferencedAtAssetInfo> referencedAtInfosPool;

		public ReferenceProcessor(IProgressReporter progressReporter)
		{
			this.progressReporter = progressReporter;
		}

		public bool ProcessReferences(AssetsMap map)
		{
			if (progressReporter.ShowProgress(3, "Generating links...", 0, 1))
			{
				Debug.Log(Maintainer.ConstructLog("Assets Map update was canceled by user."));
				return false;
			}

			try
			{
				InitializeProcessing(map);
				var count = map.assets.Count;

				for (var i = 0; i < count; i++)
				{
					if (progressReporter.ShowProgress(3, "Generating links... {0}/{1}", i + 1, count))
					{
						Debug.Log(Maintainer.ConstructLog("Assets Map update was canceled by user."));
						return false;
					}

					var asset = map.assets[i];
					
					if (!asset.needToRebuildReferences) continue;

					var dependencies = asset.dependenciesGUIDs;
					if (dependencies == null || dependencies.Count == 0) continue;

					ProcessAssetDependencies(asset, dependencies);
					map.isDirty = true;
				}
			}
			finally
			{
				CleanupProcessing();
			}

			return true;
		}

		private void InitializeProcessing(AssetsMap map)
		{
			// Create GUID lookup dictionary
			assetsByGuid = new Dictionary<string, AssetInfo>(map.assets.Count);
			foreach (var asset in map.assets)
			{
				assetsByGuid[asset.GUID] = asset;
			}

			// Initialize reusable collections
			referenceInfosPool = new List<AssetReferenceInfo>(128);
			referencedAtInfosPool = new List<ReferencedAtAssetInfo>(128);
		}

		private void CleanupProcessing()
		{
			assetsByGuid = null;
			referenceInfosPool = null;
			referencedAtInfosPool = null;
		}

		private int ProcessAssetDependencies(AssetInfo asset, IList<string> dependencies)
		{
			var processedCount = 0;
			referenceInfosPool.Clear();

			foreach (var dependencyGuid in dependencies)
			{
				processedCount++;
				
				if (!assetsByGuid.TryGetValue(dependencyGuid, out var referencedAsset))
					continue;

				// Add forward reference
				referenceInfosPool.Add(new AssetReferenceInfo { assetInfo = referencedAsset });

				// Add backward reference
				var referencedAtInfo = new ReferencedAtAssetInfo { assetInfo = asset };
				referencedAsset.referencedAtInfoList.Add(referencedAtInfo);
			}

			// Set forward references
			asset.assetReferencesInfo.Clear();
			if (referenceInfosPool.Count > 0)
			{
				asset.assetReferencesInfo.AddRange(referenceInfosPool);
			}

			asset.needToRebuildReferences = false;
			return processedCount;
		}
	}
} 