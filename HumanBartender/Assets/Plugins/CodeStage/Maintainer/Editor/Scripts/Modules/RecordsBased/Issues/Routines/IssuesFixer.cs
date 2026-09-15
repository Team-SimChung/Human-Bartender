#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Core.Scan;
	using Settings;
	using UnityEditor;
	using UnityEngine;
	using Tools;
	using Object = UnityEngine.Object;

	internal static class IssuesFixer
	{
		public static void FixRecords(IssueRecord[] results, bool showProgress = true)
		{
			// Tracks prefab asset paths that have been fully processed during batch fix,
			// so subsequent records for the same prefab can be skipped entirely.
			var processedPrefabPaths = new HashSet<string>();
			try
			{
				AssetDatabase.StartAssetEditing();

				var sortedRecords = GetFixableRecordsSorted(results);
				var count = sortedRecords.Length;

				for (var i = 0; i < count; i++)
				{
					if (showProgress)
					{
						if (IssuesFinder.ShowProgressBar(1, 1, i, count, "Resolving selected issues..."))
						{
							IssuesFinder.operationCanceled = true;
							break;
						}
					}

					var item = sortedRecords[i];

					// When fixing missing components in a prefab, the first record processes
					// all children of the prefab root. Skip subsequent records for the same
					// prefab since all missing components were already removed.
					var prefabRecord = item.Kind == IssueKind.MissingComponent &&
						item.LocationGroup == LocationGroup.PrefabAsset
						? item as AssetIssueRecord
						: null;

					if (prefabRecord != null && processedPrefabPaths.Contains(prefabRecord.Path))
					{
						item.fixResult = new FixResult(true);
						continue;
					}

					try
					{
						var fixResult = FixRecord(item);

						// Track successfully processed prefab assets
						if (fixResult.Success && prefabRecord != null)
						{
							processedPrefabPaths.Add(prefabRecord.Path);
						}

						if (!fixResult.Success)
						{
							Debug.Log(Maintainer.ConstructLog("Couldn't fix record, error: " + fixResult.ErrorText + "\n" + item));
						}
					}
					catch (Exception e)
					{
						Maintainer.PrintExceptionForSupport("Exception while fixing the record:\n" + item, e);
					}
				}

				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
			}
		}

		private static FixResult FixRecord(IssueRecord item)
		{
			if (item.LocationGroup == LocationGroup.Scene)
			{
				var assetIssue = item as AssetIssueRecord;
				if (assetIssue != null)
				{
					var newOpenSceneResult = CSSceneUtils.OpenScene(assetIssue.Path);
					if (!newOpenSceneResult.success)
						return FixResult.CreateError("Couldn't open scene at " + assetIssue.Path);

					if (newOpenSceneResult.sceneWasLoaded)
					{
						if (IssuesFinder.lastOpenSceneResult != null)
						{
							CSSceneUtils.SaveScene(IssuesFinder.lastOpenSceneResult.scene);
							CSSceneUtils.CloseOpenedSceneIfNeeded(IssuesFinder.lastOpenSceneResult);
						}
					}

					if (IssuesFinder.lastOpenSceneResult == null || IssuesFinder.lastOpenSceneResult.scene != newOpenSceneResult.scene)
					{
						IssuesFinder.lastOpenSceneResult = newOpenSceneResult;
					}
				}
			}

			return item.Fix(true);
		}

		public static FixResult FixObjectIssue(GameObjectIssueRecord issue, Object obj, Component component, IssueKind type)
		{
			FixResult result;
			if (type == IssueKind.MissingComponent)
			{
				var go = (GameObject)obj;
				var hasIssue = GameObjectHasMissingComponent(go);

				if (hasIssue)
				{
					if (PrefabUtility.IsPartOfPrefabAsset(go))
					{
						var allTransforms = go.transform.root.GetComponentsInChildren<Transform>(true);
						foreach (var child in allTransforms)
						{
							if (GameObjectHasMissingComponent(child.gameObject))
							{
								FixMissingComponents(issue, child.gameObject, false);
							}
						}
					}
					else
					{
						FixMissingComponents(issue, go, false);
					}
					
					if (!GameObjectHasMissingComponent(go))
					{
						result = new FixResult(true);
					}
					else
					{
						result = FixResult.CreateError("Fix attempt failed!");
					}
				}
				else
				{
					result = new FixResult(true);
				}
			}
			else if (type == IssueKind.MissingReference)
			{
				result = FixMissingReference(component != null ? component : obj, issue.propertyPath, issue.LocationGroup);
			}
			else
			{
				result = FixResult.CreateError("DetectorKind is not supported!");
			}

			return result;
		}

		#region missing component

		// -----------------------------------------------------------------------------
		// fix missing component
		// -----------------------------------------------------------------------------
		
		private static bool FixMissingComponents(GameObjectIssueRecord issue, GameObject go, bool alternative)
		{
			var touched = false;

			// Try the fast path first using Unity's built-in API.
			// This avoids the expensive Select → Inspector Repaint → Tracker Rebuild
			// overhead that the fallback approach requires per component.
			var removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
			if (removedCount > 0)
			{
				touched = true;

				// If all missing components were removed, skip the slow tracker path
				if (!GameObjectHasMissingComponent(go))
				{
					if (issue.LocationGroup == LocationGroup.Scene)
					{
						CSSceneUtils.MarkSceneDirty();
					}
					else
					{
						EditorUtility.SetDirty(go);
					}

					return true;
				}
			}

			// Fallback: use the tracker-based approach for any remaining missing components.
			// This handles edge cases the built-in API may miss (e.g. hidden components).

			if (!alternative)
			{
				CSSelectionTools.SelectGameObject(go, issue.LocationGroup == LocationGroup.Scene);
			}

			var tracker = CSEditorTools.GetActiveEditorTrackerForSelectedObject();
			if (tracker == null)
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't get active tracker."));
				return touched;
			}
			tracker.RebuildIfNecessary();

			var activeEditors = tracker.activeEditors;
			for (var i = activeEditors.Length - 1; i >= 0; i--)
			{
				var editor = activeEditors[i];
				if (editor.serializedObject.targetObject == null)
				{
					Object.DestroyImmediate(editor.target, true);
					touched = true;
				}
			}

			if (alternative)
			{
				return touched;
			}

			if (!touched)
			{
				// missing script could be hidden with hide flags, so let's try select it directly and repeat
				var serializedObject = new SerializedObject(go);
				var componentsArray = serializedObject.FindProperty("m_Component");
				if (componentsArray != null)
				{
					for (var i = componentsArray.arraySize - 1; i >= 0; i--)
					{
						var componentPair = componentsArray.GetArrayElementAtIndex(i);
						var nestedComponent = componentPair.FindPropertyRelative("component");
						if (nestedComponent != null)
						{
							if (CSSerializedPropertyTools.IsPropertyHasMissingReference(nestedComponent))
							{
								var instanceId = CSInstanceIdTools.GetObjectReferenceId(nestedComponent);
								if (instanceId == 0)
								{
									var fileId = nestedComponent.FindPropertyRelative("m_FileID");
									if (fileId != null)
									{
										instanceId = fileId.intValue;
									}
								}

								CSInstanceIdTools.SetSelectionIds(new[] { instanceId });
								touched |= FixMissingComponents(issue, go, true);
							}
						}
						else
						{
							Debug.LogError(Maintainer.ConstructLog("Couldn't find component in component pair!", IssuesFinder.ModuleName));
							break;
						}
					}

					if (touched)
					{
						CSSelectionTools.SelectGameObject(go, issue.LocationGroup == LocationGroup.Scene);
					}
				}
				else
				{
					Debug.LogError(Maintainer.ConstructLog("Couldn't find components array!", IssuesFinder.ModuleName));
				}
			}

			if (touched)
			{
				if (issue.LocationGroup == LocationGroup.Scene)
				{
					CSSceneUtils.MarkSceneDirty();
				}
				else
				{
					EditorUtility.SetDirty(go);
				}
			}

			return touched;
		}

		private static bool GameObjectHasMissingComponent(GameObject go)
		{
			var hasMissingComponent = false;
			var components = go.GetComponents<Component>();
			foreach (var c in components)
			{
				if (c == null)
				{
					hasMissingComponent = true;
					break;
				}
			}
			return hasMissingComponent;
		}
		
		#endregion

		#region missing reference
		// -----------------------------------------------------------------------------
		// fix missing reference
		// -----------------------------------------------------------------------------

		public static FixResult FixMissingReference(Object unityObject, string propertyPath, LocationGroup locationGroup)
		{
			var so = new SerializedObject(unityObject);
			var sp = so.FindProperty(propertyPath);

			if (sp == null)
			{
				Debug.LogWarning(Maintainer.ErrorForSupport("Unexpected behavior! Couldn't find property '" + propertyPath +
													"' at " + unityObject + " object.\n" +
													"This might happen due to object's properties changed after search and before running a fix.\n" +
													"Considering this issue as fixed.", IssuesFinder.ModuleName));
			}
			else if (CSSerializedPropertyTools.IsPropertyHasMissingReference(sp))
			{
				CSInstanceIdTools.SetObjectReferenceId(sp, 0);

				var fileId = sp.FindPropertyRelative("m_FileID");
				if (fileId != null)
				{
					fileId.intValue = 0;
				}

				// fixes dirty scene flag after batch issues fix
				// due to the additional undo action
				so.ApplyModifiedPropertiesWithoutUndo();

				if (locationGroup == LocationGroup.Scene)
				{
					CSSceneUtils.MarkSceneDirty();
				}
				else
				{
					if (unityObject != null) 
						EditorUtility.SetDirty(unityObject);
				}
			}

			return new FixResult(true);
		}
		#endregion
		
		private static IssueRecord[] GetFixableRecordsSorted(IEnumerable<IssueRecord> records)
		{
			return records.Where(record => record.selected && record.IsFixable).OrderBy(RecordsSortings.issueRecordByPath).ToArray();
		}
	}
}
