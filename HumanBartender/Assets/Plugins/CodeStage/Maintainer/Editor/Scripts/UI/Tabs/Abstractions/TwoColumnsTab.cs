#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using UnityEngine;
	using Settings;

	internal abstract class TwoColumnsTab : BaseTab
	{
		protected Vector2 leftColumnScrollPosition;
		protected Vector2 rightColumnScrollPosition;
		private static readonly float collapsedWidth = 0f;
		private static readonly float defaultExpandedWidth = 262f;
		private static readonly float toggleButtonWidth = 10f;

		protected TwoColumnsTab(MaintainerWindow window) : base(window) {}

		public virtual void Refresh(bool newData)
		{
			leftColumnScrollPosition = Vector2.zero;
			rightColumnScrollPosition = Vector2.zero;
		}

		protected bool IsLeftPanelCollapsed
		{
			get { return UserSettings.Instance.leftPanelCollapsed; }
			set
			{
				if (UserSettings.Instance.leftPanelCollapsed != value)
				{
					UserSettings.Instance.leftPanelCollapsed = value;
					UserSettings.Save();
					if (window != null)
						window.Repaint();
				}
			}
		}

		public virtual void Draw()
		{
			using (new GUILayout.HorizontalScope())
			{
				DrawLeftColumn();

				var icon = IsLeftPanelCollapsed ? CSIcons.ArrowRight : CSIcons.ArrowLeft;
				var arrowRect = GUILayoutUtility.GetRect(toggleButtonWidth, toggleButtonWidth, GUILayout.ExpandHeight(true), GUILayout.Width(toggleButtonWidth));
				if (UIHelpers.ImageButton(arrowRect, null, "Collapse / Expand", icon, UIHelpers.fillingToolbarButton))
				{
					IsLeftPanelCollapsed = !IsLeftPanelCollapsed;
					GUIUtility.ExitGUI();
				}
				
				DrawRightColumn();
			}
		}

		private void DrawLeftColumn()
		{
			using (new GUILayout.VerticalScope(UIHelpers.panelWithBackgroundNoMargins, GUILayout.ExpandHeight(true)))
			{
				if (!IsLeftPanelCollapsed)
				{
					var scrollbarWidth = GUI.skin.verticalScrollbar.fixedWidth;
					var contentWidth = IsLeftPanelCollapsed ? collapsedWidth : defaultExpandedWidth - scrollbarWidth;
					using (var scrollScope = new GUILayout.ScrollViewScope(leftColumnScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(contentWidth)))
					{
						leftColumnScrollPosition = scrollScope.scrollPosition;
				
						using (new GUILayout.HorizontalScope())
						{
							GUILayout.Space(10);
							using (new GUILayout.VerticalScope(GUILayout.Width(contentWidth-25)))
							{
								DrawLeftColumnPanel();
							}
							GUILayout.Space(10);
						}
					}
				}
				else
				{
					DrawCollapsedLeftColumnPanel();
				}
			}
		}

		private void DrawLeftColumnPanel()
		{
			DrawLeftColumnHeader();
			DrawLeftColumnBody();
		}

		private void DrawCollapsedLeftColumnPanel()
		{
			DrawCollapsedLeftColumnHeader();
			DrawCollapsedLeftColumnBody();
		}

		protected virtual void DrawLeftColumnHeader() { }

		protected virtual void DrawCollapsedLeftColumnHeader() 
		{ 
			// Override in derived classes to show icon-only buttons when collapsed
		}

		protected virtual void DrawLeftColumnBody() { }

		protected virtual void DrawCollapsedLeftColumnBody() 
		{ 
			// Override in derived classes to show minimal UI when collapsed
		}

		protected virtual void DrawRightColumn()
		{
			using (new GUILayout.VerticalScope(UIHelpers.panelWithBackgroundNoMargins))
			{
				DrawRightColumnCustomHeader();
				DrawRightColumnBody();
			}
		}

		protected virtual void DrawRightColumnCustomHeader() { }

		protected virtual void DrawRightColumnBody()
		{
			if (!DrawRightColumnTop())
				return;

			GUILayout.Space(5);

			if (!DrawRightColumnCenter())
				return;

			DrawRightColumnBottom();
			GUILayout.Space(1);
		}

		protected virtual bool DrawRightColumnTop()
		{
			return true;
		}

		protected virtual bool DrawRightColumnCenter()
		{
			return true;
		}

		protected virtual void DrawRightColumnBottom() { }
	}
}