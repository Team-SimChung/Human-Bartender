using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>제조 준비나 영업 대사 없이 개별 기믹 또는 칵테일 한 잔을 반복 플레이하는 창.</summary>
internal sealed class GimmickPlayTestWindow : EditorWindow
{
    [SerializeField] private ECraftGimmick selectedType = ECraftGimmick.Pour;
    [SerializeField] private string selectedKey;
    [SerializeField] private bool wholeCocktail;
    [SerializeField] private string selectedCocktailId;
    [SerializeField] private Vector2 scroll;
    private GimmickPlayTestCatalog.Case[] cases = Array.Empty<GimmickPlayTestCatalog.Case>();
    private GimmickPlayTestCatalog.Recipe[] recipes = Array.Empty<GimmickPlayTestCatalog.Recipe>();
    private string dataError;

    [MenuItem("Tools/Human Bartender/Tests/Gimmick Play Lab")]
    private static void Open()
    {
        var window = GetWindow<GimmickPlayTestWindow>("기믹 바로 플레이");
        window.minSize = new Vector2(520, 420);
        window.Show();
    }

    private void OnEnable() => Reload();
    private void OnInspectorUpdate() => Repaint();

    private void Reload()
    {
        try
        {
            recipes = GimmickPlayTestCatalog.LoadRecipes();
            cases = GimmickPlayTestCatalog.Cases(recipes);
            dataError = recipes.Length == 0 ? "플레이할 CSV 레시피가 없습니다." : null;
        }
        catch (Exception error)
        {
            recipes = Array.Empty<GimmickPlayTestCatalog.Recipe>();
            cases = Array.Empty<GimmickPlayTestCatalog.Case>();
            dataError = error.Message;
        }
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("기믹 바로 플레이", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("실제 Play 씬의 실행기·프리팹·HUD·입력을 사용합니다. " +
            "영업과 대사 시작은 건너뛰고 선택한 기믹 또는 칵테일 한 잔을 실행합니다. " +
            "플레이어는 복사본을 사용하며 저장 슬롯은 Library/GimmickPlayTests에 분리합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("CSV 목록 새로고침")) Reload();
        if (!string.IsNullOrEmpty(dataError)) EditorGUILayout.HelpBox(dataError, MessageType.Error);

        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUI.DisabledScope(cases.Length == 0 || playing &&
                   (!GimmickPlayTestSession.IsActive || !GimmickPlayTestSession.IsReady || GimmickPlayTestSession.IsBusy)))
            if (GUILayout.Button("간단 테스트 · 4종 연속", GUILayout.Height(32)))
            {
                if (playing) GimmickPlayTestSession.PlayAsync(GimmickPlayTestCatalog.QuickTestKey).Forget(GimmickPlayTestSession.Report);
                else Try(() => GimmickPlayTestSession.Start(GimmickPlayTestCatalog.QuickTestKey));
            }
        EditorGUILayout.LabelField("병따기 → 따르기 → 스터 → 셰이킹 · 레시피와 재료 자동 선택", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();

        wholeCocktail = GUILayout.Toolbar(wholeCocktail ? 1 : 0, new[] { "기믹 하나", "칵테일 한 잔" }) == 1;
        string key;
        if (wholeCocktail) key = DrawCocktailSelection();
        else
        {
            var types = cases.Select(value => value.Step.Type).Distinct().OrderBy(value => value).ToArray();
            key = DrawSelection(types)?.Key;
        }
        using (new EditorGUI.DisabledScope(key == null || playing))
            if (GUILayout.Button(wholeCocktail ? "선택 칵테일 제조 시작" : "선택 기믹으로 테스트 시작", GUILayout.Height(30)))
                Try(() => GimmickPlayTestSession.Start(key, wholeCocktail));

        if (GimmickPlayTestSession.IsActive)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(key == null || !GimmickPlayTestSession.IsReady || GimmickPlayTestSession.IsBusy))
                    if (GUILayout.Button(wholeCocktail ? "선택 칵테일 다시 만들기" : "선택 기믹 다시 플레이"))
                        GimmickPlayTestSession.PlayAsync(key, wholeCocktail).Forget(GimmickPlayTestSession.Report);
                using (new EditorGUI.DisabledScope(!GimmickPlayTestSession.IsBusy))
                    if (GUILayout.Button("현재 플레이 중단")) GimmickPlayTestSession.CancelGimmick();
                if (GUILayout.Button("테스트 종료")) GimmickPlayTestSession.Stop();
            }
            if (GUILayout.Button("Game 창 열기")) EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        else if (playing) EditorGUILayout.HelpBox("다른 Play 세션을 종료한 뒤 이 창에서 시작하세요.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(GimmickPlayTestSession.Status, MessageType.None);
        DrawResults();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Game 창에 포커스를 두고 조작하세요. 마치기는 실제 기믹의 마치기 버튼을 사용합니다. " +
            "필업은 실제 게임과 같이 따르기 프리팹을 재사용합니다. 칵테일 한 잔은 정답 구성을 자동 준비하고 실제 제조 순서대로 진행합니다. " +
            "스크립트를 다시 컴파일하면 테스트를 종료합니다.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private GimmickPlayTestCatalog.Case DrawSelection(ECraftGimmick[] types)
    {
        if (types.Length == 0) return null;
        int typeIndex = Array.IndexOf(types, selectedType);
        typeIndex = EditorGUILayout.Popup("기믹", Mathf.Max(0, typeIndex), types.Select(GimmickPlayTestCatalog.Name).ToArray());
        selectedType = types[typeIndex];
        var matching = cases.Where(value => value.Step.Type == selectedType).ToArray();
        int index = Array.FindIndex(matching, value => value.Key == selectedKey);
        index = EditorGUILayout.Popup("레시피 / 재료", Mathf.Max(0, index), matching.Select(value => value.Label).ToArray());
        var selected = matching[index];
        selectedKey = selected.Key;
        EditorGUILayout.LabelField("잔 / 도구", selected.Cocktail.Glass + " / " + (selected.Recipe.ToolId ?? "없음"));
        if (selected.Step.HasTarget)
            EditorGUILayout.LabelField("목표량", selected.Step.TargetValue.Value.ToString("0.##") + " " + selected.Step.TargetUnit);
        return selected;
    }

    private string DrawCocktailSelection()
    {
        if (recipes.Length == 0) return null;
        int index = Array.FindIndex(recipes, value => value.Key == selectedCocktailId);
        index = EditorGUILayout.Popup("만들 칵테일", Mathf.Max(0, index), recipes.Select(value => value.Label).ToArray());
        var recipe = recipes[index];
        selectedCocktailId = recipe.Key;
        EditorGUILayout.LabelField("잔 / 도구", recipe.Cocktail.Glass + " / " + (recipe.ToolId ?? "없음"));
        EditorGUILayout.LabelField("제한 시간", recipe.Cocktail.TimeLimitSec.ToString("0.##") + "초");
        EditorGUILayout.LabelField("제조 순서", EditorStyles.miniBoldLabel);
        for (int i = 0; i < recipe.Steps.Length; i++)
        {
            var step = recipe.Steps[i];
            EditorGUILayout.LabelField($"{i + 1}. {(step.IsImmediateAddition ? "정량 추가" : GimmickPlayTestCatalog.Name(step.Type))}",
                (step.IngredientId ?? "") + (step.HasTarget ? $"  {step.TargetValue:0.##} {step.TargetUnit}" : ""));
        }
        var automatic = recipe.AutoSqueezeIds.Concat(recipe.AutoPowderIds).ToArray();
        if (automatic.Length > 0) EditorGUILayout.LabelField("자동 투입", string.Join(", ", automatic), EditorStyles.wordWrappedLabel);
        return recipe.Key;
    }

    private static void DrawResults()
    {
        if (GimmickPlayTestSession.IsQuickTest)
        {
            EditorGUILayout.LabelField("간단 테스트 결과", EditorStyles.boldLabel);
            var sessions = GimmickPlayTestSession.QuickSessions;
            EditorGUILayout.LabelField("완료", sessions.Count(value => value.Phase == ECraftPhase.Completed) +
                " / " + GimmickPlayTestCatalog.QuickTypes.Count);
            for (int i = 0; i < sessions.Count; i++)
            {
                EditorGUILayout.LabelField($"{i + 1}. {GimmickPlayTestCatalog.Name(GimmickPlayTestCatalog.QuickTypes[i])}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("자동 선택 / 상태", sessions[i].SelectedCocktailId + " / " + sessions[i].Phase);
                foreach (var result in sessions[i].Actual.GimmickResults) DrawResult(result);
            }
            return;
        }
        var session = GimmickPlayTestSession.LastSession;
        if (session == null) return;
        EditorGUILayout.LabelField(GimmickPlayTestSession.IsWholeCocktail ? "칵테일 제조 결과" : "최근 결과", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("칵테일 / 상태", session.SelectedCocktailId + " / " + session.Phase);
        EditorGUILayout.LabelField("전체 조작 시간", session.Timer.ElapsedSec.ToString("0.00") + "초");
        var judgement = GimmickPlayTestSession.LastJudgement;
        if (judgement != null)
        {
            if (judgement.CraftScore.HasValue)
                EditorGUILayout.LabelField("제조 점수 / 등급", judgement.CraftScore.Value.ToString("0.##") + " / " + judgement.CraftGrade);
            else EditorGUILayout.HelpBox("제조 판정을 확인하세요: " + judgement.Status + " / " + judgement.ErrorCode, MessageType.Warning);
        }
        foreach (var result in session.Actual.GimmickResults) DrawResult(result);
    }

    private static void DrawResult(GimmickResult result)
    {
        if (result == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("기믹 / 종료", GimmickPlayTestCatalog.Name(result.Type) + " / " + result.EndType);
        EditorGUILayout.LabelField("조작 시간", result.CompletedAtSec.ToString("0.00") + "초");
        if (result.Type == ECraftGimmick.Open)
            EditorGUILayout.LabelField("병따기", $"시도 {result.AttemptCount}회 / 실패 {result.FailureCount}회 / 완료 {result.Completed}");
        else if (result.Type == ECraftGimmick.Shake || result.Type == ECraftGimmick.Stir)
            EditorGUILayout.LabelField("스택", $"성공 {result.SuccessCount} / 실패 {result.FailureCount} / 목표 {result.TargetStackCount}");
        else
            EditorGUILayout.LabelField("실제량 / 목표량", result.ActualValue.ToString("0.##") + " / " +
                (result.TargetValue?.ToString("0.##") ?? "없음") + " " + result.TargetUnit);
    }

    private static void Try(Action action)
    {
        try { action(); }
        catch (Exception error) { GimmickPlayTestSession.Report(error); }
    }
}
