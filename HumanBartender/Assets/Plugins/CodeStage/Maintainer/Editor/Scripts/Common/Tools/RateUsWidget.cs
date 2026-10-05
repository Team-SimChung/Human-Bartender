namespace CodeStage.EditorCommon.Tools
{
	using System;
	using UnityEditor;
	using UnityEngine;

	internal class RateUsWidget
	{
		private const int StarCount = 5;
		private const float StarSize = 14f;
		private const float StarSpacing = 2f;
		private const float RowHeight = 21f;
		private const float HoveredStarScale = 1.5f;
		private const float StarScaleSmoothing = 28f;
		private const float MaxAnimationDeltaTime = 0.066f;
		private static readonly Color HoveredStarColor = new Color(1f, 0.82f, 0.18f, 1f);

		private readonly string editorPrefsKey;
		private readonly string positiveReviewUrl;
		private readonly string feedbackUrl;
		private readonly Action repaintCallback;
		private readonly GUIContent labelContent = new GUIContent();
		private readonly float[] starScales = new float[StarCount];

		private GUIStyle labelStyle;
		private bool stylesInited;
		private int hoveredStar;
		private bool animationInited;
		private double lastAnimationTime;

		public int PositiveThreshold { get; set; }
		public string Label { get; set; }
		public bool NeedsRepaint { get; private set; }

		public bool HasRated
		{
			get { return EditorPrefs.GetBool(editorPrefsKey, false); }
		}

		public RateUsWidget(string editorPrefsKeyPrefix, string positiveReviewUrl, string feedbackUrl, Action repaintCallback = null)
		{
			editorPrefsKey = editorPrefsKeyPrefix + ".HasRated";
			this.positiveReviewUrl = positiveReviewUrl;
			this.feedbackUrl = feedbackUrl;
			this.repaintCallback = repaintCallback;

			PositiveThreshold = 4;
			Label = "Rate us:";

			for (var i = 0; i < StarCount; i++)
			{
				starScales[i] = 1f;
			}
		}

		public bool DrawToolbar()
		{
			if (HasRated)
			{
				NeedsRepaint = false;
				return false;
			}

			SetupStyles();
			var currentEvent = Event.current;
			var eventType = currentEvent.type;

			labelContent.text = Label;
			var labelWidth = labelStyle.CalcSize(labelContent).x;
			var labelRect = GUILayoutUtility.GetRect(labelWidth, RowHeight, labelStyle, GUILayout.Height(RowHeight), GUILayout.ExpandHeight(false));

			if (eventType == EventType.Repaint)
			{
				var labelHeight = labelStyle.CalcHeight(labelContent, labelRect.width);
				var centeredLabelRect = new Rect(
					labelRect.x,
					labelRect.y + Mathf.Round((labelRect.height - labelHeight) * 0.5f),
					labelRect.width,
					labelHeight
				);
				GUI.Label(centeredLabelRect, labelContent, labelStyle);
			}

			var totalWidth = StarCount * StarSize + (StarCount - 1) * StarSpacing;
			var totalRect = GUILayoutUtility.GetRect(totalWidth, RowHeight, GUIStyle.none, GUILayout.Height(RowHeight), GUILayout.ExpandHeight(false));

			var filledIcon = CSEditorIcons.FavoriteIcon;
			var emptyIcon = CSEditorIcons.Favorite;
			var starY = totalRect.y + (totalRect.height - StarSize) * 0.5f;
			var starsRect = new Rect(totalRect.x, starY, totalWidth, StarSize);
			var hoveredStarNow = eventType == EventType.Layout ? hoveredStar : 0;
			var clicked = 0;

			if (eventType != EventType.Layout && starsRect.Contains(currentEvent.mousePosition))
			{
				hoveredStarNow = GetHoveredStar(currentEvent.mousePosition.x, totalRect.x);

				if (eventType == EventType.MouseDown)
				{
					clicked = hoveredStarNow;
					currentEvent.Use();
				}
			}

			if (eventType == EventType.Repaint)
			{
				var highlightedIcon = filledIcon != null ? filledIcon : emptyIcon;
				var animationChanged = UpdateStarScales(hoveredStarNow);

				for (var i = 0; i < StarCount; i++)
				{
					var starRect = GetStarRect(totalRect.x, starY, i);
					var drawRect = GetScaledRect(starRect, starScales[i]);
					var isHovered = i < hoveredStarNow;
					var icon = isHovered ? highlightedIcon : emptyIcon;
					if (icon == null)
						continue;

					if (isHovered)
					{
						var previousColor = GUI.color;
						GUI.color = HoveredStarColor;
						GUI.DrawTexture(drawRect, icon, ScaleMode.ScaleToFit, true);
						GUI.color = previousColor;
					}
					else
					{
						GUI.DrawTexture(drawRect, icon, ScaleMode.ScaleToFit, true);
					}
				}

				if (animationChanged && repaintCallback != null)
					repaintCallback();
			}

			if (eventType != EventType.Layout)
			{
				if (hoveredStarNow != hoveredStar)
				{
					hoveredStar = hoveredStarNow;
					if (repaintCallback != null)
						repaintCallback();
				}

				EditorGUIUtility.AddCursorRect(
					starsRect,
					MouseCursor.Link
				);
			}

			if (clicked > 0)
			{
				EditorPrefs.SetBool(editorPrefsKey, true);

				if (clicked >= PositiveThreshold)
					Application.OpenURL(positiveReviewUrl);
				else
					Application.OpenURL(feedbackUrl);

				if (repaintCallback != null)
					repaintCallback();
			}

			NeedsRepaint = hoveredStar > 0 || HasActiveAnimation();

			return true;
		}

		public void ResetRating()
		{
			EditorPrefs.DeleteKey(editorPrefsKey);
		}

		private void SetupStyles()
		{
			if (stylesInited)
				return;

			labelStyle = new GUIStyle(EditorStyles.miniLabel)
			{
				alignment = TextAnchor.MiddleLeft,
				fontSize = 11,
				fontStyle = FontStyle.Bold,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 6, 0, 0)
			};

			stylesInited = true;
		}

		private static int GetHoveredStar(float mouseX, float startX)
		{
			var starIndex = Mathf.FloorToInt((mouseX - startX) / (StarSize + StarSpacing)) + 1;
			return Mathf.Clamp(starIndex, 1, StarCount);
		}

		private static Rect GetStarRect(float startX, float y, int index)
		{
			var alignedStartX = AlignToPixel(startX);
			var alignedY = AlignToPixel(y);

			return new Rect(
				alignedStartX + index * (StarSize + StarSpacing),
				alignedY,
				StarSize,
				StarSize
			);
		}

		private bool HasActiveAnimation()
		{
			for (var i = 0; i < StarCount; i++)
			{
				if (!Mathf.Approximately(starScales[i], 1f))
					return true;
			}

			return false;
		}

		private bool UpdateStarScales(int hoveredStarNow)
		{
			var currentTime = EditorApplication.timeSinceStartup;
			if (!animationInited)
			{
				lastAnimationTime = currentTime;
				animationInited = true;
			}

			var deltaTime = Mathf.Clamp((float)(currentTime - lastAnimationTime), 0f, MaxAnimationDeltaTime);
			lastAnimationTime = currentTime;

			var interpolation = 1f - Mathf.Exp(-StarScaleSmoothing * deltaTime);
			var changed = false;

			for (var i = 0; i < StarCount; i++)
			{
				var targetScale = i == hoveredStarNow - 1 ? HoveredStarScale : 1f;
				var nextScale = Mathf.Lerp(starScales[i], targetScale, interpolation);
				if (Mathf.Abs(nextScale - targetScale) < 0.001f)
				{
					nextScale = targetScale;
				}

				if (!Mathf.Approximately(nextScale, starScales[i]))
				{
					starScales[i] = nextScale;
					changed = true;
				}
			}

			return changed;
		}

		private static Rect GetScaledRect(Rect rect, float scale)
		{
			var center = rect.center;
			var scaledWidth = rect.width * scale;
			var scaledHeight = rect.height * scale;
			return new Rect(
				center.x - scaledWidth * 0.5f,
				center.y - scaledHeight * 0.5f,
				scaledWidth,
				scaledHeight
			);
		}

		private static float AlignToPixel(float value)
		{
			var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
			return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
		}
	}
}
