#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.UI
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Core;
	using Core.Scan;

	using UnityEditor;
	using UnityEngine;

	[Serializable]
	internal class RecordsTabState
	{
		public List<bool> selection = new List<bool>();
		public List<bool> compaction = new List<bool>();
		public Vector2 scrollPosition = Vector2.zero;
	}

	internal abstract class RecordsTab<T> : TwoColumnsTab where T : RecordBase
	{
		private const int RecordsPerPage = 100;

		private int recordsCurrentPage;
		private int recordsTotalPages;
		private int[] recordsToDeleteIndexes;
		private T[] records;
		private IShowableRecord gotoRecord;

		protected T[] filteredRecords;
		protected readonly GUIContent[] sortingOptions;

		/* virtual methods */

		protected RecordsTab(MaintainerWindow window) : base(window)
		{
			sortingOptions = new[] { new GUIContent("↓"), new GUIContent("↑") };
		}

		public override void Refresh(bool newData)
		{
			base.Refresh(newData);

			records = null;
			filteredRecords = null;
			recordsCurrentPage = 0;

			if (newData)
				GetState().scrollPosition = Vector2.zero;
		}

		public override void Draw()
		{
			if (records == null)
			{
				records = LoadLastRecords();
				rightColumnScrollPosition = GetState().scrollPosition;
				ApplySorting();
				ApplyState();
				recordsTotalPages = (int)Math.Ceiling((double)filteredRecords.Length / RecordsPerPage);
				PerformPostRefreshActions();
			}

			base.Draw();

			if (gotoRecord != null)
			{
				EditorApplication.delayCall += () =>
				{
					gotoRecord?.Show();
					gotoRecord = null;
				};
			}
		}

		protected virtual T[] GetRecords()
		{
			return records;
		}

		protected virtual void ClearRecords()
		{
			records = null;
			filteredRecords = null;
		}

		protected virtual void DeleteRecords(int[] indexes)
		{
			recordsToDeleteIndexes = indexes;
			EditorApplication.delayCall += DeleteRecords;
		}

		protected override bool DrawRightColumnCenter()
		{
			if (filteredRecords == null || filteredRecords.Length <= 0)
			{
				DrawEmptyPlaceholder();
				return false;
			}
			
			DrawCollectionPages();

			return true;
		}

		protected virtual void DrawPagesControls()
		{
			GUILayout.Label(recordsCurrentPage + 1 + " / " + recordsTotalPages, UIHelpers.centeredLabel);
			
			GUI.enabled = recordsCurrentPage > 0;
			if (GUILayout.Button(CSIcons.DoubleArrowLeft, EditorStyles.toolbarButton))
			{
				window.RemoveNotification();
				recordsCurrentPage = 0;
				rightColumnScrollPosition = Vector2.zero;
				GetState().scrollPosition = Vector2.zero;
			}

			if (GUILayout.Button(CSIcons.ArrowLeft, EditorStyles.toolbarButton))
			{
				window.RemoveNotification();
				recordsCurrentPage--;
				rightColumnScrollPosition = Vector2.zero;
				GetState().scrollPosition = Vector2.zero;
			}
			GUI.enabled = recordsCurrentPage < recordsTotalPages - 1;
			if (GUILayout.Button(CSIcons.ArrowRight, EditorStyles.toolbarButton))
			{
				window.RemoveNotification();
				recordsCurrentPage++;
				rightColumnScrollPosition = Vector2.zero;
				GetState().scrollPosition = Vector2.zero;
			}

			if (GUILayout.Button(CSIcons.DoubleArrowRight, EditorStyles.toolbarButton))
			{
				window.RemoveNotification();
				recordsCurrentPage = recordsTotalPages - 1;
				rightColumnScrollPosition = Vector2.zero;
				GetState().scrollPosition = Vector2.zero;
			}

			GUI.enabled = true;
		}

		protected virtual void DrawCollectionPages()
		{
			var fromItem = recordsCurrentPage * RecordsPerPage;
			var toItem = fromItem + Math.Min(RecordsPerPage, filteredRecords.Length - fromItem);

			DrawCollectionPagesToolbar();

			if (filteredRecords == null)
				return;
			
			UIHelpers.Separator();

			DrawRecords(fromItem, toItem);

			UIHelpers.Separator();
		}

		protected virtual void DrawCollectionPagesToolbar()
		{
			using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				if (IsLeftPanelCollapsed)
					DrawCollectionPagesCollapsedToolbar();
				
				if (filteredRecords.Length > 0)
					GUI.enabled = true;
				
				DrawSelectAllButton();
				DrawSelectNoneButton();
				
				DrawExpandAllButton();
				DrawCollapseAllButton();
				
				DrawCopyReportButton();
				DrawExportReportButton();
				
				DrawClearResultsButton();

				GUI.enabled = true;
				
				GUILayout.FlexibleSpace();
				
				if (recordsTotalPages > 1)
					DrawPagesControls();
				
				DrawCollectionPagesToolbarSorting();
			}
		}

		protected virtual void DrawCollectionPagesCollapsedToolbar()
		{
			
		}

		protected virtual void DrawRecords(int fromItem, int toItem)
		{
			rightColumnScrollPosition = GUILayout.BeginScrollView(rightColumnScrollPosition);

			GetState().scrollPosition = rightColumnScrollPosition;
			for (var i = fromItem; i < toItem; i++)
			{
				var record = filteredRecords[i];

				DrawRecord(record, i);

				if (Event.current != null && Event.current.type == EventType.MouseDown)
				{
					var guiRect = GUILayoutUtility.GetLastRect();
					guiRect.height += 2; // to compensate the separator's gap

					if (guiRect.Contains(Event.current.mousePosition))
					{
						Event.current.Use();

						record.compactMode = !record.compactMode;
						GetState().compaction[i] = record.compactMode;
					}
				}
			}

			GUILayout.EndScrollView();
		}

		protected virtual void ApplySorting()
		{
			filteredRecords = records.ToArray();
		}

		protected virtual void DrawRecordCheckbox(RecordBase record)
		{
			EditorGUI.BeginChangeCheck();
			record.selected = EditorGUILayout.ToggleLeft(new GUIContent(""), record.selected, GUILayout.Width(12));
			if (EditorGUI.EndChangeCheck())
			{
				var index = Array.IndexOf(filteredRecords, record);
				GetState().selection[index] = record.selected;

				OnSelectionChanged();
			}
		}

		/* empty virtual methods */

		protected virtual void PerformPostRefreshActions() { }

		protected virtual void DrawCollectionPagesToolbarSorting() { }

		protected virtual string GetReportHeader() { return null; }

		protected virtual string GetReportFooter() { return null; }

		protected virtual string GetReportFileNamePart() { return ""; }

		protected virtual void AfterClearRecords() { }

		protected virtual void OnSelectionChanged() { }
		protected virtual void DrawEmptyPlaceholderScanButton() { }

		protected virtual void DrawRecord(T record, int recordIndex) { }

		/* abstract methods */

		protected abstract T[] LoadLastRecords();
		protected abstract RecordsTabState GetState();

		protected abstract void SaveSearchResults();
		protected abstract string GetModuleName();

		/* protected methods */

		protected void DrawShowButtonIfPossible(T record)
		{
			var showableIssueRecord = record as IShowableRecord;
			if (showableIssueRecord == null) return;

			string hintText;
			switch (record.LocationGroup)
			{
				case LocationGroup.Unknown:
					hintText = "Oh, sorry, but looks like I have no clue about this record.";
					break;
				case LocationGroup.Scene:
					hintText = "Selects item in the scene. Opens scene with target item if necessary and highlights this scene in the Project Browser.";
					break;
				case LocationGroup.Asset:
					hintText = "Selects asset file in the Project Browser or opens settings asset.";
					break;
				case LocationGroup.PrefabAsset:
					hintText = "Selects Prefab file with item in the Project Browser.";
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (UIHelpers.RecordButton(record, "Show", hintText, CSIcons.Show))
			{
				gotoRecord = showableIssueRecord;
			}
		}

		protected void DrawCopyButton(T record)
		{
			if (UIHelpers.RecordButton(record, "Copy", "Copies record text to the clipboard.", CSIcons.Copy))
			{
				EditorGUIUtility.systemCopyBuffer = record.ToString(true);
				MaintainerWindow.ShowNotification("Record copied to clipboard!");
			}
		}

		protected void DrawExpandCollapseButton(RecordBase record)
		{
			var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(12));
			EditorGUI.BeginChangeCheck();
			record.compactMode = !EditorGUI.Foldout(r, !record.compactMode, GUIContent.none, UIHelpers.richFoldout);
			if (EditorGUI.EndChangeCheck())
			{
				var index = Array.IndexOf(filteredRecords, record);
				GetState().compaction[index] = record.compactMode;
			}
		}

		protected void DrawSelectAllButton()
		{
			if (UIHelpers.ImageButton("Select all items", CSIcons.SelectAll, EditorStyles.toolbarButton))
			{
				foreach (var record in filteredRecords)
				{
					record.selected = true;
				}

				OnSelectionChanged();
			}
		}

		protected void DrawSelectNoneButton()
		{
			if (UIHelpers.ImageButton("Deselect all items", CSIcons.SelectNone, EditorStyles.toolbarButton))
			{
				foreach (var record in filteredRecords)
				{
					record.selected = false;
				}

				OnSelectionChanged();
			}
		}

		protected void DrawExpandAllButton()
		{
			if (UIHelpers.ImageButton("Expand all items", CSIcons.Expand, EditorStyles.toolbarButton))
			{
				for (var i = 0; i < filteredRecords.Length; i++)
				{
					var record = filteredRecords[i];
					record.compactMode = false;
					GetState().compaction[i] = false;
				}
			}
		}

		protected void DrawCollapseAllButton()
		{
			if (UIHelpers.ImageButton("Collapse all items", CSIcons.Collapse, EditorStyles.toolbarButton))
			{
				for (var i = 0; i < filteredRecords.Length; i++)
				{
					var record = filteredRecords[i];
					record.compactMode = true;
					GetState().compaction[i] = true;
				}
			}
		}

		protected void DrawCopyReportButton()
		{
			if (UIHelpers.ImageButton("Copy report to clipboard", CSIcons.Copy, EditorStyles.toolbarButton))
			{
				EditorGUIUtility.systemCopyBuffer = ReportsBuilder.GenerateReport(GetModuleName(), filteredRecords, GetReportHeader(), GetReportFooter());
				MaintainerWindow.ShowNotification("Report copied to clipboard!");
			}
		}

		protected void DrawExportReportButton()
		{
			if (UIHelpers.ImageButton("Export report to file...", CSIcons.Export, EditorStyles.toolbarButton))
			{
				var filePath = EditorUtility.SaveFilePanel("Save " + GetModuleName() + " report", "", "Maintainer " + GetReportFileNamePart() + "Report.txt", "txt");
				if (!string.IsNullOrEmpty(filePath))
				{
					var sr = File.CreateText(filePath);
					sr.Write(ReportsBuilder.GenerateReport(GetModuleName(), filteredRecords, GetReportHeader(), GetReportFooter()));
					sr.Close();
					MaintainerWindow.ShowNotification("Report saved!");
				}
			}
		}

		protected void DrawClearResultsButton()
		{
			if (UIHelpers.ImageButton("Clear all results", CSIcons.Clear, EditorStyles.toolbarButton))
			{
				ClearRecords();
				AfterClearRecords();
			}
		}

		protected void ApplyNewIgnoreFilter(FilterItem newFilter)
		{
			var indexes = new List<int>();
			for (int i = 0; i < filteredRecords.Length; i++)
			{
				if (filteredRecords[i].MatchesFilter(newFilter))
				{
					indexes.Add(i);
				}
			}

			if (indexes.Count > 0)
			{
				DeleteRecords(indexes.ToArray());
			}
		}

		private void DrawEmptyPlaceholder()
		{
			/* logo */

			using (new GUILayout.VerticalScope())
			{
				GUILayout.FlexibleSpace();
				using (new GUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();

					var icon = CSIcons.RevealBig;
					if (icon)
					{
						icon.wrapMode = TextureWrapMode.Clamp;
						var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(icon.width),
							GUILayout.Height(icon.height));
						GUI.DrawTexture(iconRect, icon);
					}

					GUILayout.FlexibleSpace();
				}
				GUILayout.Space(25);
				GUILayout.Label("<b><size=12>Please perform a new scan</size></b>", UIHelpers.centeredLabel);
				DrawEmptyPlaceholderScanButton();
				GUILayout.FlexibleSpace();
			}
		}

		private void DeleteRecords()
		{
			for (var i = recordsToDeleteIndexes.Length - 1; i >= 0; i--)
			{
				var index = recordsToDeleteIndexes[i];
				var record = filteredRecords[index];
				ArrayUtility.RemoveAt(ref records, Array.IndexOf(records, record));

				GetState().selection.RemoveAt(index);
				GetState().compaction.RemoveAt(index);
			}

			recordsToDeleteIndexes = null;

			ApplySorting();

			if (filteredRecords.Length > 0)
			{
				recordsTotalPages = (int)Math.Ceiling((double)filteredRecords.Length / RecordsPerPage);
			}
			else
			{
				recordsTotalPages = 1;
			}

			if (recordsCurrentPage + 1 > recordsTotalPages) recordsCurrentPage = recordsTotalPages - 1;

			SaveSearchResults();
			window.Repaint();
		}

		private void ApplyState()
		{
			if (GetState().selection.Count != filteredRecords.Length)
			{
				GetState().selection = new List<bool>(filteredRecords.Length);
				GetState().compaction = new List<bool>(filteredRecords.Length);

				for (var i = 0; i < filteredRecords.Length; i++)
				{
					GetState().selection.Add(true);
					GetState().compaction.Add(true);
				}
			}

			for (var i = 0; i < filteredRecords.Length; i++)
			{
				var record = filteredRecords[i];
				record.selected = GetState().selection[i];
				record.compactMode = GetState().compaction[i];
			}
		}
	}
}