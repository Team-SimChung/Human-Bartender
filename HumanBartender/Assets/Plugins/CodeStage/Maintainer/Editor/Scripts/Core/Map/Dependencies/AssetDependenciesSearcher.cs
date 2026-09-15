#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Dependencies
{
	using System.Collections.Generic;
	using System.Linq;
	using Extension;
	using Tools;
	using UnityEditor;

	/// <summary>
	/// Does looks for assets dependencies used at the Maintainer's Assets Map.
	/// </summary>
	public static class AssetDependenciesSearcher
	{
		private static ExtensibleModule<IDependenciesParser> parsersHolder;
		
		static AssetDependenciesSearcher()
		{
			parsersHolder = new ExtensibleModule<IDependenciesParser>();
		}
		
		internal static string[] FindDependencies(AssetInfo asset)
		{
			var dependenciesGUIDs = new HashSet<string>();
			
			/* pre-regular dependenciesGUIDs additions */
			FillDependencies(parsersHolder.extensions, ref dependenciesGUIDs, asset);

			if (asset.Origin != AssetOrigin.Settings)
			{
				/* regular dependenciesGUIDs additions */
				var dependencies = AssetDatabase.GetDependencies(asset.Path, false);
				var guids = CSAssetTools.GetAssetsGUIDs(dependencies);
				if (guids != null && guids.Length > 0)
				{
					dependenciesGUIDs.UnionWith(guids);
				}
			}
			
			// kept for debugging purposes
			/*if (Path.Contains("1.unity"))
			{
				Debug.Log("1.unity non-recursive dependenciesGUIDs:");
				foreach (var reference in references)
				{
					Debug.Log(reference);
				}
			}*/

			return dependenciesGUIDs.ToArray();
		}

		private static void FillDependencies(List<IDependenciesParser> dependenciesParsers, ref HashSet<string> dependenciesGUIDs, AssetInfo asset)
		{
			if (dependenciesParsers == null)
				return;
			
			foreach (var parser in dependenciesParsers)
			{
				if (parser.Type != null && parser.Type != asset.Type)
					continue;

				var foundDependencies = parser.GetDependenciesGUIDs(asset);
				if (foundDependencies == null || foundDependencies.Count == 0)
					continue;
				
				dependenciesGUIDs.UnionWith(foundDependencies);
			}
		}
	}
}