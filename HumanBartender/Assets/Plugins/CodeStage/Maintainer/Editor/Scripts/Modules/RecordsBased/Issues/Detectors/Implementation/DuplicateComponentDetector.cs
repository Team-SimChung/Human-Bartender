#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using System;
	using System.Collections.Generic;
	using Core.Scan;
	using Tools;
	using UnityEditor;
	using UnityEngine;
	
	// ReSharper disable once ClassNeverInstantiated.Global since it's used from TypeCache
	internal class DuplicateComponentDetector : IssueDetector, 
		IGameObjectBeginIssueDetector, 
		IComponentBeginIssueDetector,
		IPropertyIssueDetector,
#if !UNITY_2022_1_OR_NEWER
		IUnityEventIssueDetector,
#endif
		IComponentEndIssueDetector
	{
		private readonly struct ComponentHash : IEquatable<ComponentHash>
		{
			private readonly long typeHash;
			private readonly long hash;

			public ComponentHash(long typeHash, long hash)
			{
				this.typeHash = typeHash;
				this.hash = hash;
			}

			public bool Equals(ComponentHash other)
			{
				return typeHash == other.typeHash && hash == other.hash;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj))
					return false;

				return obj is ComponentHash componentHash && Equals(componentHash);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (typeHash.GetHashCode() * 397) ^ hash.GetHashCode();
				}
			}
		}

		private HashSet<Type> duplicateDetector;
		private List<Type> duplicateTypes;
		private List<Component> gameObjectComponents;

		public override DetectorInfo Info =>
			DetectorInfo.From(
				IssueGroup.Component,
				DetectorKind.Defect,
				IssueSeverity.Warning,
				"Duplicate Component", 
				"Search for the multiple instances of the same Component with same values on the same Game Object.");

		public Type[] ComponentTypes => new []{CSReflectionTools.componentType};

		private HashSet<ComponentHash> componentHashes;
		private long newHash;
		private bool skipComponent;
		
		public void GameObjectBegin(DetectorResults results, GameObjectLocation location)
		{
			Reset();
			FindDuplicateTypes(location.GameObject);
		}

		public void ComponentBegin(DetectorResults results, ComponentLocation location)
		{
			newHash = 0;
			skipComponent = !duplicateTypes.Contains(location.ComponentType);

			if (!skipComponent)
			{
				// skipping duplicate search for non-standard components with invisible properties
				var baseType = location.ComponentType.BaseType;
				if (baseType != null)
				{
					if (baseType.Name == "MegaModifier")
						skipComponent = true;
				}
			}
		}
		
		public PropertyScanDepth GetPropertyScanDepth(ComponentLocation location)
		{
			return skipComponent ? PropertyScanDepth.None : PropertyScanDepth.VisibleOnly;
		}

		public void Property(DetectorResults results, PropertyLocation location)
		{
			if (skipComponent)
				return;
			
#if UNITY_2022_1_OR_NEWER
			// root properties hash does takes children into consideration
			if (location.Property.depth != 0)
				return;
#endif
			
			ProcessProperty(location.Property);
		}
		
#if !UNITY_2022_1_OR_NEWER
		public void UnityEventProperty(DetectorResults results, PropertyLocation location, UnityEventScanPhase phase)
		{
			if (skipComponent)
				return;
			
			switch (phase)
			{
				case UnityEventScanPhase.Begin:
				case UnityEventScanPhase.Calls:
				case UnityEventScanPhase.InvalidListener:
					break;
				case UnityEventScanPhase.CallTarget:
				case UnityEventScanPhase.CallMethodName:
				case UnityEventScanPhase.ArgumentType:
				case UnityEventScanPhase.CallMode:
					ProcessProperty(location.Property);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
			}
		}
#endif
		
		public void ComponentEnd(DetectorResults results, ComponentLocation location)
		{
			if (skipComponent)
				return;

			var typeHash = location.ComponentName.GetHashCode();
			var componentHash = new ComponentHash(typeHash, newHash);
			if (componentHashes == null)
				componentHashes = new HashSet<ComponentHash>();

			if (!componentHashes.Add(componentHash))
			{
				var issue = GameObjectIssueRecord.ForComponent(this, IssueKind.DuplicateComponent, location);
				results.Add(issue);
			}
		}
		
		private void Reset()
		{
			componentHashes?.Clear();
			duplicateTypes?.Clear();
			duplicateDetector?.Clear();
			gameObjectComponents?.Clear();
		}
		
		private void FindDuplicateTypes(GameObject target)
		{
			if (gameObjectComponents == null)
				gameObjectComponents = new List<Component>();
			
			target.GetComponents(gameObjectComponents);
			
			if (gameObjectComponents.Count > 0)
			{
				if (duplicateDetector == null)
					duplicateDetector = new HashSet<Type>();
				
				if (duplicateTypes == null)
					duplicateTypes = new List<Type>();
				
				foreach (var component in gameObjectComponents)
				{
					if (!component)
						continue;
					
					var type = component.GetType();
					if (!duplicateDetector.Add(type))
					{
						duplicateTypes.Add(type);
					}
				}
			}
		}

		private void ProcessProperty(SerializedProperty property)
		{
			if (skipComponent)
				return;
			
			newHash += CSSerializedPropertyTools.GetPropertyHash(property);
		}
	}
}