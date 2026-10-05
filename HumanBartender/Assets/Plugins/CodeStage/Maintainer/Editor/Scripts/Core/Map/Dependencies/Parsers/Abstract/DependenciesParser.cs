#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Dependencies
{
	using System;
	using System.Collections.Generic;
	using Extension;

	/// <summary>
	/// Base class for all Dependencies Parsers. Use to add your own Dependencies Parsers extensions.
	/// </summary>
	/// <example>
	/// <code language="csharp">
	/// <![CDATA[
	/// // Here is an example of how to create a custom dependencies parser to manually track dependencies
	/// public class ExternalDependencyParser : DependenciesParser
	/// {
	///     public override Type Type => typeof(CustomAssetType);
	///
	///     public override IList<string> GetDependenciesGUIDs(AssetInfo asset)
	///     {
	///         var referencePath = Path.ChangeExtension(asset.Path, "mat");
	///         var referenceGuid = AssetDatabase.AssetPathToGUID(referencePath);
	///
	///         if (!string.IsNullOrEmpty(referenceGuid))
	///             return new []{referenceGuid};
	/// 
	///         Debug.LogError($"Couldn't find reference from {nameof(ExternalDependencyParser)}!");
	///         return null;
	///     }
	/// }
	/// ]]>
	/// </code>
	/// </example>
	public abstract class DependenciesParser : MaintainerExtension, IDependenciesParser
	{
		protected override bool Enabled
		{
			get { return true; }
			set { /* can't be disabled */ }
		}

		public abstract Type Type { get; }
		public abstract IList<string> GetDependenciesGUIDs(AssetInfo asset);
	}
}