#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.References.Entry
{
	using Core;
	using System.Collections.Generic;
	using Tools;
	using UnityEngine;

	internal static class EntryGenerator
	{
		private class CachedObjectData
		{
			public long objectId;
			public long objectInstanceId;
			public string transformPath;
		}

		private static readonly Dictionary<long, CachedObjectData> CachedObjects = new Dictionary<long, CachedObjectData>();

		public static void ResetCachedObjects()
		{
			CachedObjects.Clear();
		}

		public static ReferencingEntryData CreateNewReferenceEntry(Location currentLocation, Object lookAt, GameObject lookAtGameObject, EntryAddSettings settings)
		{
			var lookAtInstanceId = CSInstanceIdTools.GetId(lookAt);
			CachedObjectData cachedObject;

			if (CachedObjects.ContainsKey(lookAtInstanceId))
			{
				cachedObject = CachedObjects[lookAtInstanceId];
			}
			else
			{
				cachedObject = new CachedObjectData
				{
					objectId = CSObjectTools.GetUniqueObjectId(lookAt),
					objectInstanceId = CSInstanceIdTools.GetId(lookAt),
				};

				if (currentLocation == Location.SceneGameObject || currentLocation == Location.PrefabAssetGameObject)
				{
					if (lookAtGameObject != null)
					{
						var transform = lookAtGameObject.transform;
						Transform stopAt = null;
						if (currentLocation == Location.PrefabAssetGameObject &&
							transform.root.name == "Canvas (Environment)" && 
							transform.childCount == 1)
						{
							stopAt = transform.root;
						}

						cachedObject.transformPath = CSEditorTools.GetFullTransformPath(transform, stopAt);
					}
					else
					{
						cachedObject.transformPath = lookAt.name;
					}
				}
				else if (currentLocation == Location.PrefabAssetObject)
				{
					cachedObject.transformPath = lookAt.name;
				}
				else
				{
					cachedObject.transformPath = string.Empty;
				}

				CachedObjects.Add(lookAtInstanceId, cachedObject);
			}

			var newEntry = new ReferencingEntryData
			{
				location = currentLocation,
				objectId = cachedObject.objectId,
				objectInstanceId = cachedObject.objectInstanceId,
				transformPath = cachedObject.transformPath
			};

			if (settings != null)
			{
				newEntry.componentName = settings.componentName;
				newEntry.componentId = settings.componentIndex;
				newEntry.componentInstanceId = settings.componentInstanceId;
				newEntry.prefixLabel = settings.prefix;
				newEntry.suffixLabel = settings.suffix;
				newEntry.propertyPath = settings.propertyPath;
			}

			return newEntry;
		}
	}
}