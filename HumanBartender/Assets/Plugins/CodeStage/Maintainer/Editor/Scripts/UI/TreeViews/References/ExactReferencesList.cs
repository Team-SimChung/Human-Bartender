#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

using CodeStage.EditorCommon.Tools;
using CodeStage.Maintainer.Core;
using CodeStage.Maintainer.References;
using CodeStage.Maintainer.Tools;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodeStage.Maintainer.UI
{
	internal class ExactReferencesList<T> : ListTreeView<T> where T : HierarchyReferenceItem
	{
		public ExactReferencesList(
#if UNITY_6000_2_OR_NEWER
			TreeViewState<int> 
#else
			TreeViewState
#endif
			state, TreeModel<T> model):base(state, model)
		{
		}

		protected override void PostInit()
		{
			showAlternatingRowBackgrounds = false;
			rowHeight = RowHeight - 4;
		}

		protected override 
#if UNITY_6000_2_OR_NEWER
			TreeViewItem<int> 
#else
			TreeViewItem
#endif
			GetNewTreeViewItemInstance(int id, int depth, string name, T data)
		{
			return new ExactReferencesListItem<T>(id, depth, name, data);
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			CenterRectUsingSingleLineHeight(ref args.rowRect);

			var item = (ExactReferencesListItem<T>)args.item;
			var lastRect = args.rowRect;
			lastRect.xMin += 4;

			if (item.data == null || item.data.reference == null)
			{
				GUI.Label(lastRect, item.displayName);
				return;
			}

			var entry = item.data.reference;
			Rect iconRect;
			
			if (entry.location == Location.NotFound)
			{
				iconRect = lastRect;
				iconRect.width = UIHelpers.WarningIconSize;
				iconRect.height = UIHelpers.WarningIconSize;

				GUI.DrawTexture(iconRect, CSEditorIcons.WarnSmall, ScaleMode.ScaleToFit);
				lastRect.xMin += UIHelpers.WarningIconSize + UIHelpers.EyeButtonPadding;
			}
			else if (entry.location == Location.Invisible)
			{
				iconRect = lastRect;
				iconRect.width = UIHelpers.WarningIconSize;
				iconRect.height = UIHelpers.WarningIconSize;

				GUI.DrawTexture(iconRect, CSEditorIcons.InfoSmall, ScaleMode.ScaleToFit);
				lastRect.xMin += UIHelpers.WarningIconSize + UIHelpers.EyeButtonPadding;
			}
			else
			{
				iconRect = lastRect;
				iconRect.width = UIHelpers.EyeButtonSize;
				iconRect.height = UIHelpers.EyeButtonSize;
				if (UIHelpers.IconButton(iconRect, CSIcons.Show))
				{
					ShowItem(item);
				}
				lastRect.xMin += UIHelpers.EyeButtonSize + UIHelpers.EyeButtonPadding;
			}

			var boxRect = iconRect;
			boxRect.height = lastRect.height;
			boxRect.xMin = iconRect.xMax;
			boxRect.xMax = lastRect.xMax;

			var label = entry.GetLabel();
			DefaultGUI.Label(lastRect, label, args.selected, args.focused);
		}

		protected override void ShowItem(
#if UNITY_6000_2_OR_NEWER
			TreeViewItem<int> 
#else
			TreeViewItem
#endif
			clickedItem)
		{
			var item = (ExactReferencesListItem<T>)clickedItem;

			var assetPath = item.data.AssetPath;
			var referencingEntry = item.data.Reference;

			EditorApplication.delayCall += () => CSSelectionTools.RevealAndSelectReferencingEntry(assetPath, referencingEntry);
		}
	}
}