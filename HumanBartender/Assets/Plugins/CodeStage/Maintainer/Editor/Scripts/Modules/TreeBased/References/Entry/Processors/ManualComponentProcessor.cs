#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.References.Entry
{
	using Tools;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	internal static class ManualComponentProcessor
	{
		public static void ProcessTilemap(Object inspectedUnityObject, Tilemap target, EntryAddSettings addSettings, ProcessObjectReferenceHandler processReferenceCallback)
		{
			var tilesCount = target.GetUsedTilesCount();
			if (tilesCount == 0) return;

			var usedTiles = new TileBase[tilesCount];
			target.GetUsedTilesNonAlloc(usedTiles);

			foreach (var usedTile in usedTiles)
			{
				processReferenceCallback(inspectedUnityObject, CSInstanceIdTools.GetId(usedTile), addSettings);

				var tile = usedTile as Tile;
				if (tile == null) continue;

				if (tile.sprite != null)
				{
					processReferenceCallback(inspectedUnityObject, CSInstanceIdTools.GetId(tile.sprite), addSettings);
				}
			}
		}
	}
}