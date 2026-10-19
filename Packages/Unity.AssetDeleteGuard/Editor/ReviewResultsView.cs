using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    // A disposable projection of a report, never an independent reference database.
    internal sealed class ReviewResultsView
    {
        private AnalysisReport report;
        private ExecutionResult execution;
        private List<ReferenceEntry[]> groups = new List<ReferenceEntry[]>();
        private string filter = "";
        private int tab, selected;
        private Vector2 scroll, detailScroll;
        internal void SelectTab(int index) { tab = index; scroll = detailScroll = Vector2.zero; }

        internal void Draw(Rect area, AnalysisReport current, ExecutionResult result, ReviewTheme theme)
        {
            if (!ReferenceEquals(report, current))
            {
                report = current;
                selected = 0;
                scroll = detailScroll = Vector2.zero;
                Rebuild();
                if (report != null && report.Errors.Count > 0) tab = 3;
            }
            if (!ReferenceEquals(execution, result))
            {
                execution = result;
                if (execution != null) SelectTab(4);
                else if (tab == 4) SelectTab(0);
            }
            theme.Card(area);
            var tabs = new List<string> { UiText.Get("직접 참조", "References"), UiText.Get("간접 영향", "Indirect"),
                UiText.Get("삭제 파일", "Files"), UiText.Get("검사 범위", "Coverage") };
            if (execution != null) tabs.Add(UiText.Get("실행 결과", "Result"));
            var next = GUI.Toolbar(new Rect(area.x + 10, area.y + 10, area.width - 20, 29), tab, tabs.ToArray());
            if (next != tab) SelectTab(next);
            var content = new Rect(area.x + 12, area.y + 50, area.width - 24, area.height - 62);
            if (report == null)
            {
                GUI.Label(new Rect(content.x + 10, content.y + 28, content.width - 20, 55),
                    UiText.Get("검사를 실행하여 연결된 에셋을 확인하세요.\n검사가 끝나면 참조 목록이 표시됩니다.",
                        "Scan to inspect connected assets.\nResults appear when the analysis completes."), theme.Body);
                return;
            }
            if (tab == 3) { DrawCoverage(content, theme); return; }
            if (tab == 4) { DrawExecution(content, theme); return; }
            GUI.Label(new Rect(content.x, content.y + 2, 54, 20), UiText.Get("검색", "Search"), theme.Small);
            var nextFilter = EditorGUI.TextField(new Rect(content.x + 58, content.y, content.width - 58, 22), filter);
            if (nextFilter != filter) { filter = nextFilter; Rebuild(); selected = 0; scroll = detailScroll = Vector2.zero; }
            content.y += 32;
            content.height -= 32;
            if (tab == 0) DrawReferences(content, theme);
            else DrawPaths(content, tab == 1 ? report.IndirectReferencers : report.DiskPaths, theme);
        }

        private bool Matches(string text)
        {
            return string.IsNullOrEmpty(filter) || (text ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Rebuild()
        {
            groups = report == null ? new List<ReferenceEntry[]>() : report.References
                .Where(e => Matches(e.SourceLabel + " " + e.SourcePath + " " + e.TargetPath + " " + e.PropertyPath + " " + e.Kind))
                .GroupBy(e => e.SourceKey).Select(g => g.ToArray()).ToList();
        }

        private void DrawReferences(Rect area, ReviewTheme theme)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 20), groups.Count + UiText.Get("개 참조 출처 · 행을 선택하면 연결 상세가 표시됩니다", " sources · select a row to inspect its connections"), theme.Small);
            area.y += 24; area.height -= 24;
            if (groups.Count == 0)
            {
                GUI.Label(new Rect(area.x + 12, area.y + 24, area.width - 24, 60),
                    string.IsNullOrEmpty(filter) ? UiText.Get("검사 범위에서 직접 참조를 찾지 못했습니다.\n삭제 전 ‘검사 범위’ 탭의 한계도 확인하세요.",
                    "No direct references found in the inspected coverage.\nCheck the Coverage tab before deleting.") : UiText.Get("검색 결과가 없습니다.", "No matching references."), theme.Body);
                return;
            }
            var detailHeight = Mathf.Min(148, area.height * .43f);
            var list = new Rect(area.x, area.y, area.width, area.height - detailHeight - 10);
            const float rowHeight = 58;
            var width = Mathf.Max(100, list.width - 16);
            scroll = GUI.BeginScrollView(list, scroll, new Rect(0, 0, width, Mathf.Max(list.height - 1, groups.Count * rowHeight)));
            var first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / rowHeight));
            var last = Mathf.Min(groups.Count, first + Mathf.CeilToInt(list.height / rowHeight) + 1);
            for (var i = first; i < last; i++)
            {
                var entry = groups[i][0];
                var row = new Rect(0, i * rowHeight, width, rowHeight - 4);
                EditorGUI.DrawRect(row, i == selected ? theme.Selected : theme.Background);
                // Keep row selection outside the Locate button's hit area.
                if (GUI.Button(new Rect(row.x, row.y, row.width - 80, row.height), GUIContent.none, GUIStyle.none))
                { selected = i; detailScroll = Vector2.zero; }
                var icon = string.IsNullOrEmpty(entry.SourcePath) ? null : AssetDatabase.GetCachedIcon(entry.SourcePath);
                if (icon != null) GUI.DrawTexture(new Rect(9, row.y + 11, 25, 25), icon, ScaleMode.ScaleToFit);
                var label = string.IsNullOrEmpty(entry.SourcePath) ? entry.SourceLabel : ReviewTheme.DisplayName(entry.SourcePath);
                GUI.Label(new Rect(42, row.y + 6, width - 122, 20), new GUIContent(label, entry.SourceLabel), theme.Heading);
                GUI.Label(new Rect(42, row.y + 29, width - 50, 18), new GUIContent(entry.SourceLabel, entry.SourceLabel), theme.Small);
                if (GUI.Button(new Rect(width - 73, row.y + 7, 66, 23), UiText.Get("위치 보기", "Locate"))) ReferenceLocator.Locate(entry);
            }
            GUI.EndScrollView();
            var details = new Rect(area.x, list.yMax + 10, area.width, detailHeight);
            EditorGUI.DrawRect(new Rect(details.x, details.y, details.width, 1), theme.Line);
            var entries = groups[Mathf.Clamp(selected, 0, groups.Count - 1)];
            GUI.Label(new Rect(details.x, details.y + 8, details.width, 20),
                UiText.Get("연결 상세", "Connections") + "  ·  " + entries.Length, theme.Heading);
            var inner = new Rect(details.x, details.y + 34, details.width, details.height - 34);
            var innerWidth = Mathf.Max(100, inner.width - 16);
            detailScroll = GUI.BeginScrollView(inner, detailScroll, new Rect(0, 0, innerWidth, Mathf.Max(inner.height - 1, entries.Length * 47)));
            // Details are virtualized as well: one scene object can expose thousands of properties.
            first = Mathf.Max(0, Mathf.FloorToInt(detailScroll.y / 47));
            last = Mathf.Min(entries.Length, first + Mathf.CeilToInt(inner.height / 47) + 1);
            for (var i = first; i < last; i++)
            {
                var entry = entries[i];
                GUI.Label(new Rect(0, i * 47, innerWidth, 20), new GUIContent("→ " + entry.TargetPath, entry.TargetPath));
                GUI.Label(new Rect(0, i * 47 + 21, innerWidth, 18), new GUIContent(entry.Kind + " · " +
                    (string.IsNullOrEmpty(entry.PropertyPath) ? UiText.Get("파일 의존성", "file dependency") : entry.PropertyPath), entry.PropertyPath), theme.Small);
            }
            GUI.EndScrollView();
        }

        private void DrawPaths(Rect area, IEnumerable<string> paths, ReviewTheme theme)
        {
            var items = paths.Where(Matches).ToArray();
            GUI.Label(new Rect(area.x, area.y, area.width, 22), tab == 1 ?
                UiText.Get("직접 참조를 거쳐 영향을 받을 수 있는 에셋", "Assets connected through direct references") :
                UiText.Get("메타데이터와 숨김 파일을 포함한 실제 삭제 범위", "Actual deletion inventory, including metadata and hidden files"), theme.Small);
            area.y += 28; area.height -= 28;
            if (items.Length == 0) { GUI.Label(area, UiText.Get("표시할 항목이 없습니다.", "No items to display."), theme.Body); return; }
            var width = Mathf.Max(100, area.width - 16);
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, width, Mathf.Max(area.height - 1, items.Length * 34)));
            var first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / 34));
            var last = Mathf.Min(items.Length, first + Mathf.CeilToInt(area.height / 34) + 1);
            for (var i = first; i < last; i++)
            {
                if (i % 2 == 0) EditorGUI.DrawRect(new Rect(0, i * 34, width, 33), theme.Background);
                GUI.Label(new Rect(8, i * 34 + 6, width - 88, 22), new GUIContent(items[i], items[i]));
                if (GUI.Button(new Rect(width - 73, i * 34 + 5, 66, 23), UiText.Get("위치 보기", "Locate"))) ReferenceLocator.Locate(items[i]);
            }
            GUI.EndScrollView();
        }

        private void DrawCoverage(Rect area, ReviewTheme theme)
        {
            GUILayout.BeginArea(area);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label(UiText.Get("확인한 범위", "Inspected coverage"), theme.Heading);
            GUILayout.Label(UiText.Get("저장된 에셋 · 열린 Scene / Prefab Stage · 빌드 설정 · Preloaded Assets",
                "Saved assets · open scenes / Prefab Stage · build settings · preloaded assets"), theme.Body);
            GUILayout.Space(14);
            foreach (var error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            GUILayout.Label(UiText.Get("확인하지 못한 범위와 주의사항", "Limitations and cautions"), theme.Heading);
            foreach (var warning in report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.HelpBox(UiText.Get("Native Delete는 파일 삭제 단계에서 차단합니다. Unity의 확인창과 실패 안내가 먼저 나올 수 있습니다. 다른 도구의 선행 작업이나 Material Variant 재부모화는 되돌리지 않습니다. Material은 전용 검토 메뉴로 삭제하세요.",
                "Native Delete is blocked at the file deletion stage. Unity may show its confirmation and failure dialogs first. Prior tool actions or Material Variant reparenting are not rolled back. Use the dedicated review menu for materials."), MessageType.Info);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawExecution(Rect area, ReviewTheme theme)
        {
            GUILayout.BeginArea(area);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label(UiText.Get("삭제 실행 결과", "Deletion result"), theme.Heading);
            if (!string.IsNullOrEmpty(execution.Error)) EditorGUILayout.HelpBox(execution.Error, MessageType.Warning);
            foreach (var item in execution.Items)
                EditorGUILayout.HelpBox(item.Path + "\n" + item.Status + "\n" + item.Detail, item.Status == "Deleted" ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
