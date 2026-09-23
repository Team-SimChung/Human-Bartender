using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// VContainer의 IAsyncStartable로 StreamingAssets/csv 폴더에서 표별 CSV 게임 데이터를 로드하여
/// 각 New*DataSO에 주입하는 매니저. ProjectLifetimeScope에 엔트리포인트로 등록되어
/// 컨테이너 빌드 시점에 StartAsync가 호출된다(Awake 순서에 기대지 않음).
/// 2부 바 대본(script/bar/dayN)은 일차별로 캐싱해두고 TryGetBarScript(int)로 꺼내 쓴다.
/// script/common은 날짜와 무관하게 항상 쓰이는 공용 상호작용 스크립트라 별도 SO에 고정 로드한다.
///
/// 구형 DataLoadManager와 그것이 읽던 파일은 모두 걷어냈다. 데이터 로더는 이것 하나다.
/// </summary>
public class NewDataLoadManager : MonoBehaviour, IAsyncStartable
{
    [Header("Bar Script dataset_id (script/bar/dayN)")]
    [Tooltip("2부 바 대본(script/bar/dayN)이 있는 일차. 목록에 있어도 파일이 없으면 그날 2부를 건너뛴다.")]
    [SerializeField] List<int> barDayNumbers = new() { 0, 1, 2, 3, 99 };

    [Header("Target SO")]
    [SerializeField] NewBalanceDataSO balanceData;
    [SerializeField] NewBarkDataSO barkData;
    [SerializeField] NewCharacterDataSO characterData;
    [SerializeField] NewCocktailDataSO cocktailData;
    [SerializeField] NewCutSceneDataSO cutSceneData;
    [SerializeField] NewDayInfoDataSO dayInfoData;
    [SerializeField] NewDayScriptDataSO commonScriptData;
    [SerializeField] NewDossierDataSO dossierData;
    [SerializeField] NewEndingDataSO endingData;
    [SerializeField] NewExpressionDataSO expressionData;
    [SerializeField] NewFieldAnimDataSO fieldAnimData;
    [SerializeField] NewGuestBodyDataSO guestBodyData;
    [SerializeField] NewInteractPointDataSO interactPointData;
    [SerializeField] NewInteractPointDataSO homeinteractPointData;
    [SerializeField] NewOrderRuleDataSO orderRuleData;
    [SerializeField] NewPersonalityDataSO personalityData;
    [SerializeField] NewQuestDataSO questData;
    [SerializeField] NewRandomWaveDataSO randomWaveData;
    [SerializeField] NewRegularSlotDataSO regularSlotData;
    [SerializeField] NewShelfItemDataSO shelfItemData;
    [SerializeField] NewSpotDataSO spotData;
    [SerializeField] NewTasteDataSO tasteData;
    [Tooltip("대사창의 <name>/<world>/<order> 색 치환에 쓴다. UIDialogueTextView와 GuestManager가 읽는다.")]
    [SerializeField] NewTextTagDataSO textTagData;
    [SerializeField] NewUIStringDataSO uiStringData;
    [SerializeField] NewStreetDataSO streetData;


    [Header("CSV dataset_id (system/data_sets.csv)")]
    [SerializeField] string balanceFileName = "balance";
    [SerializeField] string barkFileName = "barks";
    [SerializeField] string characterFileName = "characters";
    [SerializeField] string cocktailFileName = "cocktails";
    [SerializeField] string cutSceneFileName = "cutscenes";
    [SerializeField] string dayInfoFileName = "days";
    [SerializeField] string commonScriptFileName = "script/common";
    [SerializeField] string dossierFileName = "dossier";
    [SerializeField] string endingFileName = "endings";
    [Tooltip("표정 정본 데이터 묶음. 거리 행인 넉 명이 여기에만 있다.")]
    [SerializeField] string expressionFileName = "character_anim";
    [SerializeField] string fieldAnimFileName = "field_anims";
    [SerializeField] string guestBodyFileName = "guest_bodies";
    [Tooltip("인터랙트 지점은 장소별로 파일이 나뉘어 있다(집·실외). 읽는 쪽이 phase로 걸러 쓰므로 한 배열로 합쳐 담는다.")]
    [SerializeField] string interactPointFile = "interact_points_outside";
    [SerializeField] string homeinteractPointFile = "interact_points_home";
    [SerializeField] string orderRuleFileName = "order_rules";
    [SerializeField] string personalityFileName = "personalities";
    [SerializeField] string questFileName = "quests";
    [SerializeField] string randomWaveFileName = "random_waves";
    [SerializeField] string regularSlotFileName = "regular_slots";
    [SerializeField] string shelfItemFileName = "shelf_items";
    [SerializeField] string spotFileName = "spots";
    [SerializeField] string tasteFileName = "tastes";
    [SerializeField] string textTagFileName = "text_tags";
    [SerializeField] string uiStringFileName = "ui_strings";
    [SerializeField] string streetFileName = "script/street";


    /// <summary>
    /// 2부 바 대본(script/bar/dayN). 일차별로 들고 있다가 2부가 시작할 때 그날 것을 꺼내 쓴다.
    ///
    /// 일일 설정처럼 SO 한 칸에 갈아 끼우지 않는다. 그쪽은 "지금 보고 있는 하루" 하나만 있으면 되지만,
    /// 바 대본은 없는 날(Day 3)과 있는 날을 구분해야 해서 없다는 사실 자체가 값이다.
    ///
    /// static인 이유는 이 로더가 Play 씬에 없기 때문이다. VContainer 루트 스코프(ProjectLifeScope)에
    /// 얹혀 실행 중에 만들어지므로 씬의 오브젝트가 인스펙터로 꽂을 수 없고, 등록도
    /// AsImplementedInterfaces뿐이라 구체 타입으로 주입받을 수도 없다. 로딩 완료 신호와 같은 사정이다.
    /// </summary>
    static readonly Dictionary<int, NewDayScriptBase> _barScriptCache = new();
    static NewDayInfoData[] loadedDays;

    public static bool TryGetDayInfo(int day, out NewDayInfoData info)
    {
        if (IsLoaded && loadedDays != null)
            foreach (var candidate in loadedDays)
                if (candidate.Day == day)
                {
                    info = candidate;
                    return true;
                }
        info = default;
        return false;
    }

    /// <summary>
    /// 그날의 2부 바 대본을 꺼낸다. 그 일차의 파일이 아예 없으면 false —
    /// 씬이 0개인 것(Day 3)과 파일이 없는 것은 다르므로, 부르는 쪽이 구분할 수 있게 나눠 돌려준다.
    /// </summary>
    public static bool TryGetBarScript(int day, out NewDayScriptBase script)
    {
        script = null;
        return IsLoaded && _barScriptCache.TryGetValue(day, out script) && script != null;
    }

    static UniTaskCompletionSource loadCompletion = new();
    static bool loading;
    public static Exception LastLoadError { get; private set; }
    public static int LoadVersion { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetLoadState()
    {
        loadCompletion = new UniTaskCompletionSource();
        loading = false;
        IsLoaded = false;
        LastLoadError = null;
        LoadVersion = 0;
        _barScriptCache.Clear();
        loadedDays = null;
    }

    /// <summary>
    /// CSV 로딩이 끝났는지.
    ///
    /// 로딩은 IAsyncStartable로 프레임을 넘겨 가며 진행되는데, 씬의 MonoBehaviour들은 그와 무관하게
    /// Awake·Start를 먼저 마친다. 그래서 로딩보다 먼저 도는 쪽이 기다릴 수 있어야 한다.
    ///
    /// 기다리지 않으면 SO에 구워져 있는 빈 배열을 실제 데이터로 읽는다 — 예외도 나지 않고 그냥
    /// "오늘 손님 0명"이 되어 1부를 건너뛰는 식으로 조용히 어긋난다.
    /// </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>로딩이 끝날 때까지 기다린다. 이미 끝났으면 곧바로 돌아온다.</summary>
    public static UniTask WaitUntilLoadedAsync(CancellationToken token = default) => loadCompletion.Task.AttachExternalCancellation(token);

    /// <summary>로딩을 다시 시작할 때 완료 신호를 되돌린다(타이틀로 나갔다 다시 들어오는 경우).</summary>
    static void BeginLoad()
    {
        if (IsLoaded || LastLoadError != null) loadCompletion = new UniTaskCompletionSource();
        IsLoaded = false;
        loadedDays = null;
        LastLoadError = null;
        loading = true;
    }

    static void EndLoad()
    {
        LoadVersion++;
        IsLoaded = true;
        loadCompletion.TrySetResult();
    }

    /// <summary>VContainer가 컨테이너 빌드 시점에 호출하는 엔트리포인트. LoadDataAsync를 기다린 뒤 완료된다.</summary>
    
    async UniTask IAsyncStartable.StartAsync(CancellationToken cancellation)
    {
        await LoadDataAsync(cancellation);
    }

    static string BarScriptFileName(int day) => $"script/bar/day{day}";

    public void LoadData() => LoadDataAsync(this.GetCancellationTokenOnDestroy()).Forget();

    public async UniTask LoadDataAsync(CancellationToken cancellation = default)
    {
        if (loading) { await WaitUntilLoadedAsync(cancellation); return; }
        Logger.Log("[New] Load Data");
        BeginLoad();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation, this.GetCancellationTokenOnDestroy());
        var token = lifetime.Token;
        var pending = new List<Action>();
        var barScripts = new Dictionary<int, NewDayScriptBase>();
        try
        {
        var catalog = await CsvDataReader.LoadAsync(token);
        foreach (var day in barDayNumbers)
        {
            NewDayScriptBase bar = await LoadOptionalAsync(catalog, BarScriptFileName(day), token);
            if (bar != null) barScripts[day] = bar;
        }

        if (commonScriptData == null) throw new InvalidOperationException("Missing data target: commonScriptData");
        await StageAsync<NewDayScriptBase>(catalog, commonScriptFileName, value => commonScriptData.dayScriptData = value, pending, token);

        if (balanceData == null) throw new InvalidOperationException("Missing data target: balanceData");
        await StageAsync<NewBalanceDataBase>(catalog, balanceFileName, value => balanceData.balanceData = value, pending, token);
        if (barkData == null) throw new InvalidOperationException("Missing data target: barkData");
        await StageAsync<NewBarkData[]>(catalog, barkFileName, value => barkData.barkData = value, pending, token);
        if (characterData == null) throw new InvalidOperationException("Missing data target: characterData");
        await StageAsync<NewCharacterData[]>(catalog, characterFileName, value => characterData.characterData = value, pending, token);
        if (cocktailData == null) throw new InvalidOperationException("Missing data target: cocktailData");
        await StageAsync<NewCocktailData[]>(catalog, cocktailFileName, value => cocktailData.cocktailData = value, pending, token);
        if (cutSceneData == null) throw new InvalidOperationException("Missing data target: cutSceneData");
        await StageAsync<NewCutSceneRefData[]>(catalog, cutSceneFileName, value => cutSceneData.cutSceneData = value, pending, token);
        if (dayInfoData == null) throw new InvalidOperationException("Missing data target: dayInfoData");
        await StageAsync<NewDayInfoData[]>(catalog, dayInfoFileName, value => dayInfoData.dayInfoData = value, pending, token);
        if (dossierData == null) throw new InvalidOperationException("Missing data target: dossierData");
        await StageAsync<NewDossierData[]>(catalog, dossierFileName, value => dossierData.dossierData = value, pending, token);
        if (endingData == null) throw new InvalidOperationException("Missing data target: endingData");
        await StageAsync<NewEndingData[]>(catalog, endingFileName, value => endingData.endingData = value, pending, token);
        if (expressionData == null) throw new InvalidOperationException("Missing data target: expressionData");
        await StageAsync<Dictionary<string, Dictionary<string, NewExpressionEntry>>>(catalog, expressionFileName, value => expressionData.expressionData = value, pending, token);
        if (fieldAnimData == null) throw new InvalidOperationException("Missing data target: fieldAnimData");
        await StageAsync<NewFieldAnimData[]>(catalog, fieldAnimFileName, value => fieldAnimData.fieldAnimData = value, pending, token);
        if (guestBodyData == null) throw new InvalidOperationException("Missing data target: guestBodyData");
        await StageAsync<NewGuestBodyDataBase>(catalog, guestBodyFileName, value => guestBodyData.guestBodyData = value, pending, token);
        if (interactPointData == null) throw new InvalidOperationException("Missing data target: interactPointData");
        await StageAsync<NewInteractPointData[]>(catalog, interactPointFile, value => interactPointData.interactPointData = value, pending, token);
        if (homeinteractPointData == null) throw new InvalidOperationException("Missing data target: homeinteractPointData");
        await StageAsync<NewInteractPointData[]>(catalog, homeinteractPointFile, value => homeinteractPointData.interactPointData = value, pending, token);
        if (orderRuleData == null) throw new InvalidOperationException("Missing data target: orderRuleData");
        await StageAsync<NewOrderRuleData[]>(catalog, orderRuleFileName, value => orderRuleData.orderRuleData = value, pending, token);
        if (personalityData == null) throw new InvalidOperationException("Missing data target: personalityData");
        await StageAsync<NewPersonalityData[]>(catalog, personalityFileName, value => personalityData.personalityData = value, pending, token);
        if (questData == null) throw new InvalidOperationException("Missing data target: questData");
        await StageAsync<NewQuestDataBase>(catalog, questFileName, value => questData.questData = value, pending, token);
        if (randomWaveData == null) throw new InvalidOperationException("Missing data target: randomWaveData");
        await StageAsync<NewRandomWaveData[]>(catalog, randomWaveFileName, value => randomWaveData.randomWaveData = value, pending, token);
        if (regularSlotData == null) throw new InvalidOperationException("Missing data target: regularSlotData");
        await StageAsync<NewRegularSlotData[]>(catalog, regularSlotFileName, value => regularSlotData.regularSlotData = value, pending, token);
        if (shelfItemData == null) throw new InvalidOperationException("Missing data target: shelfItemData");
        await StageAsync<NewShelfItemData[]>(catalog, shelfItemFileName, value => shelfItemData.shelfItemData = value, pending, token);
        if (spotData == null) throw new InvalidOperationException("Missing data target: spotData");
        await StageAsync<NewSpotData[]>(catalog, spotFileName, value => spotData.spotData = value, pending, token);
        if (tasteData == null) throw new InvalidOperationException("Missing data target: tasteData");
        await StageAsync<NewTasteData[]>(catalog, tasteFileName, value => tasteData.tasteData = value, pending, token);
        if (textTagData == null) throw new InvalidOperationException("Missing data target: textTagData");
        await StageAsync<Dictionary<string, NewTextTagData>>(catalog, textTagFileName, value => textTagData.textTagData = value, pending, token);
        if (uiStringData == null) throw new InvalidOperationException("Missing data target: uiStringData");
        await StageAsync<Dictionary<string, LocalizedText>>(catalog, uiStringFileName, value => uiStringData.uiStringData = value, pending, token);
        if (streetData == null) throw new InvalidOperationException("Missing data target: streetData");
        await StageAsync<NewStreetData>(catalog, streetFileName, value => streetData.newStreetData = value, pending, token);

        // 완료를 알리기 전에 찍는다. UniTask는 TrySetResult 시점에 기다리던 쪽을 동기로 이어서 돌리기
        // 때문에, 순서를 바꾸면 로딩이 끝났다는 줄보다 그 뒤에 벌어지는 일이 먼저 찍힌다.
        token.ThrowIfCancellationRequested();
        foreach (var publish in pending) publish();
        loadedDays = dayInfoData.dayInfoData;
        _barScriptCache.Clear();
        foreach (var pair in barScripts) _barScriptCache.Add(pair.Key, pair.Value);
        // Content lookups are rebuilt before a scene can register its runtime anchors.
        streetData.InitializeDictionary();
        spotData.InitializeDictionary();
        interactPointData.InitializeDictionary();
        homeinteractPointData.InitializeDictionary();
        Logger.Log("[New] Load end");
        EndLoad();
        }
        catch (OperationCanceledException)
        {
            LastLoadError = new OperationCanceledException(token);
            loadCompletion.TrySetCanceled(token);
            throw;
        }
        catch (Exception e)
        {
            LastLoadError = e;
            loadCompletion.TrySetException(e);
            throw;
        }
        finally { loading = false; }
    }

    static UniTask StageAsync<T>(CsvContentCatalog catalog, string fileName, Action<T> publish, List<Action> pending, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        T value = catalog.Read<T>(fileName);
        if (value == null) throw new InvalidOperationException($"Required data is null: {fileName}");
        pending.Add(() => publish(value));
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 대본 파일 하나를 읽되, 없으면 예외 대신 null을 돌려준다.
    ///
    /// 로딩은 파일을 순서대로 기다리며 진행하기 때문에, 중간에 하나가 예외를 던지면 그 뒤 파일이
    /// 통째로 로드되지 않는다. 대본은 일차에 따라 없을 수 있는 데이터라(빈 날, 아직 안 쓴 날)
    /// 그 하나 때문에 게임 전체 데이터가 비는 것은 맞지 않는다.
    /// </summary>
    static UniTask<NewDayScriptBase> LoadOptionalAsync(CsvContentCatalog catalog, string sourceId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return UniTask.FromResult(catalog.Contains(sourceId) ? catalog.Read<NewDayScriptBase>(sourceId) : null);
    }

}
