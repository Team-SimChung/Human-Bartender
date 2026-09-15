namespace CodeStage.Maintainer.Core
{
	using System;

	[Serializable]
	public enum AssetSettingsKind : byte
	{
		Undefined = 0,
		AudioManager = 10,
		ClusterInputManager = 20,
		DynamicsManager = 30,
		EditorBuildSettings = 40,
		EditorSettings = 50,
		GraphicsSettings = 60,
		InputManager = 70,
		NavMeshAreas = 80,
		NavMeshLayers = 90,
		NavMeshProjectSettings = 100,
		NetworkManager = 110,
		Physics2DSettings = 120,
		ProjectSettings = 130,
		PresetManager = 140,
		QualitySettings = 150,
		TagManager = 160,
		TimeManager = 170,
		UnityAdsSettings = 180,
		UnityConnectSettings = 190,
		VFXManager = 200,
		UnknownSettingAsset = 250
	}
}