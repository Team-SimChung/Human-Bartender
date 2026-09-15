#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.EditorCommon.Tools
{
	using System.IO;

	internal static class CSFileTools
	{
		public static void DeleteFile(string path)
		{
			if (!File.Exists(path)) return;
			RemoveReadOnlyAttribute(path);
			File.Delete(path);
		}

		private static void RemoveReadOnlyAttribute(string filePath)
		{
			var attributes = File.GetAttributes(filePath);
			if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
		}
	}
}