#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Storage
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using UnityEngine;

	internal static class AssetsMapBinarySerializer
	{
		private const int FormatVersion = 2;

		/// <summary>
		/// Bump this version when any map processing logic changes (dependency parsing, reference building, etc.).
		/// This triggers a full map rebuild for users who update to a version with a different MapDataVersion.
		/// </summary>
		internal const int MapDataVersion = 2;

		public static void Write(BinaryWriter writer, AssetsMap map)
		{
			writer.Write(FormatVersion);
			writer.Write(Maintainer.Version);
			writer.Write(MapDataVersion);

			var assets = map.assets;
			var count = assets?.Count ?? 0;
			writer.Write(count);

			if (count == 0)
				return;

			// First pass: write all AssetInfo basic data
			for (var i = 0; i < count; i++)
			{
				WriteAssetInfo(writer, assets[i]);
			}

			// Second pass: write references (using GUID indices)
			for (var i = 0; i < count; i++)
			{
				WriteAssetReferences(writer, assets[i]);
			}
		}

		public static AssetsMap Read(BinaryReader reader, out bool versionMismatch)
		{
			versionMismatch = false;

			var formatVersion = reader.ReadInt32();
			if (formatVersion != FormatVersion)
			{
				versionMismatch = true;
				return null;
			}

			var maintainerVersion = reader.ReadString();

			var mapDataVersion = reader.ReadInt32();
			if (mapDataVersion != MapDataVersion)
			{
				versionMismatch = true;
				return null;
			}

			var count = reader.ReadInt32();
			var map = new AssetsMap { version = maintainerVersion };

			if (count == 0)
				return map;

			// Use array to maintain 1:1 correspondence with written data
			// Some slots may be null if the asset failed to deserialize
			var assetsArray = new AssetInfo[count];
			var guidToIndex = new Dictionary<string, int>(count);

			// First pass: read all AssetInfo basic data
			for (var i = 0; i < count; i++)
			{
				var asset = ReadAssetInfo(reader);
				assetsArray[i] = asset;
				if (asset != null && !string.IsNullOrEmpty(asset.GUID))
				{
					guidToIndex[asset.GUID] = i;
				}
			}

			// Second pass: ALWAYS read reference data to maintain stream alignment
			// Only apply references to non-null assets
			for (var i = 0; i < count; i++)
			{
				ReadAssetReferences(reader, assetsArray[i], assetsArray, guidToIndex);
			}

			// Add only non-null assets to the map
			foreach (var asset in assetsArray)
			{
				if (asset != null)
				{
					map.assets.Add(asset);
				}
			}

			return map;
		}

		private static void WriteAssetInfo(BinaryWriter writer, AssetInfo asset)
		{
			if (asset == null)
			{
				writer.Write(false);
				return;
			}

			writer.Write(true);
			WriteNullableString(writer, asset.GUID);
			WriteNullableString(writer, asset.Path);
			writer.Write((byte)asset.Origin);
			writer.Write((byte)asset.SettingsKind);
			WriteType(writer, asset.Type);
			writer.Write(asset.needToRebuildReferences);
			WriteNullableString(writer, GetLastDependenciesHashSerialized(asset));

			// Write dependencies GUIDs
			var depsCount = asset.dependenciesGUIDs?.Count ?? 0;
			writer.Write(depsCount);
			if (depsCount > 0)
			{
				foreach (var guid in asset.dependenciesGUIDs)
				{
					WriteNullableString(writer, guid);
				}
			}
		}

		private static AssetInfo ReadAssetInfo(BinaryReader reader)
		{
			var hasValue = reader.ReadBoolean();
			if (!hasValue)
				return null;

			var guid = ReadNullableString(reader);
			var path = ReadNullableString(reader);
			var origin = (AssetOrigin)reader.ReadByte();
			var settingsKind = (AssetSettingsKind)reader.ReadByte();
			var type = ReadType(reader);
			var needToRebuildReferences = reader.ReadBoolean();
			var lastDependenciesHashSerialized = ReadNullableString(reader);

			var asset = AssetInfo.CreateForDeserialization(guid, path, origin, settingsKind, type);
			asset.needToRebuildReferences = needToRebuildReferences;
			SetLastDependenciesHashSerialized(asset, lastDependenciesHashSerialized);

			// Read dependencies GUIDs
			var depsCount = reader.ReadInt32();
			if (depsCount > 0)
			{
				asset.dependenciesGUIDs.Clear();
				asset.dependenciesGUIDs.Capacity = depsCount;
				for (var i = 0; i < depsCount; i++)
				{
					var depGuid = ReadNullableString(reader);
					if (!string.IsNullOrEmpty(depGuid))
						asset.dependenciesGUIDs.Add(depGuid);
				}
			}

			return asset;
		}

		private static void WriteAssetReferences(BinaryWriter writer, AssetInfo asset)
		{
			// Handle null asset - write empty reference data to maintain format consistency
			if (asset == null)
			{
				writer.Write(0); // refsCount = 0
				writer.Write(0); // refsAtCount = 0
				return;
			}

			// Write assetReferencesInfo (forward references)
			var refsCount = asset.assetReferencesInfo?.Count ?? 0;
			writer.Write(refsCount);
			if (refsCount > 0)
			{
				foreach (var refInfo in asset.assetReferencesInfo)
				{
					WriteNullableString(writer, refInfo?.assetInfo?.GUID);
				}
			}

			// Write referencedAtInfoList (backward references)
			var refsAtCount = asset.referencedAtInfoList?.Count ?? 0;
			writer.Write(refsAtCount);
			if (refsAtCount > 0)
			{
				foreach (var refAtInfo in asset.referencedAtInfoList)
				{
					WriteNullableString(writer, refAtInfo?.assetInfo?.GUID);
					WriteReferencingEntries(writer, refAtInfo?.entries);
				}
			}
		}

		private static void ReadAssetReferences(BinaryReader reader, AssetInfo asset, 
			AssetInfo[] assetsArray, Dictionary<string, int> guidToIndex)
		{
			// Read assetReferencesInfo (forward references)
			// ALWAYS read to maintain stream alignment, even if asset is null
			var refsCount = reader.ReadInt32();
			if (refsCount > 0)
			{
				if (asset != null)
				{
					asset.assetReferencesInfo.Clear();
					asset.assetReferencesInfo.Capacity = refsCount;
				}

				for (var i = 0; i < refsCount; i++)
				{
					var refGuid = ReadNullableString(reader);
					if (asset != null && !string.IsNullOrEmpty(refGuid) && guidToIndex.TryGetValue(refGuid, out var index))
					{
						var referencedAsset = assetsArray[index];
						if (referencedAsset != null)
						{
							asset.assetReferencesInfo.Add(new AssetReferenceInfo { assetInfo = referencedAsset });
						}
					}
				}
			}

			// Read referencedAtInfoList (backward references)
			// ALWAYS read to maintain stream alignment, even if asset is null
			var refsAtCount = reader.ReadInt32();
			if (refsAtCount > 0)
			{
				if (asset != null)
				{
					asset.referencedAtInfoList.Clear();
					asset.referencedAtInfoList.Capacity = refsAtCount;
				}

				for (var i = 0; i < refsAtCount; i++)
				{
					var refAtGuid = ReadNullableString(reader);
					var entries = ReadReferencingEntries(reader);

					if (asset != null && !string.IsNullOrEmpty(refAtGuid) && guidToIndex.TryGetValue(refAtGuid, out var index))
					{
						var referencedAsset = assetsArray[index];
						if (referencedAsset != null)
						{
							var refAtInfo = new ReferencedAtAssetInfo
							{
								assetInfo = referencedAsset,
								entries = entries
							};
							asset.referencedAtInfoList.Add(refAtInfo);
						}
					}
				}
			}
		}

		private static void WriteReferencingEntries(BinaryWriter writer, ReferencingEntryData[] entries)
		{
			var count = entries?.Length ?? 0;
			writer.Write(count);
			if (count == 0)
				return;

			foreach (var entry in entries)
			{
				if (entry == null)
				{
					writer.Write(false);
					continue;
				}

				writer.Write(true);
				writer.Write((byte)entry.location);
				WriteNullableString(writer, entry.prefixLabel);
				WriteNullableString(writer, entry.transformPath);
				WriteNullableString(writer, entry.componentName);
				WriteNullableString(writer, entry.propertyPath);
				WriteNullableString(writer, entry.suffixLabel);
				writer.Write(entry.objectInstanceId);
				writer.Write(entry.objectId);
				writer.Write(entry.componentInstanceId);
				writer.Write(entry.componentId);
			}
		}

		private static ReferencingEntryData[] ReadReferencingEntries(BinaryReader reader)
		{
			var count = reader.ReadInt32();
			if (count == 0)
				return null;

			var entries = new ReferencingEntryData[count];
			for (var i = 0; i < count; i++)
			{
				var hasValue = reader.ReadBoolean();
				if (!hasValue)
				{
					entries[i] = null;
					continue;
				}

				entries[i] = new ReferencingEntryData
				{
					location = (Location)reader.ReadByte(),
					prefixLabel = ReadNullableString(reader),
					transformPath = ReadNullableString(reader),
					componentName = ReadNullableString(reader),
					propertyPath = ReadNullableString(reader),
					suffixLabel = ReadNullableString(reader),
					objectInstanceId = reader.ReadInt64(),
					objectId = reader.ReadInt64(),
					componentInstanceId = reader.ReadInt64(),
					componentId = reader.ReadInt64()
				};
			}

			return entries;
		}

		private static void WriteType(BinaryWriter writer, Type type)
		{
			if (type == null)
			{
				writer.Write(string.Empty);
				return;
			}

			writer.Write(type.AssemblyQualifiedName ?? string.Empty);
		}

		private static Type ReadType(BinaryReader reader)
		{
			var typeName = reader.ReadString();
			if (string.IsNullOrEmpty(typeName))
				return null;

			try
			{
				return Type.GetType(typeName, false);
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static void WriteNullableString(BinaryWriter writer, string value)
		{
			var hasValue = !string.IsNullOrEmpty(value);
			writer.Write(hasValue);
			if (hasValue)
				writer.Write(value);
		}

		private static string ReadNullableString(BinaryReader reader)
		{
			var hasValue = reader.ReadBoolean();
			return hasValue ? reader.ReadString() : null;
		}

		private static string GetLastDependenciesHashSerialized(AssetInfo asset)
		{
			// Access the private field via reflection-like approach
			// Since we're in the same assembly, we can add an internal accessor
			return asset.GetLastDependenciesHashSerializedForStorage();
		}

		private static void SetLastDependenciesHashSerialized(AssetInfo asset, string value)
		{
			asset.SetLastDependenciesHashSerializedForStorage(value);
		}
	}
}

