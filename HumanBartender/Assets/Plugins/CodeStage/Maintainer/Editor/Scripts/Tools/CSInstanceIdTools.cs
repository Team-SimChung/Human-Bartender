#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Tools
{
	using System;
	using UnityEditor;
	using Object = UnityEngine.Object;
	using Shared = CodeStage.EditorCommon.Tools.CSEntityIdTools;

	/// <summary>
	/// Thin long-based facade over the shared cross-version InstanceID → EntityId helper
	/// (CodeStage.EditorCommon.Tools.CSEntityIdTools, in the UAS Common submodule).
	///
	/// Maintainer stores object ids as long, converting to/from the shared opaque ulong handle
	/// at the boundary via lossless bit-reinterpretation (unchecked (long)/(ulong) casts, never
	/// narrowing). All Unity-version handling lives once in the shared helper — including Unity
	/// 6000.5, which escalated the EntityId↔int cast from a deprecation warning (CS0618) to a
	/// hard, non-suppressible error (CS0619).
	///
	/// IMPORTANT: this used to be an int-based facade. On Unity 6000.4+, EntityId.ToULong() can
	/// return values wider than 32 bits, so narrowing to int silently corrupted every id (proven
	/// via runtime testing: a real selection's EntityId got truncated to a small garbage int that
	/// never resolved back to the original object, making GetSelectedAssets() — and therefore
	/// every selection-gated menu item and hotkey — always come back empty on 6000.4/6000.5).
	/// long is used (not ulong) to match Maintainer's existing signed-integer id conventions
	/// elsewhere (e.g. ReferencingEntryData.objectId); the bit pattern round-trips exactly either way.
	///
	/// Named CSInstanceIdTools (not CSEntityIdTools) to avoid a CS0104 ambiguous-reference clash
	/// with the shared class in the many files that import both CodeStage.Maintainer.Tools and
	/// CodeStage.EditorCommon.Tools.
	/// </summary>
	internal static class CSInstanceIdTools
	{
		public static long GetObjectReferenceId(SerializedProperty property) =>
			unchecked((long)Shared.GetObjectReferenceId(property));

		public static void SetObjectReferenceId(SerializedProperty property, long id) =>
			Shared.SetObjectReferenceId(property, unchecked((ulong)id));

		public static long GetId(Object obj) =>
			unchecked((long)Shared.GetId(obj));

		public static Object IdToObject(long id) =>
			Shared.IdToObject(unchecked((ulong)id));

		public static void PingObject(long id) =>
			Shared.PingObject(unchecked((ulong)id));

		public static string GetAssetPath(long id) =>
			Shared.GetAssetPath(unchecked((ulong)id));

		public static bool Contains(long id) =>
			Shared.Contains(unchecked((ulong)id));

		public static bool IsSubAsset(long id) =>
			Shared.IsSubAsset(unchecked((ulong)id));

		public static bool IsMainAsset(long id) =>
			Shared.IsMainAsset(unchecked((ulong)id));

		public static long[] GetSelectionIds() =>
			Array.ConvertAll(Shared.GetSelectionIds(), id => unchecked((long)id));

		public static void SetSelectionIds(long[] ids) =>
			Shared.SetSelectionIds(Array.ConvertAll(ids, id => unchecked((ulong)id)));

		public static void SetActiveId(long id) =>
			Shared.SetActiveId(unchecked((ulong)id));
	}
}
