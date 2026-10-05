#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Dependencies;
	using UnityEditor;
	using UnityEngine;
	using System.Runtime.Serialization;

	using Tools;

	internal class RawAssetInfo
	{
		public string path;
		public string guid;
		public AssetOrigin origin;
	}

	[Serializable]
	public class AssetInfo : IEquatable<AssetInfo>, IDeserializationCallback
	{
		/// <summary>
		/// Asset GUID as reported by AssetDatabase.
		/// </summary>
		public string GUID { get; private set; }
		
		/// <summary>
		/// Path to the Asset, as reported by AssetDatabase, with enforced forward slash delimiter (/).
		/// </summary>
		public string Path { get; private set; }
		
		/// <summary>
		/// Represents the asset origin.
		/// </summary>
		public AssetOrigin Origin { get; private set; }
		
		public AssetSettingsKind SettingsKind { get; private set; }
		public Type Type { get; private set; }

		[NonSerialized] 
		private long size = -1;
		
		public long Size 
		{ 
			get
            {
                if (size == -1 && !string.IsNullOrEmpty(Path) && File.Exists(Path))
                    size = new FileInfo(Path).Length;
                return size;
            }
            private set => size = value;
		}
		
		[field:NonSerialized]
		public bool IsUntitledScene { get; private set; }

		internal List<string> dependenciesGUIDs = new List<string>();
		internal List<AssetReferenceInfo> assetReferencesInfo = new List<AssetReferenceInfo>();
		internal List<ReferencedAtAssetInfo> referencedAtInfoList = new List<ReferencedAtAssetInfo>();

		internal bool needToRebuildReferences = true;

		[OptionalField] private string lastDependenciesHashSerialized;
		[NonSerialized] private Hash128 lastDependenciesHash;
		[NonSerialized] private bool lastDependenciesHashInitialized;

		[NonSerialized] private long[] allAssetObjects;

		internal static AssetInfo Create(RawAssetInfo rawAssetInfo, Type type, AssetSettingsKind settingsKind)
		{
			if (string.IsNullOrEmpty(rawAssetInfo.guid))
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't create AssetInfo since guid for file " + rawAssetInfo.path + " is invalid!"));
				return null;
			}

			var newAsset = new AssetInfo
			{
				GUID = rawAssetInfo.guid,
				Path = rawAssetInfo.path,
				Origin = rawAssetInfo.origin,
				Type = type,
				SettingsKind = settingsKind,
			};

			newAsset.UpdateIfNeeded();

			return newAsset;
		}

		internal static AssetInfo CreateUntitledScene()
		{
			return new AssetInfo
			{
				GUID = CSPathTools.UntitledScenePath,
				Path = CSPathTools.UntitledScenePath,
				Origin = AssetOrigin.AssetsFolder,
				Type = CSReflectionTools.sceneAssetType,
				IsUntitledScene = true
			};
		}

		internal static AssetInfo CreateForDeserialization(string guid, string path, AssetOrigin origin, 
			AssetSettingsKind settingsKind, Type type)
		{
			return new AssetInfo
			{
				GUID = guid,
				Path = path,
				Origin = origin,
				SettingsKind = settingsKind,
				Type = type
			};
		}

		internal string GetLastDependenciesHashSerializedForStorage()
		{
			return lastDependenciesHashSerialized;
		}

		internal void SetLastDependenciesHashSerializedForStorage(string value)
		{
			lastDependenciesHashSerialized = value;
			lastDependenciesHashInitialized = false;
		}

		private AssetInfo() { }

		internal bool Exists(bool actualizePath = true)
		{
			if (actualizePath)
				ActualizePath();
			return File.Exists(Path);
		}

		internal bool UpdateIfNeeded()
		{
			if (string.IsNullOrEmpty(Path))
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't update Asset since path is not set!"));
				return false;
			}

			/*if (Path.Contains("qwerty.unity"))
			{
				Debug.Log(Path);
			}*/

			if (!Exists(false))
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't update asset since file is not found:\n" + Path));
				return false;
			}

			var currentHash = AssetDatabase.GetAssetDependencyHash(Path);
			var storedHash = GetStoredDependenciesHash();
			var hashesMatch = currentHash == storedHash;
			if (hashesMatch)
			{
				var dirty = false;

				for (var i = dependenciesGUIDs.Count - 1; i > -1; i--)
				{
					var guid = dependenciesGUIDs[i];
					var path = AssetDatabase.GUIDToAssetPath(guid);
					path = CSPathTools.EnforceSlashes(path);
					if (!string.IsNullOrEmpty(path) && (File.Exists(path) || AssetDatabase.IsValidFolder(path))) 
						continue;

					dirty = true;

					dependenciesGUIDs.RemoveAt(i);

					for (var referenceIndex = assetReferencesInfo.Count - 1; referenceIndex >= 0; referenceIndex--)
					{
						var referenceInfo = assetReferencesInfo[referenceIndex];
						if (referenceInfo.assetInfo.GUID != guid) 
							continue;

						assetReferencesInfo.RemoveAt(referenceIndex);
						break;
					}
				}

				if (!needToRebuildReferences) return dirty;
			}
			else
			{
				Size = -1;
			}

			foreach (var referenceInfo in assetReferencesInfo)
			{
				var referencedAtList = referenceInfo.assetInfo.referencedAtInfoList;
				for (var i = referencedAtList.Count - 1; i >= 0; i--)
				{
					var info = referencedAtList[i];
					if (!info.assetInfo.Equals(this)) 
						continue;

					referencedAtList.RemoveAt(i);
					break;
				}
			}
			
			SetStoredDependenciesHash(currentHash);
			needToRebuildReferences = true;

			assetReferencesInfo.Clear();
			dependenciesGUIDs.Clear();
			dependenciesGUIDs.AddRange(AssetDependenciesSearcher.FindDependencies(this));
			
			return true;
		}

		internal List<AssetInfo> GetReferencesRecursive()
		{
			var result = new List<AssetInfo>();
			GetReferencesRecursive(result);
			return result;
		}

		internal void GetReferencesRecursive(ICollection<AssetInfo> result)
		{
			var visited = new HashSet<AssetInfo>();
			WalkReferencesRecursive(result, visited, assetReferencesInfo);
		}

		internal List<AssetInfo> GetReferencedAtRecursive()
		{
			var result = new List<AssetInfo>();
			GetReferencedAtRecursive(result);
			return result;
		}

		internal void GetReferencedAtRecursive(ICollection<AssetInfo> result)
		{
			var visited = new HashSet<AssetInfo>();
			WalkReferencedAtRecursive(result, visited, referencedAtInfoList);
		}

		internal void Clean()
		{
			foreach (var referenceInfo in assetReferencesInfo)
			{
				var referencedAtList = referenceInfo.assetInfo.referencedAtInfoList;
				for (var i = referencedAtList.Count - 1; i >= 0; i--)
				{
					var info = referencedAtList[i];
					if (!info.assetInfo.Equals(this)) 
						continue;

					referencedAtList.RemoveAt(i);
					break;
				}
			}

			foreach (var referencedAtInfo in referencedAtInfoList)
			{
				var referencesList = referencedAtInfo.assetInfo.assetReferencesInfo;
				for (var i = referencesList.Count - 1; i >= 0; i--)
				{
					var info = referencesList[i];
					if (!info.assetInfo.Equals(this)) 
						continue;
					referencesList.RemoveAt(i);
					referencedAtInfo.assetInfo.needToRebuildReferences = true;
					break;
				}
			}
		}

		internal void ClearAllAssetObjectsCache()
		{
			allAssetObjects = null;
		}

		internal long[] GetAllAssetObjects()
		{
			if (allAssetObjects != null) return allAssetObjects;

			var assetType = Type;
			var assetTypeName = assetType != null ? assetType.Name : null;

			if ((assetType == CSReflectionTools.fontType ||
				assetType == CSReflectionTools.texture2DType ||
				assetType == CSReflectionTools.gameObjectType ||
				assetType == CSReflectionTools.defaultAssetType && Path.EndsWith(".dll") ||
				assetTypeName == "AudioMixerController" ||
				Path.EndsWith("LightingData.asset")) &&
				assetType != CSReflectionTools.lightingDataAsset
			    && assetType != CSReflectionTools.lightingSettings
				)
			{
				var loadedObjects = AssetDatabase.LoadAllAssetsAtPath(Path);
				var referencedObjectsCandidatesList = new List<long>(loadedObjects.Length);
				foreach (var loadedObject in loadedObjects)
				{
					if (loadedObject == null)
						continue;

					var instance = CSInstanceIdTools.GetId(loadedObject);
					if (assetType == CSReflectionTools.gameObjectType)
					{
						var isComponent = loadedObject is Component;
						if (!isComponent &&
							!CSInstanceIdTools.IsSubAsset(instance) &&
							!CSInstanceIdTools.IsMainAsset(instance)) continue;
					}

					referencedObjectsCandidatesList.Add(instance);
				}

				allAssetObjects = referencedObjectsCandidatesList.ToArray();
			}
			else
			{
				var mainAsset = AssetDatabase.LoadMainAssetAtPath(Path);
				allAssetObjects = mainAsset != null ?
					new[] { CSInstanceIdTools.GetId(AssetDatabase.LoadMainAssetAtPath(Path)) } :
					new long[0];
			}

			return allAssetObjects;
		}

		private Hash128 GetStoredDependenciesHash()
		{
			if (!lastDependenciesHashInitialized)
			{
				lastDependenciesHashInitialized = true;
				if (!string.IsNullOrEmpty(lastDependenciesHashSerialized))
				{
					try
					{
						lastDependenciesHash = Hash128.Parse(lastDependenciesHashSerialized);
					}
					catch (Exception)
					{
						lastDependenciesHash = default;
					}
				}
				else
				{
					lastDependenciesHash = default;
				}
			}

			return lastDependenciesHash;
		}

		private void SetStoredDependenciesHash(Hash128 hash)
		{
			lastDependenciesHash = hash;
			lastDependenciesHashSerialized = hash.ToString();
			lastDependenciesHashInitialized = true;
		}

		private void WalkReferencesRecursive(ICollection<AssetInfo> result, HashSet<AssetInfo> visited, IList<AssetReferenceInfo> assetReferenceInfos)
		{
			foreach (var referenceInfo in assetReferenceInfos)
			{
				if (visited.Add(referenceInfo.assetInfo))
				{
					result.Add(referenceInfo.assetInfo);
					WalkReferencesRecursive(result, visited, referenceInfo.assetInfo.assetReferencesInfo);
				}
			}
		}

		private void WalkReferencedAtRecursive(ICollection<AssetInfo> result, HashSet<AssetInfo> visited, IList<ReferencedAtAssetInfo> referencedAtInfos)
		{
			foreach (var referencedAtInfo in referencedAtInfos)
			{
				if (visited.Add(referencedAtInfo.assetInfo))
				{
					result.Add(referencedAtInfo.assetInfo);
					WalkReferencedAtRecursive(result, visited, referencedAtInfo.assetInfo.referencedAtInfoList);
				}
			}
		}

		private void ActualizePath()
		{
			if (Origin == AssetOrigin.ImmutablePackage) return;

			var actualPath = CSPathTools.EnforceSlashes(AssetDatabase.GUIDToAssetPath(GUID));
			if (!string.IsNullOrEmpty(actualPath) && actualPath != Path)
			{
				Path = actualPath;
				allAssetObjects = null;
			}
		}

		public override string ToString()
		{
			var baseType = "N/A";
			if (Type != null && Type.BaseType != null)
				baseType = Type.BaseType.ToString();
			
			return "Asset Info\n" +
				   "Path: " + Path + "\n" +
				   "GUID: " + GUID + "\n" +
				   "Kind: " + Origin + "\n" +
				   "SettingsKind: " + SettingsKind + "\n" +
				   "Size: " + Size + "\n" +
				   "Type: " + Type + "\n" +
				   "Type.BaseType: " + baseType;
		}
		
		public bool Equals(AssetInfo other)
		{
			if (ReferenceEquals(null, other))
				return false;

			if (ReferenceEquals(this, other))
				return true;

			return GUID == other.GUID;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;

			if (ReferenceEquals(this, obj))
				return true;

			if (obj.GetType() != GetType())
				return false;

			return Equals((AssetInfo)obj);
		}

		public override int GetHashCode()
		{
			return GUID != null ? GUID.GetHashCode() : 0;
		}

		public void OnDeserialization(object sender)
		{
			// Reset size to -1 after deserialization since it's [NonSerialized]
			// This ensures Size property will recalculate the file size correctly
			size = -1;
			lastDependenciesHashInitialized = false;
			lastDependenciesHash = default;
		}
	}
}
