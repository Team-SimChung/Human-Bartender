#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.EditorCommon.Tools
{
	using System;
	using UnityEditor;
	using UnityEngine;
	using Object = UnityEngine.Object;

	/// <summary>
	/// Cross-version compatibility layer for the Unity InstanceID → EntityId migration.
	/// The portable handle is a raw <see cref="ulong"/> so it never goes through the
	/// EntityId↔int cast, which Unity escalated from a deprecation warning (CS0618) to a
	/// hard, non-suppressible compile error (CS0619) in Unity 6000.5.
	///
	/// On 6000.4+ the handle is the opaque value from EntityId.ToULong / FromULong (Unity's
	/// recommended conversion). On 6000.3 the EntityId APIs exist but the raw ulong accessors
	/// are not guaranteed, so the still-warning int cast is used (suppressed) and widened to
	/// ulong. On older Unity the legacy int InstanceID is used. int↔ulong widening is bit-exact
	/// for all int values (including negative InstanceIDs) via (ulong)(uint) / (int)(uint), so
	/// handles round-trip losslessly within any single Unity version.
	///
	/// NOTE: GetObjectReferenceId/SetObjectReferenceId switch at UNITY_6000_4_OR_NEWER because
	/// objectReferenceEntityIdValue was introduced in 6000.4, while the other methods switch at
	/// UNITY_6000_3_OR_NEWER because GetEntityId() and related APIs arrived in 6000.3.
	/// </summary>
	internal static class CSEntityIdTools
	{
		public static ulong GetObjectReferenceId(SerializedProperty property)
		{
#if UNITY_6000_4_OR_NEWER
			return EntityId.ToULong(property.objectReferenceEntityIdValue);
#else
#pragma warning disable CS0618
			return (ulong)(uint)property.objectReferenceInstanceIDValue;
#pragma warning restore CS0618
#endif
		}

		public static void SetObjectReferenceId(SerializedProperty property, ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			property.objectReferenceEntityIdValue = EntityId.FromULong(id);
#else
#pragma warning disable CS0618
			property.objectReferenceInstanceIDValue = (int)(uint)id;
#pragma warning restore CS0618
#endif
		}

		public static ulong GetId(Object obj)
		{
#if UNITY_6000_4_OR_NEWER
			return EntityId.ToULong(obj.GetEntityId());
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return (ulong)(uint)(int)obj.GetEntityId();
#pragma warning restore CS0618
#else
			return (ulong)(uint)obj.GetInstanceID();
#endif
		}

		public static Object IdToObject(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			return EditorUtility.EntityIdToObject(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return EditorUtility.EntityIdToObject((EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			return EditorUtility.InstanceIDToObject((int)(uint)id);
#endif
		}

		public static void PingObject(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			EditorGUIUtility.PingObject(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			EditorGUIUtility.PingObject((EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			EditorGUIUtility.PingObject((int)(uint)id);
#endif
		}

		public static string GetAssetPath(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			return AssetDatabase.GetAssetPath(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return AssetDatabase.GetAssetPath((EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			return AssetDatabase.GetAssetPath((int)(uint)id);
#endif
		}

		public static bool Contains(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			return AssetDatabase.Contains(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return AssetDatabase.Contains((EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			return AssetDatabase.Contains((int)(uint)id);
#endif
		}

		public static bool IsSubAsset(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			return AssetDatabase.IsSubAsset(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return AssetDatabase.IsSubAsset((EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			return AssetDatabase.IsSubAsset((int)(uint)id);
#endif
		}

		public static bool IsMainAsset(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			return AssetDatabase.IsMainAsset(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return AssetDatabase.IsMainAsset((EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			return AssetDatabase.IsMainAsset((int)(uint)id);
#endif
		}

		public static ulong[] GetSelectionIds()
		{
#if UNITY_6000_4_OR_NEWER
			return Array.ConvertAll(Selection.entityIds, id => EntityId.ToULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			return Array.ConvertAll(Selection.entityIds, id => (ulong)(uint)(int)id);
#pragma warning restore CS0618
#else
			return Array.ConvertAll(Selection.instanceIDs, id => (ulong)(uint)id);
#endif
		}

		public static void SetSelectionIds(ulong[] ids)
		{
#if UNITY_6000_4_OR_NEWER
			Selection.entityIds = Array.ConvertAll(ids, id => EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			Selection.entityIds = Array.ConvertAll(ids, id => (EntityId)(int)(uint)id);
#pragma warning restore CS0618
#else
			Selection.instanceIDs = Array.ConvertAll(ids, id => (int)(uint)id);
#endif
		}

		public static void SetActiveId(ulong id)
		{
#if UNITY_6000_4_OR_NEWER
			Selection.activeEntityId = EntityId.FromULong(id);
#elif UNITY_6000_3_OR_NEWER
#pragma warning disable CS0618
			Selection.activeEntityId = (EntityId)(int)(uint)id;
#pragma warning restore CS0618
#else
			Selection.activeInstanceID = (int)(uint)id;
#endif
		}
	}
}
