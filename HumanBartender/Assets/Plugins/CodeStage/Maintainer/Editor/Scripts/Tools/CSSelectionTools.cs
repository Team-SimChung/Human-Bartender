#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Tools
{
	using Core;
	using System;
	using System.IO;
	using UI;
	using UnityEditor;
	using UnityEngine;
	using UnityEngine.SceneManagement;

#if UNITY_2021_2_OR_NEWER
	using UnityEditor.SceneManagement;
#else
	using UnityEditor.Experimental.SceneManagement;
#endif

	internal static class CSSelectionTools
	{
		public static bool RevealAndSelectFileAsset(string assetPath)
		{
			var instanceId = CSAssetTools.GetMainAssetInstanceID(assetPath);
			if (CSInstanceIdTools.Contains(instanceId))
			{
				Selection.activeObject = null;
				EditorApplication.delayCall += () =>
				{
					CSEditorTools.GetInspectorWindow();
					CSInstanceIdTools.SetActiveId(instanceId);
				};

				return true;
			}

			return false;
		}

		public static bool RevealAndSelectSubAsset(string assetPath, string name, long objectId)
		{
			var targetAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			if (targetAssets == null || targetAssets.Length == 0) return false;

			foreach (var targetAsset in targetAssets)
			{
				if (!AssetDatabase.IsSubAsset(targetAsset)) continue;
				if (targetAsset is GameObject || targetAsset is Component) continue;
				if (!string.Equals(targetAsset.name, name, StringComparison.OrdinalIgnoreCase)) continue;

				var assetId = CSObjectTools.GetUniqueObjectId(targetAsset);
				if (assetId != objectId) continue;

				CSEditorTools.GetInspectorWindow();
				CSInstanceIdTools.SetActiveId(CSInstanceIdTools.GetId(targetAsset));
				return true;
			}

			return false;
		}

		public static void RevealAndSelectReferencingEntry(string assetPath, ReferencingEntryData referencingEntry)
		{
			if (!File.Exists(assetPath))
			{
				Debug.LogError(Maintainer.ConstructLog("Can't find asset at path: " + assetPath));
				MaintainerWindow.ShowNotification("Can't show it properly");
				return;
			}

			try
			{
				if (!string.IsNullOrEmpty(assetPath) && 
					(referencingEntry.location == Location.SceneLightingSettings ||
					 referencingEntry.location == Location.SceneNavigationSettings))
				{
					var sceneOpenResult = CSSceneUtils.OpenSceneWithSavePrompt(assetPath);
					if (!sceneOpenResult.success)
					{
						Debug.LogError(Maintainer.ErrorForSupport("Can't open scene " + assetPath));
						MaintainerWindow.ShowNotification("Can't show it properly");
						return;
					}
				}

				var success = false;

				switch (referencingEntry.location)
				{
					case Location.ScriptAsset:
					case Location.UnityObjectAsset:
						success = RevealAndSelectFileAsset(assetPath);
						break;
					case Location.PrefabAssetObject:
						success = RevealAndSelectSubAsset(assetPath, referencingEntry.transformPath,
							referencingEntry.objectId);
						break;
					case Location.PrefabAssetGameObject:
					case Location.SceneGameObject:
						success = RevealAndSelectGameObject(assetPath, referencingEntry.transformPath,
							referencingEntry.objectId, referencingEntry.componentId);
						break;

					case Location.SceneLightingSettings:
						success = CSMenuTools.ShowSceneSettingsLighting();
						break;

					case Location.SceneNavigationSettings:
						success = CSMenuTools.ShowSceneSettingsNavigation();
						break;

					case Location.NotFound:
					case Location.Invisible:
						break;

					case Location.TileMap:
						success = RevealAndSelectGameObject(assetPath, referencingEntry.transformPath,
							referencingEntry.objectId, referencingEntry.componentId);

						// TODO: open tile map editor window?

						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				if (!success)
					MaintainerWindow.ShowNotification("Can't show it properly");
#if UNITY_2021_3_OR_NEWER
				else
					CSHighlightTools.TryHighlightProperty(referencingEntry);
#endif
			}
			catch (Exception e)
			{
				Debug.LogError(Maintainer.ErrorForSupport("Error while revealing and selecting referencing entry: " + e));
				MaintainerWindow.ShowNotification("Can't show it properly");
			}
		}

		public static bool RevealAndSelectGameObject(string assetPath, string transformPath, long objectId, long componentId)
		{
			var enclosingAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
			if (enclosingAssetType == CSReflectionTools.sceneAssetType 
				|| string.IsNullOrEmpty(assetPath) 
				|| assetPath == CSPathTools.UntitledScenePath)
			{
				return RevealAndSelectGameObjectInScene(assetPath, transformPath, objectId, componentId);
			}

			return RevealAndSelectGameObjectInPrefab(assetPath, transformPath, objectId, componentId);
		}

		public static CSSceneUtils.OpenSceneResult OpenSceneForReveal(string path)
		{
			var result = CSSceneUtils.OpenScene(path);
			if (result.success)
			{
				CSSceneUtils.CloseUntitledSceneIfNotDirty();

				if (CSEditorTools.lastRevealSceneOpenResult != null)
				{
					if (CSSceneUtils.IsOpenedSceneNeedsToBeClosed(CSEditorTools.lastRevealSceneOpenResult, path, true))
					{
						if (CSEditorTools.lastRevealSceneOpenResult.scene.isDirty)
						{
							if (!CSSceneUtils.SaveDirtyScenesWithPrompt(
								new[] { CSEditorTools.lastRevealSceneOpenResult.scene }))
							{
								return new CSSceneUtils.OpenSceneResult();
							}
						}
					}

					CSSceneUtils.CloseOpenedSceneIfNeeded(CSEditorTools.lastRevealSceneOpenResult, path, true);
				}

				CSEditorTools.lastRevealSceneOpenResult = result;
			}

			return result;
		}

		public static bool TryFoldAllComponentsExceptId(long componentId)
		{
			var tracker = CSEditorTools.GetActiveEditorTrackerForSelectedObject();
			if (tracker == null)
			{
				Debug.LogError(Maintainer.ErrorForSupport("Can't get active tracker."));
				return false;
			}

			tracker.RebuildIfNecessary();

			var editors = tracker.activeEditors;
			if (editors.Length > 1)
			{
				var targetFound = false;
				var skipCount = 0;

				for (var i = 0; i < editors.Length; i++)
				{
					var editor = editors[i];
					var editorTargetType = editor.target.GetType();
					if (editorTargetType == CSReflectionTools.assetImporterType ||
					    editorTargetType == CSReflectionTools.gameObjectType)
					{
						skipCount++;
						continue;
					}

					if (i - skipCount == componentId)
					{
						var hiddenFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;
						var isHidden = (editor.target.hideFlags & hiddenFlags) != 0;
						targetFound = !isHidden;

						/* known corner cases when editor can't be set to visible via tracker */

						if (editor.serializedObject.targetObject is ParticleSystemRenderer)
						{
							var renderer = (ParticleSystemRenderer)editor.serializedObject.targetObject;
							var ps = renderer.GetComponent<ParticleSystem>();
							componentId = CSComponentTools.GetComponentIndex(ps);
						}

						break;
					}
				}

				if (componentId == -1 || !targetFound)
					return false;

				SwitchTrackersVisibility(tracker, componentId, skipCount);

				// workaround for bug when tracker selection gets reset after scene open
				// (e.g. revealing TMP component in new scene)
				EditorApplication.delayCall += () =>
				{
					EditorApplication.delayCall += () =>
					{
						try
						{
							SwitchTrackersVisibility(tracker, componentId, skipCount);
						}
						catch (Exception)
						{
							// ignored
						}
					};
				};
			}

			return true;
		}

		private static void SwitchTrackersVisibility(ActiveEditorTracker tracker, long componentId, int skipCount)
		{
			for (var i = 1; i < tracker.activeEditors.Length; i++)
			{
				if (componentId != -1)
					tracker.SetVisible(i, i - skipCount != componentId ? 0 : 1);
				else
					tracker.SetVisible(i, tracker.activeEditors[i].target != null ? 0 : 1);
			}

			var inspectorWindow2 = CSEditorTools.GetInspectorWindow();
			if (inspectorWindow2 != null)
			{
				inspectorWindow2.Repaint();
			}
		}

		private static bool RevealAndSelectGameObjectInScene(string path, string transformPath, long objectId, long componentId)
		{
			Scene targetScene;

			if (!string.IsNullOrEmpty(path))
			{
				var openResult = OpenSceneForReveal(path);
				if (!openResult.success) 
					return false;

				targetScene = openResult.scene;
			}
			else
			{
				targetScene = CSSceneUtils.GetUntitledScene();
			}

			if (!targetScene.IsValid())
			{
				Debug.LogError(Maintainer.ErrorForSupport("Target scene is not valid or not found! Scene path: " + path + ", looked for ObjectID " + objectId + "!"));
				return false;
			}

			var target = CSObjectTools.FindGameObjectInScene(targetScene, objectId, transformPath);

			if (target == null)
			{
				Debug.LogError(Maintainer.ErrorForSupport("Couldn't find target Game Object " + transformPath + " at " + path + " with ObjectID " + objectId + "!"));
				return false;
			}

			// workaround for a bug when Unity doesn't expand hierarchy in scene
			EditorApplication.delayCall += () =>
			{
				EditorGUIUtility.PingObject(target);
			};

			SelectGameObject(target, true);

			var enclosingAssetInstanceId = CSAssetTools.GetMainAssetInstanceID(path);
			EditorApplication.delayCall += () =>
			{
				CSInstanceIdTools.PingObject(enclosingAssetInstanceId);
			};

			return componentId == -1 || TryFoldAllComponentsExceptId(componentId);
		}

		private static bool RevealAndSelectGameObjectInPrefab(string path, string transformPath, long objectId, long componentId)
		{
			bool prefabStageOpened;
			var prefabRoot = CSPrefabTools.OpenPrefabAndReturnRoot(path, out prefabStageOpened);
			if (prefabRoot == null)
				return false;

			var rootPath = prefabRoot.transform.name; // assuming root path is the same as the prefab root name
			var target = CSObjectTools.FindChildGameObjectRecursive(prefabRoot.transform, objectId, rootPath, transformPath);

			EditorApplication.delayCall += () =>
			{
				SelectGameObject(target, false);
				EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));

				if (componentId != -1)
				{
					EditorApplication.delayCall += () =>
					{
						TryFoldAllComponentsExceptId(componentId);
					};
				}
			};

			return true;
		}

		public static void SelectGameObject(GameObject go, bool inScene)
		{
			CSEditorTools.GetInspectorWindow();
			if (inScene)
			{
				Selection.activeTransform = go == null ? null : go.transform;
			}
			else
			{
				Selection.activeGameObject = go;
			}
		}
	}
}
