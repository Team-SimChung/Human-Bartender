#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.ChangeTracking
{
	using System;
	using System.IO;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEditor;
	using UnityEngine;
	using Core;
	using Core.Map.Session;
	using Core.Map.Storage;
	using Tools;

	/// <summary>
	/// Tracks asset changes via AssetPostprocessor and stores deltas in SessionState.
	/// Provides fast change detection to avoid unnecessary map processing.
	/// </summary>
	internal class AssetsChangeTracker : AssetPostprocessor
	{
		private const string SessionKey = "Maintainer.AssetsChangeTracker";
		
		[Serializable]
		private struct ChangeSnapshot
		{
			public string[] addedGuids;
			public string[] changedGuids;
			public string[] deletedGuids;
			public int pathsCount;
			public string version;
		}

		private static ChangeSnapshot currentSnapshot;
		private static bool isInitialized;
		private static bool hasValidSnapshot;
		private static int cachedPathsCount = -1;

		private static void EnsureCachesReady()
		{
			AssetsPathGuidIndex.EnsureLoaded();

			if (cachedPathsCount >= 0)
				return;

			// Always query the actual asset database count as the authoritative source
			// The index might be stale or partially populated from a previous session
			cachedPathsCount = AssetDatabase.GetAllAssetPaths().Length;
		}

		private static void Initialize()
		{
			if (isInitialized) return;

			try
			{
				var snapshotData = SessionState.GetString(SessionKey, null);
				var snapshotLoaded = false;
				if (!string.IsNullOrEmpty(snapshotData))
				{
					currentSnapshot = JsonUtility.FromJson<ChangeSnapshot>(snapshotData);
					
					// Reset if version mismatch
					if (currentSnapshot.version != AssetsMapBinarySerializer.MapDataVersion.ToString())
					{
						ResetSnapshot();
					}
					else
					{
						snapshotLoaded = true;
					}
				}
				else
				{
					ResetSnapshot();
				}

				hasValidSnapshot = snapshotLoaded;

				EnsureCachesReady();
				if (currentSnapshot.pathsCount <= 0)
				{
					currentSnapshot.pathsCount = cachedPathsCount;
				}

				isInitialized = true;
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to initialize change tracker: {e.Message}"));
				// Clear corrupted SessionState data to prevent infinite retry loops
				try
				{
					SessionState.EraseString(SessionKey);
				}
				catch
				{
					// Ignore errors when clearing SessionState
				}
				ResetSnapshot();
				isInitialized = true;
			}
		}

		/// <summary>
		/// Checks if there are any tracked changes since last snapshot.
		/// </summary>
		public static bool HasChanges()
		{
			Initialize();
			return (currentSnapshot.addedGuids?.Length > 0) || 
			       (currentSnapshot.changedGuids?.Length > 0) || 
			       (currentSnapshot.deletedGuids?.Length > 0);
		}

		/// <summary>
		/// Checks if the total asset paths count has changed.
		/// </summary>
		public static bool PathsCountUnchanged()
		{
			Initialize();
			var actualCount = AssetDatabase.GetAllAssetPaths().Length;
			return currentSnapshot.pathsCount == actualCount;
		}

		public static bool IsIndexEmpty()
		{
			Initialize();
			return AssetsPathGuidIndex.Count == 0;
		}

		public static bool HasBaseline()
		{
			Initialize();
			return hasValidSnapshot;
		}

		/// <summary>
		/// Gets the GUIDs of assets that were added since last snapshot.
		/// </summary>
		public static string[] GetAddedGuids()
		{
			Initialize();
			return currentSnapshot.addedGuids ?? Array.Empty<string>();
		}

		/// <summary>
		/// Gets the GUIDs of assets that were changed since last snapshot.
		/// </summary>
		public static string[] GetChangedGuids()
		{
			Initialize();
			return currentSnapshot.changedGuids ?? Array.Empty<string>();
		}

		/// <summary>
		/// Gets the GUIDs of assets that were deleted since last snapshot.
		/// </summary>
		public static string[] GetDeletedGuids()
		{
			Initialize();
			return currentSnapshot.deletedGuids ?? Array.Empty<string>();
		}

		/// <summary>
		/// Takes a snapshot of current state, clearing all tracked changes.
		/// </summary>
		public static void TakeSnapshot()
		{
			Initialize();
			ResetSnapshot();
			SaveSnapshot();
		}

		/// <summary>
		/// Clears all tracked changes and resets the snapshot.
		/// </summary>
		public static void ClearChanges()
		{
			Initialize();
			ResetSnapshot();
			SaveSnapshot();
		}

		/// <summary>
		/// Gets diagnostic information about current tracker state.
		/// </summary>
		public static string GetDiagnosticInfo()
		{
			Initialize();
			var actualCount = AssetDatabase.GetAllAssetPaths().Length;
			return $"Change Tracker State:\n" +
			       $"Added: {currentSnapshot.addedGuids?.Length ?? 0}\n" +
			       $"Changed: {currentSnapshot.changedGuids?.Length ?? 0}\n" +
			       $"Deleted: {currentSnapshot.deletedGuids?.Length ?? 0}\n" +
			       $"Paths Count: {currentSnapshot.pathsCount} (current: {actualCount})\n" +
			       $"Version: {currentSnapshot.version}";
		}

		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, 
			string[] movedAssets, string[] movedFromAssetPaths)
		{
			Initialize();

			// Detect empty/stale index state - track this to skip delta math later
			AssetsPathGuidIndex.EnsureLoaded();
			var indexWasEmpty = AssetsPathGuidIndex.Count == 0;

			// Accumulate changes instead of replacing them
			var addedGuids = new HashSet<string>(currentSnapshot.addedGuids ?? Array.Empty<string>());
			var changedGuids = new HashSet<string>(currentSnapshot.changedGuids ?? Array.Empty<string>());
			var deletedGuids = new HashSet<string>(currentSnapshot.deletedGuids ?? Array.Empty<string>());
			var pathsDelta = 0;

			// Track imported assets (new or modified)
			if (importedAssets != null)
			{
				foreach (var path in importedAssets)
				{
					if (string.IsNullOrEmpty(path))
						continue;

					var guid = AssetDatabase.AssetPathToGUID(path);
					if (string.IsNullOrEmpty(guid))
						continue;

					if (!File.Exists(path))
						continue;

					var normalizedPath = CSPathTools.EnforceSlashes(path);
					var isNewPath = !AssetsPathGuidIndex.ContainsPath(normalizedPath);
					AssetsPathGuidIndex.Set(normalizedPath, guid);
					if (isNewPath)
					{
						pathsDelta++;
					}

					// Could be new or modified - mark for both processing paths
					changedGuids.Add(guid);
					addedGuids.Add(guid);
				}
			}

			// Track moved assets
			if (movedAssets != null)
			{
				var movedCount = movedAssets.Length;
				for (var i = 0; i < movedCount; i++)
				{
					var path = movedAssets[i];
					var guid = AssetDatabase.AssetPathToGUID(path);
					if (!string.IsNullOrEmpty(guid))
					{
						changedGuids.Add(guid);
						var normalizedPath = CSPathTools.EnforceSlashes(path);
						var pathAlreadyTracked = AssetsPathGuidIndex.ContainsPath(normalizedPath);

						if (movedFromAssetPaths != null && i < movedFromAssetPaths.Length)
						{
							var oldPath = CSPathTools.EnforceSlashes(movedFromAssetPaths[i]);
							if (!string.IsNullOrEmpty(oldPath))
							{
								if (AssetsPathGuidIndex.Remove(oldPath))
								{
									pathsDelta--;
								}
							}
						}

						AssetsPathGuidIndex.Set(normalizedPath, guid);

						if (!pathAlreadyTracked)
						{
							pathsDelta++;
						}
					}
				}
			}

			// Track deleted assets
			if (deletedAssets != null)
			{
				foreach (var path in deletedAssets)
				{
					if (string.IsNullOrEmpty(path))
						continue;

					var normalizedPath = CSPathTools.EnforceSlashes(path);
					var guid = AssetDatabase.AssetPathToGUID(path);
					if (string.IsNullOrEmpty(guid))
					{
						// Try to get GUID from our index - if not there, asset was never tracked
						AssetsPathGuidIndex.TryGetGuid(normalizedPath, out guid);
					}
					
					if (!string.IsNullOrEmpty(guid))
					{
						deletedGuids.Add(guid);
					}

					if (AssetsPathGuidIndex.Remove(normalizedPath))
					{
						pathsDelta--;
					}
				}
			}

			// Update snapshot with accumulated changes
			// When index was empty, delta math is unreliable - use actual asset count instead
			if (indexWasEmpty)
			{
				cachedPathsCount = AssetDatabase.GetAllAssetPaths().Length;
			}
			else
			{
				cachedPathsCount = Mathf.Max(0, cachedPathsCount + pathsDelta);
			}

			currentSnapshot.addedGuids = addedGuids.ToArray();
			currentSnapshot.changedGuids = changedGuids.ToArray();
			currentSnapshot.deletedGuids = deletedGuids.ToArray();
			currentSnapshot.pathsCount = cachedPathsCount;
			currentSnapshot.version = AssetsMapBinarySerializer.MapDataVersion.ToString();

			SaveSnapshot();
		}

		private static void ResetSnapshot()
		{
			EnsureCachesReady();
			hasValidSnapshot = false;

			currentSnapshot = new ChangeSnapshot
			{
				addedGuids = Array.Empty<string>(),
				changedGuids = Array.Empty<string>(),
				deletedGuids = Array.Empty<string>(),
				pathsCount = cachedPathsCount,
				version = AssetsMapBinarySerializer.MapDataVersion.ToString()
			};
		}

		private static void SaveSnapshot()
		{
			try
			{
				var snapshotData = JsonUtility.ToJson(currentSnapshot);
				SessionState.SetString(SessionKey, snapshotData);
				hasValidSnapshot = true;
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to save change tracker snapshot: {e.Message}"));
			}
		}
	}
}



