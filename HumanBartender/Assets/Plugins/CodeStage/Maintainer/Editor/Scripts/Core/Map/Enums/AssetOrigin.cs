namespace CodeStage.Maintainer.Core
{
	using System;

	/// <summary>
	/// Origin of the asset, representing where it resides.
	/// </summary>
	[Serializable]
	public enum AssetOrigin : byte
	{
		/// <summary>
		/// Found at the Project/Assets folder.
		/// </summary>
		AssetsFolder = 0,
		
		/// <summary>
		/// Found at the Project/ProjectSettings folder.
		/// </summary>
		Settings = 10,
		
		/// <summary>
		/// Found at the special user-specific Unity packages cache folder or at the Project/Library/PackageCache folder.
		/// </summary>
		ImmutablePackage = 20,
		
		/// <summary>
		/// Found inside the Project/Packages folder.
		/// </summary>
		EmbeddedPackage = 30,
		
		/// <summary>
		/// Rest of the assets.
		/// </summary>
		Unknown = 100
	}
}