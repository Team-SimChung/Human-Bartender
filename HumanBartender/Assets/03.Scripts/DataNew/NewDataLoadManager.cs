using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// VContainer의 IAsyncStartable로 StreamingAssets/json 폴더에서 신규 JSON 게임 데이터를 로드하여
/// 각 New*DataSO에 주입하는 매니저. ProjectLifetimeScope에 엔트리포인트로 등록되어
/// 컨테이너 빌드 시점에 StartAsync가 호출된다(Awake 순서에 기대지 않음).
/// 2부 바 대본(script/bar/dayN.json)은 일차별로 캐싱해두고 TryGetBarScript(int)로 꺼내 쓴다.
/// json/script/common.json은 날짜와 무관하게 항상 쓰이는 공용 상호작용 스크립트라 별도 SO에 고정 로드한다.
///
/// 구형 DataLoadManager와 그것이 읽던 파일은 모두 걷어냈다. 데이터 로더는 이것 하나다.
/// </summary>
public class NewDataLoadManager : MonoBehaviour, IAsyncStartable
{
    [Header("Bar Script (json/script/bar/dayN.json)")]
    [Tooltip("2부 바 대본(script/bar/dayN.json)이 있는 일차. 목록에 있어도 파일이 없으면 그날 2부를 건너뛴다.")]
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


    [Header("DataFile Name (json/*.json)")]
    [SerializeField] string balanceFileName = "json/balance.json";
    [SerializeField] string barkFileName = "json/barks.json";
    [SerializeField] string characterFileName = "json/characters.json";
    [SerializeField] string cocktailFileName = "json/cocktails.json";
    [SerializeField] string cutSceneFileName = "json/cutscenes.json";
    [SerializeField] string dayInfoFileName = "json/days.json";
    [SerializeField] string commonScriptFileName = "json/script/common.json";
    [SerializeField] string dossierFileName = "json/dossier.json";
    [SerializeField] string endingFileName = "json/endings.json";
    [Tooltip("표정 정본. expressions.json이 이 이름으로 넘어왔고, 거리 행인 넉 명이 여기에만 있다.")]
    [SerializeField] string expressionFileName = "json/character_anim.json";
    [SerializeField] string fieldAnimFileName = "json/field_anims.json";
    [SerializeField] string guestBodyFileName = "json/guest_bodies.json";
    [Tooltip("인터랙트 지점은 장소별로 파일이 나뉘어 있다(집·실외). 읽는 쪽이 phase로 걸러 쓰므로 한 배열로 합쳐 담는다.")]
    [SerializeField] string interactPointFile = "json/interact_points_home.json";
    [SerializeField] string homeinteractPointFile = "json/interact_points_outside.json";
    [SerializeField] string orderRuleFileName = "json/order_rules.json";
    [SerializeField] string personalityFileName = "json/personalities.json";
    [SerializeField] string questFileName = "json/quests.json";
    [SerializeField] string randomWaveFileName = "json/random_waves.json";
    [SerializeField] string regularSlotFileName = "json/regular_slots.json";
    [SerializeField] string shelfItemFileName = "json/shelf_items.json";
    [SerializeField] string spotFileName = "json/spots.json";
    [SerializeField] string tasteFileName = "json/tastes.json";
    [SerializeField] string textTagFileName = "json/text_tags.json";
    [SerializeField] string uiStringFileName = "json/ui_strings.json";
    [SerializeField] string streetFileName = "json/script/street.json";


    /// <summary>
    /// 2부 바 대본(script/bar/dayN.json). 일차별로 들고 있다가 2부가 시작할 때 그날 것을 꺼내 쓴다.
    ///
    /// day_N.json처럼 SO 한 칸에 갈아 끼우지 않는다. 그쪽은 "지금 보고 있는 하루" 하나만 있으면 되지만,
    /// 바 대본은 없는 날(Day 3)과 있는 날을 구분해야 해서 없다는 사실 자체가 값이다.
    ///
    /// static인 이유는 이 로더가 Play 씬에 없기 때문이다. VContainer 루트 스코프(ProjectLifeScope)에
    /// 얹혀 실행 중에 만들어지므로 씬의 오브젝트가 인스펙터로 꽂을 수 없고, 등록도
    /// AsImplementedInterfaces뿐이라 구체 타입으로 주입받을 수도 없다. 로딩 완료 신호와 같은 사정이다.
    /// </summary>
    static readonly Dictionary<int, NewDayScriptBase> _barScriptCache = new();

    /// <summary>
    /// 그날의 2부 바 대본을 꺼낸다. 그 일차의 파일이 아예 없으면 false —
    /// 씬이 0개인 것(Day 3)과 파일이 없는 것은 다르므로, 부르는 쪽이 구분할 수 있게 나눠 돌려준다.
    /// </summary>
    public static bool TryGetBarScript(int day, out NewDayScriptBase script)
    {
        return _barScriptCache.TryGetValue(day, out script) && script != null;
    }

    static UniTaskCompletionSource loadCompletion = new();

    /// <summary>
    /// json 로딩이 끝났는지.
    ///
    /// 로딩은 IAsyncStartable로 프레임을 넘겨 가며 진행되는데, 씬의 MonoBehaviour들은 그와 무관하게
    /// Awake·Start를 먼저 마친다. 그래서 로딩보다 먼저 도는 쪽이 기다릴 수 있어야 한다.
    ///
    /// 기다리지 않으면 SO에 구워져 있는 빈 배열을 실제 데이터로 읽는다 — 예외도 나지 않고 그냥
    /// "오늘 손님 0명"이 되어 1부를 건너뛰는 식으로 조용히 어긋난다.
    /// </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>로딩이 끝날 때까지 기다린다. 이미 끝났으면 곧바로 돌아온다.</summary>
    public static UniTask WaitUntilLoadedAsync() => loadCompletion.Task;

    /// <summary>로딩을 다시 시작할 때 완료 신호를 되돌린다(타이틀로 나갔다 다시 들어오는 경우).</summary>
    static void BeginLoad()
    {
        if (!IsLoaded) return;

        IsLoaded = false;
        loadCompletion = new UniTaskCompletionSource();
    }

    static void EndLoad()
    {
        IsLoaded = true;
        loadCompletion.TrySetResult();
    }

    /// <summary>VContainer가 컨테이너 빌드 시점에 호출하는 엔트리포인트. LoadDataAsync를 기다린 뒤 완료된다.</summary>
    
    async UniTask IAsyncStartable.StartAsync(CancellationToken cancellation)
    {
        await LoadDataAsync();
    }

    static string BarScriptFileName(int day) => $"json/script/bar/day{day}.json";

    public void LoadData()
    {
        Logger.Log("[New] Load Data");
        BeginLoad();

        _barScriptCache.Clear();
        foreach (var day in barDayNumbers)
        {
            var bar = JsonManager<NewDayScriptBase>.LoadGameData_StreamingAssets(BarScriptFileName(day));
            if (bar != null) _barScriptCache[day] = bar;
        }

        commonScriptData.dayScriptData = JsonManager<NewDayScriptBase>.LoadGameData_StreamingAssets(commonScriptFileName);

        balanceData.balanceData = JsonManager<NewBalanceDataBase>.LoadGameData_StreamingAssets(balanceFileName);
        barkData.barkData = JsonManager<NewBarkData[]>.LoadGameData_StreamingAssets(barkFileName);
        characterData.characterData = JsonManager<NewCharacterData[]>.LoadGameData_StreamingAssets(characterFileName);
        cocktailData.cocktailData = JsonManager<NewCocktailData[]>.LoadGameData_StreamingAssets(cocktailFileName);
        cutSceneData.cutSceneData = JsonManager<NewCutSceneRefData[]>.LoadGameData_StreamingAssets(cutSceneFileName);
        dayInfoData.dayInfoData = JsonManager<NewDayInfoData[]>.LoadGameData_StreamingAssets(dayInfoFileName);
        dossierData.dossierData = JsonManager<NewDossierData[]>.LoadGameData_StreamingAssets(dossierFileName);
        endingData.endingData = JsonManager<NewEndingData[]>.LoadGameData_StreamingAssets(endingFileName);
        expressionData.expressionData = JsonManager<Dictionary<string, Dictionary<string, NewExpressionEntry>>>.LoadGameData_StreamingAssets(expressionFileName);
        fieldAnimData.fieldAnimData = JsonManager<NewFieldAnimData[]>.LoadGameData_StreamingAssets(fieldAnimFileName);
        guestBodyData.guestBodyData = JsonManager<NewGuestBodyDataBase>.LoadGameData_StreamingAssets(guestBodyFileName);
        interactPointData.interactPointData = JsonManager<NewInteractPointData[]>.LoadGameData_StreamingAssets(interactPointFile);
        homeinteractPointData.interactPointData = JsonManager<NewInteractPointData[]>.LoadGameData_StreamingAssets(homeinteractPointFile);
        orderRuleData.orderRuleData = JsonManager<NewOrderRuleData[]>.LoadGameData_StreamingAssets(orderRuleFileName);
        personalityData.personalityData = JsonManager<NewPersonalityData[]>.LoadGameData_StreamingAssets(personalityFileName);
        questData.questData = JsonManager<NewQuestDataBase>.LoadGameData_StreamingAssets(questFileName);
        randomWaveData.randomWaveData = JsonManager<NewRandomWaveData[]>.LoadGameData_StreamingAssets(randomWaveFileName);
        regularSlotData.regularSlotData = JsonManager<NewRegularSlotData[]>.LoadGameData_StreamingAssets(regularSlotFileName);
        shelfItemData.shelfItemData = JsonManager<NewShelfItemData[]>.LoadGameData_StreamingAssets(shelfItemFileName);
        spotData.spotData = JsonManager<NewSpotData[]>.LoadGameData_StreamingAssets(spotFileName);
        tasteData.tasteData = JsonManager<NewTasteData[]>.LoadGameData_StreamingAssets(tasteFileName);
        textTagData.textTagData = JsonManager<Dictionary<string, NewTextTagData>>.LoadGameData_StreamingAssets(textTagFileName);
        uiStringData.uiStringData = JsonManager<Dictionary<string, LocalizedText>>.LoadGameData_StreamingAssets(uiStringFileName);
        streetData.newStreetData = JsonManager<NewStreetData>.LoadGameData_StreamingAssets(streetFileName);

        // 완료를 알리기 전에 찍는다. UniTask는 TrySetResult 시점에 기다리던 쪽을 동기로 이어서 돌리기
        // 때문에, 순서를 바꾸면 로딩이 끝났다는 줄보다 그 뒤에 벌어지는 일이 먼저 찍힌다.
        Logger.Log("[New] Load end");
        EndLoad();
    }

    public async UniTask LoadDataAsync()
    {
        Logger.Log("[New] Load Data");
        BeginLoad();

        _barScriptCache.Clear();
        foreach (var day in barDayNumbers)
        {
            NewDayScriptBase bar = await LoadOptionalAsync(BarScriptFileName(day));
            if (bar != null) _barScriptCache[day] = bar;
        }

        commonScriptData.dayScriptData = await JsonManager<NewDayScriptBase>.LoadAsync<NewDayScriptBase>(commonScriptFileName);

        balanceData.balanceData = await JsonManager<NewBalanceDataBase>.LoadAsync<NewBalanceDataBase>(balanceFileName);
        barkData.barkData = await JsonManager<NewBarkData[]>.LoadAsync<NewBarkData[]>(barkFileName);
        characterData.characterData = await JsonManager<NewCharacterData[]>.LoadAsync<NewCharacterData[]>(characterFileName);
        cocktailData.cocktailData = await JsonManager<NewCocktailData[]>.LoadAsync<NewCocktailData[]>(cocktailFileName);
        cutSceneData.cutSceneData = await JsonManager<NewCutSceneRefData[]>.LoadAsync<NewCutSceneRefData[]>(cutSceneFileName);
        dayInfoData.dayInfoData = await JsonManager<NewDayInfoData[]>.LoadAsync<NewDayInfoData[]>(dayInfoFileName);
        dossierData.dossierData = await JsonManager<NewDossierData[]>.LoadAsync<NewDossierData[]>(dossierFileName);
        endingData.endingData = await JsonManager<NewEndingData[]>.LoadAsync<NewEndingData[]>(endingFileName);
        expressionData.expressionData = await JsonManager<Dictionary<string, Dictionary<string, NewExpressionEntry>>>.LoadAsync<Dictionary<string, Dictionary<string, NewExpressionEntry>>>(expressionFileName);
        fieldAnimData.fieldAnimData = await JsonManager<NewFieldAnimData[]>.LoadAsync<NewFieldAnimData[]>(fieldAnimFileName);
        guestBodyData.guestBodyData = await JsonManager<NewGuestBodyDataBase>.LoadAsync<NewGuestBodyDataBase>(guestBodyFileName);
        interactPointData.interactPointData = await JsonManager<NewInteractPointData[]>.LoadAsync<NewInteractPointData[]>(interactPointFile);
        homeinteractPointData.interactPointData = await JsonManager<NewInteractPointData[]>.LoadAsync<NewInteractPointData[]>(homeinteractPointFile);
        orderRuleData.orderRuleData = await JsonManager<NewOrderRuleData[]>.LoadAsync<NewOrderRuleData[]>(orderRuleFileName);
        personalityData.personalityData = await JsonManager<NewPersonalityData[]>.LoadAsync<NewPersonalityData[]>(personalityFileName);
        questData.questData = await JsonManager<NewQuestDataBase>.LoadAsync<NewQuestDataBase>(questFileName);
        randomWaveData.randomWaveData = await JsonManager<NewRandomWaveData[]>.LoadAsync<NewRandomWaveData[]>(randomWaveFileName);
        regularSlotData.regularSlotData = await JsonManager<NewRegularSlotData[]>.LoadAsync<NewRegularSlotData[]>(regularSlotFileName);
        shelfItemData.shelfItemData = await JsonManager<NewShelfItemData[]>.LoadAsync<NewShelfItemData[]>(shelfItemFileName);
        spotData.spotData = await JsonManager<NewSpotData[]>.LoadAsync<NewSpotData[]>(spotFileName);
        tasteData.tasteData = await JsonManager<NewTasteData[]>.LoadAsync<NewTasteData[]>(tasteFileName);
        textTagData.textTagData = await JsonManager<Dictionary<string, NewTextTagData>>.LoadAsync<Dictionary<string, NewTextTagData>>(textTagFileName);
        uiStringData.uiStringData = await JsonManager<Dictionary<string, LocalizedText>>.LoadAsync<Dictionary<string, LocalizedText>>(uiStringFileName);
        streetData.newStreetData = await JsonManager<NewStreetData>.LoadAsync<NewStreetData>(streetFileName);

        // 완료를 알리기 전에 찍는다. UniTask는 TrySetResult 시점에 기다리던 쪽을 동기로 이어서 돌리기
        // 때문에, 순서를 바꾸면 로딩이 끝났다는 줄보다 그 뒤에 벌어지는 일이 먼저 찍힌다.
        Logger.Log("[New] Load end");
        EndLoad();
    }

    /// <summary>
    /// 대본 파일 하나를 읽되, 없으면 예외 대신 null을 돌려준다.
    ///
    /// 로딩은 파일을 순서대로 기다리며 진행하기 때문에, 중간에 하나가 예외를 던지면 그 뒤 파일이
    /// 통째로 로드되지 않는다. 대본은 일차에 따라 없을 수 있는 데이터라(빈 날, 아직 안 쓴 날)
    /// 그 하나 때문에 게임 전체 데이터가 비는 것은 맞지 않는다.
    /// </summary>
    static async UniTask<NewDayScriptBase> LoadOptionalAsync(string fileName)
    {
        try
        {
            return await JsonManager<NewDayScriptBase>.LoadAsync<NewDayScriptBase>(fileName);
        }
        catch (Exception e)
        {
            Logger.LogWarning($"[New] 대본을 읽지 못해 건너뜁니다: {fileName} / {e.Message}");
            return null;
        }
    }

}
