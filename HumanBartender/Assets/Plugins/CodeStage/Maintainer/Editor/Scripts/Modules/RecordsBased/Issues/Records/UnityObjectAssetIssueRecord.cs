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
	using Tools;
	using UI;
	using UnityEditor;
	using UnityEngine;
	using Location = Core.Location;

	[Serializable]
	public class UnityObjectAssetIssueRecord : AssetIssueRecord, IShowableRecord
	{
		public string propertyPath;
		public string typeName;
		public string humanReadablePropertyName;

		[SerializeField]
		private bool missingEventMethod;

		public override bool IsFixable
		{
			get
			{
				return Kind == IssueKind.MissingReference && !missingEventMethod;
			}
		}

		public void Show()
		{
			var entry = new ReferencingEntryData
			{
				location = Location.UnityObjectAsset,
				propertyPath = propertyPath,
			};
			
			CSSelectionTools.RevealAndSelectReferencingEntry(Path, entry);
		}

		internal static UnityObjectAssetIssueRecord Create(IIssueDetector detector, IssueKind type, AssetLocation location)
		{
			return new UnityObjectAssetIssueRecord(detector, type, location);
		}

		internal static UnityObjectAssetIssueRecord Create(IIssueDetector detector, IssueKind type, ComponentLocation location)
		{
			return new UnityObjectAssetIssueRecord(detector, type, location);
		}

		internal static UnityObjectAssetIssueRecord Create(IIssueDetector detector, IssueKind type, PropertyLocation location)
		{
			return new UnityObjectAssetIssueRecord(detector, type, location);
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
					return !string.IsNullOrEmpty(typeName) && CSFilterTools.IsValueMatchesAnyFilterOfKind(typeName, filters, newFilter.kind);
				}
				case FilterKind.NotSet:
					return false;
				default:
					Debug.LogWarning(Maintainer.ErrorForSupport("Unknown filter kind: " + newFilter.kind, IssuesFinder.ModuleName));
					return false;
			}
		}

		internal UnityObjectAssetIssueRecord(IIssueDetector detector, IssueKind kind, AssetLocation location) : base(detector, kind, location)
		{

		}

		internal UnityObjectAssetIssueRecord(IIssueDetector detector, IssueKind kind, ComponentLocation location) : this(detector, kind, location as AssetLocation)
		{
			typeName = location.ComponentName;
		}

		internal UnityObjectAssetIssueRecord(IIssueDetector detector, IssueKind kind, PropertyLocation location) : this(detector, kind, location as ComponentLocation)
		{
			propertyPath = location.PropertyPath;
			humanReadablePropertyName = location.HumanReadablePropertyName;

			if (propertyPath.EndsWith("].m_MethodName", StringComparison.OrdinalIgnoreCase))
			{
				missingEventMethod = true;
			}
		}

		protected override void ConstructBody(StringBuilder text)
		{
			text.Append("<b>Asset:</b> ");
			text.Append(CSPathTools.NicifyAssetPath(Path, true));

			if (!string.IsNullOrEmpty(typeName))
			{
				text.Append("\n<b>Type:</b> ").Append(typeName);
			}

			AppendPropertyInfo(text, propertyPath, humanReadablePropertyName);
		}

		internal override FixResult PerformFix(bool batchMode)
		{
			FixResult result;
			var unityObject = AssetDatabase.LoadMainAssetAtPath(Path);

			if (unityObject == null)
			{
				result = new FixResult(false);
				if (batchMode)
				{
					Debug.LogWarning(Maintainer.ConstructLog("Can't find Unity Object for issue:\n" + this, IssuesFinder.ModuleName));
				}
				else
				{
					result.SetErrorText("Couldn't find Unity Object\n" + Path);
				}
				return result;
			}

			result = IssuesFixer.FixMissingReference(unityObject, propertyPath, LocationGroup.Asset);
			return result;
		}
	}
}