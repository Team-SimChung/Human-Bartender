#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	using Cleaner;
	using Core;
	using Issues;
	using References;
	using EditorCommon.Tools;

	using UnityEditor;
	using UnityEngine;
	using Debug = UnityEngine.Debug;

	internal static class SearchResultsStorage
	{
		private const string Directory = "Temp";
		private const string IssuesResultsPath = Directory + "/MaintainerIssuesResults.bin";
		private const string CleanerResultsPath = Directory + "/MaintainerCleanerResults.bin";

		private const string ProjectReferencesResultsPath = Directory + "/MaintainerProjectReferencesResults.bin";
		private const string ProjectReferencesLastSearchedPath = Directory + "/MaintainerProjectReferencesLastSearched.bin";

		private const string SceneReferencesResultsPath = Directory + "/MaintainerSceneReferencesResults.bin";
		private const string SceneReferencesLastSearchedPath = Directory + "/MaintainerSceneReferencesLastSearched.bin";

		private const int FormatVersion = 1;
		private const int ProgressItemsThreshold = 40000;
		private const long ProgressStreamLengthThreshold = 500000;

		private static IssueRecord[] issuesSearchResults;
		private static CleanerRecord[] cleanerSearchResults;

		private static ProjectReferenceItem[] projectReferencesSearchResults;
		private static FilterItem[] projectReferencesLastSearched;

		private static HierarchyReferenceItem[] sceneReferencesSearchResults;
		private static long[] sceneReferencesLastSearched;

		public static void Clear()
		{
			CSFileTools.DeleteFile(IssuesResultsPath);
			CSFileTools.DeleteFile(CleanerResultsPath);

			CSFileTools.DeleteFile(ProjectReferencesResultsPath);
			CSFileTools.DeleteFile(ProjectReferencesLastSearchedPath);

			CSFileTools.DeleteFile(SceneReferencesResultsPath);
			CSFileTools.DeleteFile(SceneReferencesLastSearchedPath);
		}

		private static void EnsureDirectoryExists()
		{
			if (!System.IO.Directory.Exists(Directory))
				System.IO.Directory.CreateDirectory(Directory);
		}

		public static IssueRecord[] IssuesSearchResults
		{
			get
			{
				if (issuesSearchResults == null)
				{
					issuesSearchResults = LoadPolymorphicItems<IssueRecord>(IssuesResultsPath);
				}
				return issuesSearchResults;
			}
			set
			{
				issuesSearchResults = value;
				SavePolymorphicItems(IssuesResultsPath, issuesSearchResults);
			}
		}

		public static CleanerRecord[] CleanerSearchResults
		{
			get
			{
				if (cleanerSearchResults == null)
				{
					cleanerSearchResults = LoadPolymorphicItems<CleanerRecord>(CleanerResultsPath);
				}
				return cleanerSearchResults;
			}
			set
			{
				cleanerSearchResults = value;
				SavePolymorphicItems(CleanerResultsPath, cleanerSearchResults);
			}
		}

		public static ProjectReferenceItem[] ProjectReferencesSearchResults
		{
			get
			{
				if (projectReferencesSearchResults == null)
				{
					projectReferencesSearchResults = LoadItemsFromJson<ProjectReferenceItem>(ProjectReferencesResultsPath);
				}
				return projectReferencesSearchResults;
			}
			set
			{
				projectReferencesSearchResults = value;
				SaveItemsToJson(ProjectReferencesResultsPath, projectReferencesSearchResults);
			}
		}

		public static HierarchyReferenceItem[] HierarchyReferencesSearchResults
		{
			get
			{
				if (sceneReferencesSearchResults == null)
					sceneReferencesSearchResults = LoadItemsFromJson<HierarchyReferenceItem>(SceneReferencesResultsPath);
				return sceneReferencesSearchResults;
			}
			set
			{
				sceneReferencesSearchResults = value;
				SaveItemsToJson(SceneReferencesResultsPath, sceneReferencesSearchResults);
			}
		}

		public static FilterItem[] ProjectReferencesLastSearched
		{
			get
			{
				if (projectReferencesLastSearched == null)
				{
					projectReferencesLastSearched = LoadItemsFromJson<FilterItem>(ProjectReferencesLastSearchedPath);
				}
				return projectReferencesLastSearched;
			}
			set
			{
				projectReferencesLastSearched = value;
				SaveItemsToJson(ProjectReferencesLastSearchedPath, projectReferencesLastSearched);
			}
		}

		public static long[] HierarchyReferencesLastSearched
		{
			get
			{
				if (sceneReferencesLastSearched == null)
				{
					sceneReferencesLastSearched = LoadLongArray(SceneReferencesLastSearchedPath);
				}
				return sceneReferencesLastSearched;
			}
			set
			{
				sceneReferencesLastSearched = value;
				SaveLongArray(SceneReferencesLastSearchedPath, sceneReferencesLastSearched);
			}
		}

		private static void SavePolymorphicItems<T>(string path, T[] items) where T : class
		{
			if (items == null)
			{
				items = Array.Empty<T>();
			}

			EnsureDirectoryExists();

			var showProgress = items.Length > ProgressItemsThreshold;
			if (showProgress)
			{
				EditorUtility.DisplayProgressBar("Maintainer", "Saving items, please wait...", 0.5f);
			}

			try
			{
				using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
				using (var writer = new BinaryWriter(stream))
				{
					writer.Write(FormatVersion);
					writer.Write(Maintainer.Version);
					writer.Write(items.Length);

					foreach (var item in items)
					{
						if (item == null)
						{
							writer.Write(string.Empty);
							writer.Write(string.Empty);
							continue;
						}

						var itemType = item.GetType();
						writer.Write(itemType.AssemblyQualifiedName ?? string.Empty);
						writer.Write(JsonUtility.ToJson(item));
					}
				}
			}
			finally
			{
				if (showProgress)
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}

		private static T[] LoadPolymorphicItems<T>(string path) where T : class
		{
			if (!File.Exists(path))
				return Array.Empty<T>();

			var showProgress = false;

			try
			{
				var fileSize = new FileInfo(path).Length;
				if (fileSize > ProgressStreamLengthThreshold)
				{
					EditorUtility.DisplayProgressBar("Maintainer", "Loading items, please wait...", 0.5f);
					showProgress = true;
				}

				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var reader = new BinaryReader(stream))
				{
					var formatVersion = reader.ReadInt32();
					if (formatVersion != FormatVersion)
					{
						CSFileTools.DeleteFile(path);
						return Array.Empty<T>();
					}

					var maintainerVersion = reader.ReadString();
					if (maintainerVersion != Maintainer.Version)
					{
						CSFileTools.DeleteFile(path);
						return Array.Empty<T>();
					}

					var count = reader.ReadInt32();
					var results = new List<T>(count);

					for (var i = 0; i < count; i++)
					{
						var typeName = reader.ReadString();
						var json = reader.ReadString();

						if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(json))
							continue;

						var itemType = Type.GetType(typeName, false);
						if (itemType == null)
							continue;

						if (!typeof(T).IsAssignableFrom(itemType))
						{
							Debug.LogWarning(Maintainer.ConstructLog($"Skipping incompatible type {itemType.Name} when loading {typeof(T).Name} from {path}"));
							continue;
						}

						var item = JsonUtility.FromJson(json, itemType) as T;
						if (item != null)
						{
							results.Add(item);
						}
					}

					return results.ToArray();
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't read search results from " + path + ".\n" +
														 "They might be generated at different Maintainer version.\n" + e));
				CSFileTools.DeleteFile(path);
				return Array.Empty<T>();
			}
			finally
			{
				if (showProgress)
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}

		private static void SaveLongArray(string path, long[] items)
		{
			if (items == null)
			{
				items = Array.Empty<long>();
			}

			EnsureDirectoryExists();

			var showProgress = items.Length > ProgressItemsThreshold;
			if (showProgress)
			{
				EditorUtility.DisplayProgressBar("Maintainer", "Saving int array, please wait...", 0.5f);
			}

			try
			{
				using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
				using (var writer = new BinaryWriter(stream))
				{
					writer.Write(FormatVersion);
					writer.Write(Maintainer.Version);
					writer.Write(items.Length);

					foreach (var item in items)
					{
						writer.Write(item);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog($"Failed to save int array: {e.Message}"));
			}
			finally
			{
				if (showProgress)
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}

		private static long[] LoadLongArray(string path)
		{
			if (!File.Exists(path))
				return Array.Empty<long>();

			var showProgress = false;

			try
			{
				var fileSize = new FileInfo(path).Length;
				if (fileSize > ProgressStreamLengthThreshold)
				{
					EditorUtility.DisplayProgressBar("Maintainer", "Loading int array, please wait...", 0.5f);
					showProgress = true;
				}

				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var reader = new BinaryReader(stream))
				{
					var formatVersion = reader.ReadInt32();
					if (formatVersion != FormatVersion)
					{
						CSFileTools.DeleteFile(path);
						return Array.Empty<long>();
					}

					var maintainerVersion = reader.ReadString();
					if (maintainerVersion != Maintainer.Version)
					{
						CSFileTools.DeleteFile(path);
						return Array.Empty<long>();
					}

					var count = reader.ReadInt32();
					var results = new long[count];

					for (var i = 0; i < count; i++)
					{
						results[i] = reader.ReadInt64();
					}

					return results;
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't read int array from " + path + ".\n" +
														 "They might be generated at different Maintainer version.\n" + e));
				CSFileTools.DeleteFile(path);
				return Array.Empty<long>();
			}
			finally
			{
				if (showProgress)
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}

		private static void SaveItemsToJson<T>(string path, T[] items)
		{
			if (items == null)
			{
				items = Array.Empty<T>();
			}

			EnsureDirectoryExists();

			var shouldShowProgress = items.Length > ProgressItemsThreshold;
			if (shouldShowProgress)
			{
				EditorUtility.DisplayProgressBar("Maintainer", "Saving items, please wait...", 0.5f);
			}

			try
			{
				var wrapper = new ItemsWrapper<T> { items = items };
				var toWrite = JsonUtility.ToJson(wrapper);

				using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
				using (var streamWriter = new StreamWriter(stream))
				{
					streamWriter.Write(toWrite);
				}
			}
			finally
			{
				if (shouldShowProgress)
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}

		private static T[] LoadItemsFromJson<T>(string path)
		{
			if (!File.Exists(path))
				return Array.Empty<T>();

			var progressShown = false;
			try
			{
				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					if (stream.Length > ProgressStreamLengthThreshold)
					{
						EditorUtility.DisplayProgressBar("Maintainer", "Loading items, please wait...", 0.5f);
						progressShown = true;
					}

					using (var streamReader = new StreamReader(stream))
					{
						var wrapper = JsonUtility.FromJson<ItemsWrapper<T>>(streamReader.ReadToEnd());
						var items = wrapper?.items;
						if (items == null || items.Length == 0)
							return Array.Empty<T>();

						var hasNullItem = false;
						for (var i = 0; i < items.Length; i++)
						{
							if (items[i] != null) continue;

							hasNullItem = true;
							break;
						}

						if (hasNullItem)
						{
							Debug.LogWarning(Maintainer.ConstructLog("Cached search results contained null items and were cleared: " + path));
							CSFileTools.DeleteFile(path);
							return Array.Empty<T>();
						}

						return items;
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning(Maintainer.ConstructLog("Can't read search results from " + path + ".\n" +
														 "They might be generated at different Maintainer version.\n" + e));
				CSFileTools.DeleteFile(path);
				return Array.Empty<T>();
			}
			finally
			{
				if (progressShown)
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}

		[Serializable]
		public class ItemsWrapper<T>
		{
			public T[] items;
		}
	}
}
