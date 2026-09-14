#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Cleaner
{
	using System;
	using System.IO;
	using System.Text;
	using UnityEditor;
	using UnityEngine;
	using Core;
	using Core.Scan;
	using Settings;
	using Tools;
	using EditorCommon.Tools;
	using UI;

	/// <summary>
	/// Project Cleaner search results item representing found asset to be cleaned up.
	/// </summary>
	[Serializable]
	public class AssetRecord : CleanerRecord, IShowableRecord
	{
		/// <summary>
		/// Project-relative asset path (e.g., "Assets/..." or "Packages/...").
		/// </summary>
		public string AssetPath
		{
			get { return assetDatabasePath; }
		}

		[SerializeField] internal string path;
		[SerializeField] internal long size; // in bytes
		[SerializeField] internal string beautyPath;
		[SerializeField] internal string assetDatabasePath;
		[SerializeField] internal string assetTypeName;
		[SerializeField] internal bool isTexture;

		[NonSerialized] private Type cachedAssetType;
		[NonSerialized] private bool assetTypeResolved;

		internal Type assetType
		{
			get
			{
				if (!assetTypeResolved)
				{
					assetTypeResolved = true;
					if (!string.IsNullOrEmpty(assetTypeName))
					{
						try
						{
							cachedAssetType = System.Type.GetType(assetTypeName, false);
						}
						catch (Exception)
						{
							cachedAssetType = null;
						}
					}
				}
				return cachedAssetType;
			}
		}

		void IShowableRecord.Show()
		{
			if (!CSSelectionTools.RevealAndSelectFileAsset(assetDatabasePath))
			{
				MaintainerWindow.ShowNotification("Can't show it properly");
			}
		}

		internal static AssetRecord CreateEmptyFolderRecord(string folderPath)
		{
			var newRecord = new AssetRecord(folderPath);

			if (!string.IsNullOrEmpty(newRecord.path))
			{
				return newRecord;
			}

			return null;
		}

		internal static AssetRecord Create(RecordType type, AssetInfo assetInfo)
		{
			var newRecord = new AssetRecord(type, assetInfo);
			return newRecord;
		}

		internal override bool MatchesFilter(FilterItem newFilter)
		{
			var filters = new[] { newFilter };

			switch (newFilter.kind)
			{
				case FilterKind.Path:
				case FilterKind.Directory:
				case FilterKind.FileName:
				case FilterKind.Extension:
					return !string.IsNullOrEmpty(assetDatabasePath) && CSFilterTools.IsValueMatchesAnyFilterOfKind(assetDatabasePath, filters, newFilter.kind);
				case FilterKind.Type:
					return assetType != null && CSFilterTools.IsValueMatchesAnyFilterOfKind(assetType.FullName, filters, newFilter.kind);
				case FilterKind.NotSet:
					return false;
				default:
					Debug.LogWarning(Maintainer.ErrorForSupport("Unknown filter kind: " + newFilter.kind));
					return false;
			}
		}

		protected AssetRecord(string folderPath) : base(RecordType.EmptyFolder, LocationGroup.Asset)
		{
			path = folderPath;

			assetDatabasePath = CSPathTools.GetProjectRelativePath(folderPath);
			beautyPath = CSPathTools.NicifyAssetPath(assetDatabasePath);
		}

		internal AssetRecord(RecordType type, AssetInfo assetInfo) : base(type, LocationGroup.Asset)
		{
			path = assetInfo.Path;

			assetDatabasePath = CSPathTools.GetProjectRelativePath(path);
			beautyPath = CSPathTools.NicifyAssetPath(assetDatabasePath);

			if (assetInfo.Type != null)
			{
				assetTypeName = assetInfo.Type.AssemblyQualifiedName;
				cachedAssetType = assetInfo.Type;
				assetTypeResolved = true;

				if (assetInfo.Type.BaseType == CSReflectionTools.textureType)
				{
					isTexture = true;
				}
			}

			if (type == RecordType.UnreferencedAsset)
			{
				size = assetInfo.Size;
			}
		}

		protected override void ConstructCompactLine(StringBuilder text)
		{
			text.Append(beautyPath);
		}

		protected override void ConstructHeader(StringBuilder header)
		{
			base.ConstructHeader(header);

			if (type == RecordType.UnreferencedAsset)
			{
				header.Append(assetType.Name);
			}
		}

		protected override void ConstructBody(StringBuilder text)
		{
			text.Append("<b>Path:</b> ").Append(beautyPath);
			if (size > 0)
			{
				text.AppendLine().Append("<b>Size:</b> ").Append(CSEditorTools.FormatBytes(size));
			}
			if (type == RecordType.UnreferencedAsset)
			{
				text.AppendLine().Append("<b>Full Type:</b> ").Append(assetType.FullName);
			}
		}

		protected override bool PerformClean()
		{
			bool result;

			if (ProjectSettings.Cleaner.useTrashBin)
			{
				result = AssetDatabase.MoveAssetToTrash(assetDatabasePath);
			}
			else
			{
				switch (type)
				{
					case RecordType.EmptyFolder:
						{
							if (Directory.Exists(path))
							{
								Directory.Delete(path, true);
							}
							break;
						}
					case RecordType.UnreferencedAsset:
						{
							CSFileTools.DeleteFile(path);
							break;
						}
					case RecordType.Error:
						break;
					case RecordType.Other:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				var metaPath = path + ".meta";
				CSFileTools.DeleteFile(metaPath);
				result = !(Directory.Exists(path) || File.Exists(path));
			}

			if (!result)
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't clean asset: " + beautyPath, ProjectCleaner.ModuleName));
			}
			else
			{
				var directory = CSPathTools.EnforceSlashes(Path.GetDirectoryName(path));
				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					var filesInDir = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);

					if (filesInDir.Length == 0)
					{
						CreateEmptyFolderRecord(directory).Clean();
					}
				}
			}

			return result;
		}
	}
}