#region copyright
// ---------------------------------------------------------------
//  Copyright (C) Dmitry Yuhanov [https://codestage.net]
// ---------------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using UnityEngine;

	internal abstract class BaseTab
	{
		protected readonly MaintainerWindow window;

		private GUIContent caption;
		internal GUIContent Caption
		{
			get
			{
				if (caption == null)
				{
					caption = new GUIContent(CaptionName, CaptionIcon);
				}
				return caption;
			}
		}

		protected abstract string CaptionName { get; }
		protected abstract Texture CaptionIcon { get; }

		protected BaseTab(MaintainerWindow window)
		{
			this.window = window;
		}
	}
}