#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Issues.Detectors
{
	using Core.Extension;
	using Settings;

	/// <summary>
	/// Base class for all Issues Detectors. Use to extend Issues Detector with your own Issues Detectors.
	/// </summary>
	/// <example>
	/// <code>
	/// // Here is an example of how to create a custom issue detector
	/// internal class ExternalIssueDetector : IssueDetector, IGameObjectBeginIssueDetector
	/// {
	///     public override DetectorInfo Info =>
	///         DetectorInfo.From(
	///             IssueGroup.Other,
	///             DetectorKind.Defect,
	///             IssueSeverity.Warning,
	///             "Custom problem (⌐■_■)",
	///             "External issue detector example: checks 'ValidationTarget' Game Object hideFlags to be HideFlags.NotEditable.");
	/// 
	///     public void GameObjectBegin(DetectorResults results, GameObjectLocation location)
	///     {
	///         if (location.GameObject.name != "ValidationTarget")
	///             return;
	/// 
	///         if (location.GameObject.hideFlags == HideFlags.NotEditable)
	///             return;
	/// 
	///         var issue = GameObjectIssueRecord.ForGameObject(this, IssueKind.Other, location);
	///         issue.BodyPostfix = "Incorrect hideFlags: " + location.GameObject.hideFlags + " while " + HideFlags.NotEditable + " expected!";
	///         results.Add(issue);
	///     }
	/// }
	/// </code>	
	/// </example>

	/// <seealso cref="IAssetBeginIssueDetector"/>
	/// <seealso cref="IAssetEndIssueDetector"/>
	/// <seealso cref="IGameObjectBeginIssueDetector"/>
	/// <seealso cref="IGameObjectEndIssueDetector"/>
	/// <seealso cref="IComponentBeginIssueDetector"/>
	/// <seealso cref="IComponentEndIssueDetector"/>
	/// <seealso cref="IPropertyIssueDetector"/>
	/// <seealso cref="ISceneBeginIssueDetector"/>
	/// <seealso cref="ISceneEndIssueDetector"/>
	/// <seealso cref="ISettingsAssetBeginIssueDetector"/>
	/// <seealso cref="IUnityEventIssueDetector"/>
	public abstract class IssueDetector : MaintainerExtension, IIssueDetector
	{
		/// <summary>
		/// Represents this detector information, such as name, severity and so on.
		/// </summary>
		public abstract DetectorInfo Info { get; }
		
		protected override bool Enabled
		{
			get => this.GetEnabled();
			set => this.SetEnabled(value);
		}
	}
	
	internal static class IssueDetectorExtensions
	{
		public static bool GetEnabled<T>(this T instance) where T : IIssueDetector
		{
			return ProjectSettings.Issues.GetDetectorEnabled(instance);
		}
		
		public static void SetEnabled<T>(this T instance, bool enabled) where T : IIssueDetector
		{
			ProjectSettings.Issues.SetDetectorEnabled(instance, enabled);
		}
	}
}