#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using Core;
	using UnityEditor.IMGUI.Controls;

	internal class ListTreeViewItem<T> : MaintainerTreeViewItem<T> where T : TreeItem
	{
		internal ListTreeViewItem(int id, int depth, string displayName, T data) : base(id, depth, displayName, data)
		{

		}
	}

	internal class ListTreeView<T> : MaintainerTreeView<T> where T : TreeItem
	{
		public ListTreeView(
#if UNITY_6000_2_OR_NEWER
			TreeViewState<int> 
#else
			TreeViewState
#endif
			state, TreeModel<T> model) : base(state, model)
		{

		}

		public ListTreeView(
#if UNITY_6000_2_OR_NEWER
			TreeViewState<int> 
#else
			TreeViewState
#endif
			state, MultiColumnHeader multiColumnHeader, TreeModel<T> model) : base(state, multiColumnHeader, model)
		{

		}

		protected override 
#if UNITY_6000_2_OR_NEWER
			TreeViewItem<int> 
#else
			TreeViewItem
#endif
			GetNewTreeViewItemInstance(int id, int depth, string name, T data)
		{
			return new ListTreeViewItem<T>(id, depth, name, data);
		}

		protected override void SortByMultipleColumns()
		{
			return;
		}
	}
}