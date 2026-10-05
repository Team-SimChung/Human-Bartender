#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues
{
	using System;
	using System.Text;
	using Core;
	using Core.Scan;
	using Detectors;
	using UnityEngine;
	using Object = UnityEngine.Object;
	using Tools;
	using UI;
	using Location = Core.Location;

	[Serializable]
	public class GameObjectIssueRecord : AssetIssueRecord, IShowableRecord
	{
		public string transformPath;
		public long objectId;
		public string componentName;
		public string componentNamePostfix;
		public long componentIndex = -1;
		public string propertyPath;
		public string humanReadablePropertyName;

		[SerializeField]
		private bool missingEventMethod;

		public override bool IsFixable
		{
			get
			{
				return (Kind == IssueKind.MissingComponent || Kind == IssueKind.MissingReference) && !missingEventMethod;
			}
		}

		public void Show()
		{
			var entry = new ReferencingEntryData
			{
				location =
					LocationGroup == LocationGroup.Scene
						? Location.SceneGameObject
						: Location.PrefabAssetGameObject,
				transformPath = transformPath,
				componentId = componentIndex,
				propertyPath = propertyPath,
				objectId = objectId
			};

			if (Kind == IssueKind.UnnamedLayer)
				entry.propertyPath = "Layer";
			
			CSSelectionTools.RevealAndSelectReferencingEntry(Path, entry);
		}

		public static GameObjectIssueRecord ForGameObject(IIssueDetector detector, IssueKind type, GameObjectLocation location)
		{
			return new GameObjectIssueRecord(detector, type, location);
		}
		
		public static GameObjectIssueRecord ForComponent(IIssueDetector detector, IssueKind type, ComponentLocation location)
		{
			return new GameObjectIssueRecord(detector, type, location);
		}
		
		public static GameObjectIssueRecord ForProperty(IIssueDetector detector, IssueKind type, PropertyLocation location)
		{
			return new GameObjectIssueRecord(detector, type, location);
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
					return !string.IsNullOrEmpty(Path) && CSFilterTools.IsValueMatchesAnyFilterOfKind(Path, filters, newFilter.kind);
				case FilterKind.Type:
				{
					return !string.IsNullOrEmpty(componentName) && CSFilterTools.IsValueMatchesAnyFilterOfKind(componentName, filters, newFilter.kind);
				}
				case FilterKind.NotSet:
					return false;
				default:
					Debug.LogWarning(Maintainer.ErrorForSupport("Unknown filter kind: " + newFilter.kind, IssuesFinder.ModuleName));
					return false;
			}
		}
		
		internal GameObjectIssueRecord(IIssueDetector detector, IssueKind kind, PropertyLocation location) : this(detector, kind, location as ComponentLocation)
		{
			propertyPath = location.PropertyPath;
			humanReadablePropertyName = location.HumanReadablePropertyName;

			if (!string.IsNullOrEmpty(propertyPath) &&
				propertyPath.EndsWith("].m_MethodName", StringComparison.OrdinalIgnoreCase))
			{
				missingEventMethod = true;
			}
		}

		internal GameObjectIssueRecord(IIssueDetector detector, IssueKind kind, ComponentLocation location) : this(detector, kind, location as GameObjectLocation)
		{
			componentName = location.ComponentName;
			componentIndex = location.ComponentIndex;
			
			if (componentIndex > 0 && location.ComponentType != null && 
				location.GameObject != null &&
				location.GameObject.GetComponents(location.ComponentType).Length > 1)
			{
				componentNamePostfix = " (#" + componentIndex + ")";
			}
		}

		internal GameObjectIssueRecord(IIssueDetector detector, IssueKind kind, GameObjectLocation location) : base(detector, kind, location)
		{
			var transform = location.GameObject.transform;
			transformPath = CSEditorTools.GetFullTransformPath(transform);

			if (kind != IssueKind.MissingPrefab)
			{
				objectId = CSObjectTools.GetUniqueObjectId(location.GameObject);
			}
		}

		protected override void ConstructBody(StringBuilder text)
		{
			text.Append(LocationGroup == LocationGroup.Scene ? "<b>Scene:</b> " : "<b>Prefab:</b> ");

			var nicePath = CSPathTools.NicifyAssetPath(Path, true);

			text.Append(nicePath);

			if (!string.IsNullOrEmpty(transformPath)) text.Append("\n<b>Object:</b> ").Append(transformPath);
			if (!string.IsNullOrEmpty(componentName)) text.Append("\n<b>Component:</b> ").Append(componentName);
			if (!string.IsNullOrEmpty(componentNamePostfix)) text.Append(componentNamePostfix);
			AppendPropertyInfo(text, propertyPath, humanReadablePropertyName);
		}

		internal override FixResult PerformFix(bool batchMode)
		{
			Component component = null;
			FixResult result;

			CSSceneUtils.OpenSceneResult openSceneResult = null;

			if (!batchMode && LocationGroup == LocationGroup.Scene)
			{
				openSceneResult = CSSceneUtils.OpenScene(Path);
				if (!openSceneResult.success)
				{
					return FixResult.CreateError("Couldn't open scene");
				}
			}

			var obj = GetObjectWithThisIssue();
			if (obj == null)
			{
				result = new FixResult(false);
				if (batchMode)
				{
					Debug.LogWarning(Maintainer.ConstructLog("Can't find Object for issue:\n" + this, IssuesFinder.ModuleName));
				}
				else
				{
					result.SetErrorText("Couldn't find Object\n" + transformPath);
				}
				return result;
			}

			if (!string.IsNullOrEmpty(componentName) && obj is GameObject)
			{
				component = GetComponentWithThisIssue(obj as GameObject);

				if (component == null)
				{
					result = new FixResult(false);
					if (batchMode)
					{
						Debug.LogWarning(Maintainer.ConstructLog("Can't find component for issue:\n" + this, IssuesFinder.ModuleName));
					}
					else
					{
						result.SetErrorText("Can't find component\n" + componentName);
					}

					return result;
				}
			}

			result = IssuesFixer.FixObjectIssue(this, obj, component, Kind);

			if (!batchMode && LocationGroup == LocationGroup.Scene && openSceneResult != null)
			{
				CSSceneUtils.SaveScene(openSceneResult.scene);
				CSSceneUtils.CloseOpenedSceneIfNeeded(openSceneResult);
			}

			return result;
		}

		private Object GetObjectWithThisIssue()
		{
			Object result = null;

			if (LocationGroup == LocationGroup.Scene)
			{
				var scene = CSSceneUtils.GetSceneByPath(Path);
				result = CSObjectTools.FindGameObjectInScene(scene, objectId, transformPath);
			}
			else
			{
				var prefabRoot = CSPrefabTools.GetPrefabAssetRoot(Path);
				if (prefabRoot != null)
					result = CSObjectTools.FindChildGameObjectRecursive(prefabRoot.transform, objectId, prefabRoot.transform.name, transformPath);
			}
			return result;
		}

		private Component GetComponentWithThisIssue(GameObject go)
		{
			Component component = null;
			var components = go.GetComponents<Component>();
			for (var i = 0; i < components.Length; i++)
			{
				if (i == componentIndex)
				{
					component = components[i];
					break;
				}
			}

			return component;
		}
	}
}