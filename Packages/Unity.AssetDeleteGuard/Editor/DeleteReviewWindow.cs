using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    public sealed class DeleteReviewWindow : EditorWindow
    {
        [SerializeField] private string[] requestedPaths = new string[0];
        [SerializeField] private bool allowDelete;
        [SerializeField] private bool fromNativeDelete;
        private DeleteReviewController controller;
        private ReviewResultsView results;
        private ReviewTheme theme;
        private bool darkTheme;
        private Vector2 rootsScroll;
        private Vector2 canvasScroll;
        private bool NeedsCanvasScroll { get { return position.width < 820 || position.height < 580; } }
        private float ViewWidth { get { return NeedsCanvasScroll ? Mathf.Max(820, position.width - 16) : position.width; } }
        private float ViewHeight { get { return NeedsCanvasScroll ? Mathf.Max(580, position.height - 16) : position.height; } }

        internal static DeleteReviewWindow Open(string[] paths, bool delete, bool native = false)
        {
            var window = CreateInstance<DeleteReviewWindow>();
            window.requestedPaths = (string[])paths.Clone();
            window.allowDelete = delete;
            window.fromNativeDelete = native;
            window.titleContent = new GUIContent("Asset Delete Guard");
            window.minSize = new Vector2(820, 580);
            window.position = new Rect(120, 100, 1020, 720);
            window.Show();
            window.EnsureController();
            window.controller.Scan();
            return window;
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (controller != null) controller.Dispose();
            controller = null;
            results = null;
            theme = null;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (controller != null) controller.Reset();
            Repaint();
        }

        private void EnsureController()
        {
            if (controller == null) controller = new DeleteReviewController(requestedPaths, allowDelete);
            if (results == null) results = new ReviewResultsView();
        }

        private void Tick()
        {
            if (controller != null && controller.Advance()) Repaint();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { Close(); return; }
            EnsureController();
            if (theme == null || darkTheme != EditorGUIUtility.isProSkin)
            {
                darkTheme = EditorGUIUtility.isProSkin;
                theme = new ReviewTheme();
            }
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), theme.Background);
            // Unity ignores minSize when docked. Keep all controls reachable in small docks.
            var smallDock = NeedsCanvasScroll;
            if (smallDock) canvasScroll = GUI.BeginScrollView(new Rect(0, 0, position.width, position.height),
                canvasScroll, new Rect(0, 0, ViewWidth, ViewHeight));
            DrawHeader();
            DrawMetrics();
            var body = new Rect(16, 158, ViewWidth - 32, ViewHeight - 272);
            DrawTargets(new Rect(body.x, body.y, 228, body.height));
            results.Draw(new Rect(body.x + 240, body.y, body.width - 240, body.height), controller.Report, controller.Execution, theme);
            DrawFooter(new Rect(0, ViewHeight - 102, ViewWidth, 102));
            if (smallDock) GUI.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUI.DrawRect(new Rect(0, 0, ViewWidth, 76), theme.Panel);
            EditorGUI.DrawRect(new Rect(0, 0, 5, 76), theme.Accent);
            GUI.Label(new Rect(20, 12, 390, 30), "Asset Delete Guard", theme.Title);
            GUI.Label(new Rect(21, 45, ViewWidth - 300, 20), fromNativeDelete ?
                UiText.Get("기본 삭제를 차단했습니다. 참조를 검토한 뒤 삭제를 결정하세요.", "Native deletion blocked. Review the references before continuing.") :
                UiText.Get("삭제 전에 연결된 에셋과 영향 범위를 확인하세요.", "Review connected assets and their impact before deleting."), theme.Small);
            if (GUI.Button(new Rect(ViewWidth - 91, 13, 74, 24), UiText.Korean ? "English" : "한국어")) UiText.Korean = !UiText.Korean;
            var enabled = GUI.Toggle(new Rect(ViewWidth - 235, 47, 218, 22), NativeDeleteSettings.Enabled,
                new GUIContent(UiText.Get("기본 Delete 연동", "Guard native Delete"), UiText.Get(
                    "이 프로젝트의 사용자 설정. 대화형 에디터의 Assets 삭제 API에도 적용됩니다. 배치 모드는 제외합니다.",
                    "User setting for this project. Also guards Assets deletion APIs in the interactive editor. Batch mode is excluded.")));
            NativeDeleteSettings.Enabled = enabled;
        }

        private void DrawMetrics()
        {
            var report = controller.Report;
            var width = (ViewWidth - 52) / 3;
            DrawMetric(new Rect(16, 88, width, 56), report == null ? "—" : report.TargetPaths.Count.ToString(),
                UiText.Get("삭제 대상 에셋", "Assets in deletion scope"), 2);
            DrawMetric(new Rect(26 + width, 88, width, 56), report == null ? "—" : report.References.Select(r => r.SourceKey).Distinct().Count().ToString(),
                UiText.Get("직접 참조 출처", "Direct reference sources"), 0);
            DrawMetric(new Rect(36 + width * 2, 88, width, 56), report == null ? "—" : report.IndirectReferencers.Count.ToString(),
                UiText.Get("간접 영향 에셋", "Indirectly connected assets"), 1);
        }

        private void DrawMetric(Rect rect, string value, string label, int tab)
        {
            theme.Card(rect);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) results.SelectTab(tab);
            GUI.Label(new Rect(rect.x + 13, rect.y + 9, 62, 36), value, theme.Number);
            GUI.Label(new Rect(rect.x + 78, rect.y + 18, rect.width - 86, 25), label, theme.Heading);
        }

        private void DrawTargets(Rect area)
        {
            theme.Card(area);
            GUI.Label(new Rect(area.x + 12, area.y + 12, area.width - 24, 22), UiText.Get("선택한 삭제 대상", "Selected targets"), theme.Heading);
            GUI.Label(new Rect(area.x + 12, area.y + 38, area.width - 24, 18), requestedPaths.Length + UiText.Get("개 루트 · 하위 파일 포함", " roots · includes children"), theme.Small);
            var list = new Rect(area.x + 8, area.y + 68, area.width - 16, area.height - 184);
            var width = list.width - 16;
            rootsScroll = GUI.BeginScrollView(list, rootsScroll, new Rect(0, 0, width, Mathf.Max(list.height - 1, requestedPaths.Length * 68)));
            var first = Mathf.Max(0, Mathf.FloorToInt(rootsScroll.y / 68));
            var last = Mathf.Min(requestedPaths.Length, first + Mathf.CeilToInt(list.height / 68) + 1);
            for (var i = first; i < last; i++)
            {
                var path = requestedPaths[i];
                var icon = AssetDatabase.GetCachedIcon(path);
                if (icon != null) GUI.DrawTexture(new Rect(4, i * 68 + 4, 22, 22), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(32, i * 68 + 3, width - 34, 23), new GUIContent(ReviewTheme.DisplayName(path), path), theme.Heading);
                GUI.Label(new Rect(4, i * 68 + 31, width - 8, 30), new GUIContent(path, path), theme.Small);
                EditorGUI.DrawRect(new Rect(4, i * 68 + 63, width - 8, 1), theme.Line);
            }
            GUI.EndScrollView();
            var note = new Rect(area.x + 12, area.yMax - 109, area.width - 24, 98);
            GUI.Label(new Rect(note.x, note.y, note.width, 22), UiText.Get("삭제 전 확인", "Before deleting"), theme.Heading);
            GUI.Label(new Rect(note.x, note.y + 28, note.width, 70), UiText.Get(
                "직접 참조가 있으면 연결이 끊어질 수 있습니다. 동적 로딩 등 검사 한계도 확인하세요.",
                "Deleting referenced assets may break connections. Review coverage limits, including dynamic loading."), theme.Body);
        }

        private void DrawFooter(Rect area)
        {
            EditorGUI.DrawRect(area, theme.Panel);
            EditorGUI.DrawRect(new Rect(area.x, area.y, area.width, 1), theme.Line);
            var report = controller.Report;
            string status;
            if (controller.IsScanning) status = UiText.Get("검사 중", "Analysing") + "  ·  " + controller.Progress;
            else if (report == null) status = UiText.Get("다시 검사하여 최신 결과를 확인하세요.", "Scan to obtain current results.");
            else if (report.Consumed) status = UiText.Get("삭제 요청 처리 완료 · 실행 결과를 확인하세요.", "Deletion request processed · check the Result tab.");
            else if (!report.CanDelete) status = UiText.Get("삭제할 수 없음 · 검사 범위 탭에서 원인을 확인하세요.", "Deletion unavailable · check the Coverage tab.");
            else if (report.Generation != ProjectChanges.Generation) status = UiText.Get("검사 후 변경 감지 · 삭제 전에 다시 검증합니다.", "Changes detected · all results will be revalidated before deletion.");
            else status = UiText.Get("검사 완료", "Analysis complete") + "  ·  " + report.ScannedAssetCount + UiText.Get("개 에셋 검사", " assets scanned");
            GUI.Label(new Rect(18, area.y + 8, area.width - 36, 20), new GUIContent(status, status), theme.Small);
            if (allowDelete)
            {
                using (new EditorGUI.DisabledScope(controller.IsScanning || report == null || !report.CanDelete || report.Consumed))
                    controller.Acknowledge(GUI.Toggle(new Rect(18, area.y + 32, area.width - 36, 20), controller.Acknowledged,
                        UiText.Get("참조 손상 가능성과 검사 범위의 한계를 확인했습니다.", "I reviewed the potential broken references and coverage limitations.")));
            }
            if (GUI.Button(new Rect(18, area.y + 64, 76, 27), UiText.Get("닫기", "Close"))) { Close(); GUIUtility.ExitGUI(); }
            if (controller.IsScanning)
            {
                if (GUI.Button(new Rect(102, area.y + 64, 96, 27), UiText.Get("검사 취소", "Cancel scan"))) controller.CancelScan();
            }
            else
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUI.Button(new Rect(102, area.y + 64, 96, 27), UiText.Get("다시 검사", "Rescan"))) controller.Scan();
            }
            using (new EditorGUI.DisabledScope(report == null || controller.IsScanning))
                if (GUI.Button(new Rect(206, area.y + 64, 112, 27), UiText.Get("보고서 저장", "Export report"))) Export();
            if (allowDelete)
            {
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, .62f, .53f);
                using (new EditorGUI.DisabledScope(!controller.CanExecute))
                    if (GUI.Button(new Rect(area.width - 217, area.y + 61, 199, 33), UiText.Get("재검사 후 삭제", "Revalidate and delete"))) ConfirmDelete();
                GUI.backgroundColor = previous;
            }
        }

        private void ConfirmDelete()
        {
            if (Application.isBatchMode || !controller.CanExecute) return;
            var message = string.Join("\n", controller.Report.Roots.Take(8).ToArray()) + "\n\n" + UiText.Get(
                "전체를 다시 검사한 뒤 삭제합니다. 결과가 바뀌면 새 목록을 검토해야 합니다.\nUnity Undo로 복원할 수 없으며 VCS 사용 시 휴지통 보관이 보장되지 않습니다.",
                "A full rescan runs before deletion. Changed results require another review.\nUnity Undo cannot restore deleted assets; VCS may bypass OS trash.");
            if (EditorUtility.DisplayDialog("Asset Delete Guard", message, UiText.Get("검사 후 삭제", "Revalidate and delete"), UiText.Get("취소", "Cancel")))
                controller.Execute(new UnityTrashBackend());
        }

        private void Export()
        {
            var path = EditorUtility.SaveFilePanel("Export Asset Delete Guard report", "", "asset-delete-guard-report", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { ReviewReportExporter.Write(controller.Report, path); }
            catch (Exception ex) { EditorUtility.DisplayDialog("Asset Delete Guard", ex.Message, "OK"); }
        }
    }
}
