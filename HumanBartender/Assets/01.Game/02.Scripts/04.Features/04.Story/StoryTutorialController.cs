using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Clickable lessons use Unity's existing camera, shelves, crafting and serving resources.
/// 단계 진행 규칙은 이 컨트롤러가, 단계별 제목·문구는 직렬화한 문구 표(copy)가 소유한다.
/// </summary>
public sealed class StoryTutorialController : MonoBehaviour
{
    /// <summary>
    /// 튜토리얼 단계 하나의 제목과 문구. {PrevSeat} {NextSeat} {PrepPrev} {PrepNext} {ServicePanel}은
    /// 플레이어의 현재 키 설정으로 바뀐다.
    /// </summary>
    [Serializable]
    public sealed class LessonCopy
    {
        public string Key;
        public string Title;
        [TextArea(1, 4)] public string Text;

        public LessonCopy(string key, string title, string text) { Key = key; Title = title; Text = text; }
    }

    [SerializeField] StoryTutorialView viewPrefab;
    [SerializeField] PlayCamera slotCamera;
    [SerializeField] DialogueCharacterManager characters;
    [SerializeField] UIDialogueTextView dialogue;
    [SerializeField] CraftServingView serving;
    [SerializeField] CraftFlowController craft;
    [SerializeField] CraftPrepStageHost preparationHost;
    [SerializeField] RecipeBrowserScreen recipes;
    [SerializeField] CraftGimmickHud gimmickHud;

    [Tooltip("단계별 제목·문구. Key는 RequiredCopyKeys와 같아야 하며 빠진 항목은 기본 문구를 쓴다.")]
    [SerializeField] LessonCopy[] copy = DefaultCopy();

    StoryTutorialView view;
    UniTaskCompletionSource lessonComplete;
    CancellationToken lessonToken;
    ESlotType currentSeat, originSeat;
    bool visitedLeft, visitedRight, cameraMoving;
    bool craftLesson;
    bool recipeConfirmed;
    string cocktail;
    string preparationStep;
    string displayedText;
    CraftPrepStageScreen guidedStage;
    CocktailRecipeBrowser recipeBrowser;
    ServicePanelController servicePanel;

    static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };

    public string ActiveLesson { get; private set; }
    public CraftServingView Tables => serving;

    /// <summary>문구 표에 반드시 있어야 하는 키. 진행 규칙에서 쓰는 단계 이름과 같다.</summary>
    public static IReadOnlyList<string> RequiredCopyKeys => Array.ConvertAll(DefaultCopy(), entry => entry.Key);

    /// <summary>웹 본편과 같은 순서·표현의 기본 문구. 인스펙터에서 바꾼 값이 우선한다.</summary>
    public static LessonCopy[] DefaultCopy() => new[]
    {
        new LessonCopy("seatLeft", "바 둘러보기", "{PrevSeat}를 눌러 왼쪽 끝 좌석을 바라보세요."),
        new LessonCopy("seatRight", "바 둘러보기", "{NextSeat}를 눌러 오른쪽 끝 좌석을 바라보세요."),
        new LessonCopy("seatReturn", "바 둘러보기", "{PrevSeat} / {NextSeat}로 다시 크리스가 앉은 좌석을 바라보세요."),
        new LessonCopy("coaster", "손님 맞이하기", "코스터를 크리스 앞으로 드래그하세요."),
        new LessonCopy("recipeOpen", "레시피 선택", "{ServicePanel} 키 또는 서비스 패널 버튼을 눌러 칵테일 레시피를 열어 주세요."),
        new LessonCopy("recipeSelect", "레시피 선택", "진토닉 레시피를 눌러 선택하세요."),
        new LessonCopy("beerRecipeSelect", "레시피 선택", "맥주 레시피를 선택하세요"),
        new LessonCopy("recipeSelectGinFizz", "레시피 선택", "진피즈 레시피를 눌러 선택하세요."),
        new LessonCopy("readRecipe", "제조 준비", "좌측 상단의 레시피를 눌러 상세 정보를 확인하세요."),
        new LessonCopy("recipeRead", "제조 준비", "잔·재료·제조법 등 필요한 정보를 확인하세요."),
        new LessonCopy("recipeClose", "제조 준비", "확인이 끝났다면 레시피를 닫아 주세요."),
        new LessonCopy("glass", "제조 준비", "롱드링크잔을 선택하세요."),
        // 1일차 맥주 수업. 웹 본편 preparation-flow.js의 beer* 단계와 같은 순서·문구다.
        new LessonCopy("beerGlass", "제조 준비", "맥주를 담을 잔을 선택하세요"),
        new LessonCopy("beerToolNavigate", "제조 준비", "{PrepPrev} / {PrepNext} 또는 양쪽 화살표로 도구 선반으로 이동하세요."),
        new LessonCopy("beerOpener", "제조 준비", "맥주 뚜껑을 따기 위해 병따개를 담으세요."),
        new LessonCopy("beerFridgeNavigate", "제조 준비", "{PrepPrev} / {PrepNext} 또는 양쪽 화살표로 냉장고로 이동하세요."),
        new LessonCopy("beerAdd", "제조 준비", "냉장고에 있는 병맥주를 담으세요"),
        new LessonCopy("beerStart", "제조 준비", "제조 시작하기"),
        new LessonCopy("shaker", "제조 준비", "도구 선반에서 레시피에 맞는 셰이커를 선택하세요."),
        new LessonCopy("navigateGin", "제조 준비", "{PrepPrev} / {PrepNext} 또는 양쪽 화살표로 다음 선반으로 이동하세요."),
        new LessonCopy("hoverGin", "제조 준비", "재료에 마우스를 올려 이름과 설명을 확인하세요."),
        new LessonCopy("addGin", "제조 준비", "진을 클릭해 선택해 보세요."),
        new LessonCopy("removeGin", "제조 준비", "아래에 담긴 재료를 클릭하면 다시 뺄 수 있어요."),
        new LessonCopy("ginAgain", "제조 준비", "다시 진을 담아 제조를 준비하세요."),
        new LessonCopy("navigateSoda", "제조 준비", "{PrepNext} 또는 오른쪽 화살표를 눌러 냉장고로 이동하세요."),
        new LessonCopy("hoverSoda", "제조 준비", "탄산수에 마우스를 올려 확인하세요."),
        new LessonCopy("addSoda", "제조 준비", "탄산수를 클릭해 담으세요."),
        new LessonCopy("start", "제조 준비", "이제 제조 시작을 눌러 주세요."),
        new LessonCopy("startNotReady", "제조 준비", "레시피에 맞는 잔과 재료가 모두 담겼는지 확인하세요."),
        new LessonCopy("pourTarget", "칵테일 제조", "목표량부터 확인해요. 병을 누른 채 기울여 따르세요."),
        new LessonCopy("pourCurrent", "칵테일 제조", "현재량을 확인해요. 목표량에 맞았다면 따르기를 마쳐 주세요."),
        // 병따기 수업. 웹 open-view.js lesson()의 단계별 문구다. 제목은 쓰지 않는다(병따기 화면이 직접 그린다).
        new LessonCopy("openReady", "병따기", "Space를 눌러 시작하세요. 먼저 힘 게이지의 초록 구간을 맞춘 뒤, 원 타이밍을 맞춰요."),
        new LessonCopy("openPower", "병따기", "힘 게이지가 초록 구간에 닿으면 Space를 한 번 누르세요."),
        new LessonCopy("openPowerDone", "병따기", "힘 조절 성공! 이제 두 원이 겹칠 때 Space를 누르세요."),
        new LessonCopy("openRhythm", "병따기", "움직이는 원과 민트색 원이 겹칠 때 Space를 한 번 누르세요."),
        new LessonCopy("openBroken", "병따기", "병을 다시 준비하고 있어요. 잠시 뒤 힘 조절부터 다시 시작해요."),
        new LessonCopy("openPop", "병따기", "잘했어요. 다음을 눌러 제조를 이어가세요."),
        new LessonCopy("shake", "칵테일 제조", "쉐이킹 화면의 타이밍에 맞춰 마우스 버튼을 누르세요."),
        new LessonCopy("stir", "칵테일 제조", "스터 화면의 표시 순서에 맞춰 방향키를 누르세요."),
        new LessonCopy("gimmick", "칵테일 제조", "제조 화면의 안내에 따라 진행하세요."),
        new LessonCopy("serveDrink", "완성된 잔 서빙", "완성된 칵테일을 크리스 앞 코스터로 드래그하세요."),
        new LessonCopy("serveDrinkOrder", "완성된 잔 서빙", "완성된 칵테일을 주문한 손님 앞 코스터로 드래그하세요."),
        new LessonCopy("serveResult", "완성된 잔 서빙", "제조 결과를 확인하고 제공하기를 눌러 주세요."),
    };

    /// <summary>문구 표에서 빠진 필수 키. 검증과 테스트에서 쓴다.</summary>
    public static List<string> FindMissingCopy(IEnumerable<LessonCopy> table)
    {
        var present = new HashSet<string>(StringComparer.Ordinal);
        if (table != null)
            foreach (var entry in table)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Text))
                    present.Add(entry.Key);
        var missing = new List<string>();
        foreach (var key in RequiredCopyKeys) if (!present.Contains(key)) missing.Add(key);
        return missing;
    }

    (string title, string text) Copy(string key)
    {
        LessonCopy found = null;
        if (copy != null) foreach (var entry in copy) if (entry != null && entry.Key == key) { found = entry; break; }
        if (found == null || string.IsNullOrWhiteSpace(found.Text))
            foreach (var entry in DefaultCopy()) if (entry.Key == key) { found = entry; break; }
        if (found == null) throw new InvalidOperationException("튜토리얼 문구가 없습니다: " + key);
        return (found.Title, ApplyKeys(found.Text));
    }

    static string ApplyKeys(string text) => text
        .Replace("{PrevSeat}", GameKeyBindings.DisplayName(GameControl.PreviousSeat))
        .Replace("{NextSeat}", GameKeyBindings.DisplayName(GameControl.NextSeat))
        .Replace("{PrepPrev}", GameKeyBindings.DisplayName(GameControl.PrepPrevious))
        .Replace("{PrepNext}", GameKeyBindings.DisplayName(GameControl.PrepNext))
        .Replace("{ServicePanel}", GameKeyBindings.DisplayName(GameControl.ServicePanel));

    void OnValidate()
    {
        var missing = FindMissingCopy(copy);
        if (missing.Count > 0) Debug.LogWarning("[StoryTutorial] 문구 표에 없는 단계(기본 문구 사용): " + string.Join(", ", missing), this);
    }

    void EnsureView()
    {
        if (view != null) return;
        var font = dialogue != null && dialogue.lunaSpeechBubble != null
            ? dialogue.lunaSpeechBubble.textLabel.font : null;
        view = StoryTutorialView.Create(viewPrefab, font);
        view.Hide();
    }

    public void ConnectSceneResources()
    {
        slotCamera = FindInScene(slotCamera);
        characters = FindInScene(characters);
        dialogue = FindInScene(dialogue);
        serving = FindInScene(serving);
        craft = FindInScene(craft);
        preparationHost = FindInScene(preparationHost);
        recipes = FindInScene(recipes);
        gimmickHud = FindInScene(gimmickHud);
        recipeBrowser = FindInScene(recipeBrowser);
        servicePanel = FindInScene(servicePanel);
        EnsureView();
        view.ConfigureHud(FindInScene<PlayPhaseController>(null), characters, FindInScene<GuestManager>(null),
            craft, preparationHost, recipes);
    }

    T FindInScene<T>(T current) where T : Component
    {
        if (current != null) return current;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    public async UniTask RunBarLessonAsync(string kind, string actor, ESlotType seat, CancellationToken token)
    {
        EnsureView();
        dialogue.ClearText();
        ActiveLesson = kind;
        lessonToken = token;
        lessonComplete = new UniTaskCompletionSource();
        try
        {
            switch (kind)
            {
                case "seatExplore":
                    originSeat = currentSeat = seat;
                    visitedLeft = visitedRight = false;
                    ShowSeatLesson();
                    await lessonComplete.Task.AttachExternalCancellation(token);
                    break;
                case "seatIndicator":
                    view.ShowSeatIndicator(() => lessonComplete.TrySetResult());
                    await lessonComplete.Task.AttachExternalCancellation(token);
                    break;
                case "coaster":
                    var (title, text) = Copy("coaster");
                    view.ShowInstruction(title, text);
                    await serving.WaitForCoasterAsync(actor, token);
                    break;
                default: throw new NotSupportedException("지원하지 않는 튜토리얼: " + kind);
            }
        }
        finally
        {
            lessonComplete = null;
            ActiveLesson = null;
            if (view != null) { view.SeatHud.SetExploration(false, currentSeat, false); view.Hide(); }
        }
    }

    void ShowSeatLesson()
    {
        var (title, text) = Copy(!visitedLeft ? "seatLeft" : !visitedRight ? "seatRight" : "seatReturn");
        view.SeatHud.SetExploration(true, currentSeat, cameraMoving);
        ESlotType target = !visitedLeft ? ESlotType.Left : !visitedRight ? ESlotType.Right : originSeat;
        RectTransform key = Array.IndexOf(SeatOrder, target) < Array.IndexOf(SeatOrder, currentSeat)
            ? view.SeatHud.LeftKey : view.SeatHud.RightKey;
        view.ShowGuide(title, text, key, view.SeatHud.Indicators);
    }

    public bool TryMoveSeat(int step)
    {
        if (ActiveLesson != "seatExplore" || lessonComplete == null) return false;
        if (cameraMoving || step == 0) return true;
        int current = Array.IndexOf(SeatOrder, currentSeat);
        int next = Mathf.Clamp(current + Math.Sign(step), 0, SeatOrder.Length - 1);
        if (next != current)
            MoveSeatAsync(SeatOrder[next]).Forget(error => lessonComplete?.TrySetException(error));
        return true;
    }

    async UniTask MoveSeatAsync(ESlotType slot)
    {
        if (cameraMoving || lessonComplete == null) return;
        float x = SeatX(slot);
        cameraMoving = true;
        view.SeatHud.SetExploration(true, currentSeat, true);
        try
        {
            await slotCamera.MoveToXAsync(x, .4f, lessonToken);
            currentSeat = slot;
            visitedLeft |= slot == ESlotType.Left;
            visitedRight |= slot == ESlotType.Right;
        }
        finally { cameraMoving = false; }
        if (visitedLeft && visitedRight && currentSeat == originSeat) lessonComplete?.TrySetResult();
        else if (lessonComplete != null) ShowSeatLesson();
    }

    float SeatX(ESlotType slot)
    {
        if (characters.TryGetSlotX(slot, out float x)) return x;
        // Story character rigs only need left/right; the empty middle seat still needs a camera position.
        if (slot == ESlotType.Middle && characters.TryGetSlotX(ESlotType.Left, out float left) &&
            characters.TryGetSlotX(ESlotType.Right, out float right)) return (left + right) * .5f;
        throw new InvalidOperationException("좌석 카메라 위치가 없습니다: " + slot);
    }

    bool Beer => cocktail == "bottle_beer";
    bool GinFizz => cocktail == "gin_fizz";

    /// <summary>명시된 수업과 0일차의 후속 주문을 같은 튜토리얼 수명으로 안내한다.</summary>
    public static string ResolveCraftLesson(int day, string explicitCocktail, string orderedCocktail, bool skipDay0)
    {
        if (day == 0 && !skipDay0)
            return explicitCocktail ?? (orderedCocktail is "gin_tonic" or "gin_fizz" ? orderedCocktail : null);
        return day == 1 && explicitCocktail == "bottle_beer" ? explicitCocktail : null;
    }

    public void BeginCraftLesson(string id)
    {
        EnsureView();
        dialogue.ClearText();
        cocktail = id;
        craftLesson = true;
        OpenLessonCompleted = false;
        preparationStep = FirstPreparationStep;
        recipeConfirmed = false;
        displayedText = null;
        ActiveLesson = "recipeSelect";
        ShowRecipeSelection();
    }

    void ShowRecipeSelection()
    {
        Show(recipes.IsOpen ? Beer ? "beerRecipeSelect" : GinFizz ? "recipeSelectGinFizz" : "recipeSelect" : "recipeOpen");
        view.SetGuideTargets(GuideTarget());
    }

    void Show(string key, Action onConfirm = null)
    {
        var (title, text) = Copy(key);
        if (displayedText == text) return;
        displayedText = text;
        view.ShowGuide(title, text, GuideTarget(), onConfirm: onConfirm);
    }

    Transform GuideTarget() => ActiveLesson == "recipeSelect"
        ? recipes.IsOpen ? recipeBrowser?.GetTutorialTarget(cocktail) : servicePanel?.ToggleControl :
        craft.IsPreparing ? preparationHost.ActiveStage?.GetTutorialTarget(StageTargetStep(preparationStep), Beer) : null;

    // 레시피 확인·닫기 단계는 레시피 버튼 자리(열려 있으면 닫기 버튼)를 가리킨다.
    // 맥주 수업의 이동·담기 단계는 같은 자리를 가리키는 기존 단계 이름으로 바꿔 묻는다.
    static string StageTargetStep(string step) => step switch
    {
        "recipeRead" or "recipeClose" => "readRecipe",
        "beerToolNavigate" or "beerOpener" => "opener",
        "beerFridgeNavigate" => "navigateSoda",
        "beerAdd" => "addSoda",
        _ => step,
    };

    /// <summary>
    /// 준비 화면의 첫 단계. 웹 맥주 수업은 레시피 메모 확인 없이 바로 잔 고르기부터 시작한다.
    /// </summary>
    string FirstPreparationStep => Beer ? "glass" : "readRecipe";

    /// <summary>이번 수업에서 병따기를 끝까지 마쳤는지. 서빙 때 flag.day1_open_taught를 남기는 데 쓴다.</summary>
    public bool OpenLessonCompleted { get; private set; }

    CapManager lessonCap;

    void Update()
    {
        if (!craftLesson || view == null) return;
        if (!craft.IsBusy && !craft.IsCraftFlowActive && craft.PendingDrink == null)
        {
            if (guidedStage != null) { guidedStage.TutorialCanStart = null; guidedStage = null; }
            ReleaseLessonCap();
            preparationStep = FirstPreparationStep;
            recipeConfirmed = false;
            ActiveLesson = "recipeSelect";
            ShowRecipeSelection();
            return;
        }
        if (craft.IsPreparing && craft.Preparation != null)
        {
            var stage = preparationHost.ActiveStage;
            if (stage == null) return;
            if (guidedStage != stage)
            {
                guidedStage = stage;
                preparationStep = FirstPreparationStep;
                recipeConfirmed = false;
                stage.TutorialCanStart = () => preparationStep == "start" && IsReadyToStart(craft.Preparation);
            }
            UpdatePreparation(stage, craft.Preparation);
            return;
        }
        if (guidedStage != null) { guidedStage.TutorialCanStart = null; guidedStage = null; }
        if (craft.Current?.Phase == ECraftPhase.Playing)
        {
            ActiveLesson = "gimmick";
            int index = craft.Current.CurrentGimmickIndex;
            if (craft.Plan == null || index < 0 || index >= craft.Plan.Count) return;
            UpdateGimmick(craft.Plan.Steps[index]);
            return;
        }
        ReleaseLessonCap();
        ActiveLesson = "serveDrink";
        Show(craft.PendingDrink != null ? GinFizz ? "serveDrinkOrder" : "serveDrink" : "serveResult");
    }

    void UpdateGimmick(GimmickStep step)
    {
        if (step.Type is ECraftGimmick.Pour or ECraftGimmick.FillUp)
        {
            // 병을 조작하는 동안은 가리지 않고, 손을 뗐을 때만 수량 안내를 표시한다.
            if (gimmickHud != null && gimmickHud.CurrentGimmick is PourManager activePour && activePour.IsPourInputHeld)
            {
                view.Hide();
                // 같은 문구도 다시 표시할 수 있도록 숨김과 함께 캐시를 비운다.
                displayedText = null;
                return;
            }
            bool poured = gimmickHud != null && gimmickHud.CurrentGimmick is PourManager pour && pour.PouredInTargetUnit > 0f;
            Show(poured ? "pourCurrent" : "pourTarget");
            view.SetGuideTargets(poured ? gimmickHud?.NextControl ?? gimmickHud?.ProgressTarget : gimmickHud?.ProgressTarget);
            return;
        }
        if (step.Type == ECraftGimmick.Open && Beer && gimmickHud != null && gimmickHud.CurrentGimmick is CapManager cap)
        {
            UpdateOpenLesson(cap);
            return;
        }
        ReleaseLessonCap();
        Show(step.Type switch
        {
            ECraftGimmick.Shake => "shake",
            ECraftGimmick.Stir => "stir",
            _ => "gimmick",
        });
    }

    /// <summary>
    /// 1일차 병따기 수업. 웹처럼 병따기 화면이 자기 위에 가림막과 안내를 직접 그린다 — 게이지와 링은
    /// 기믹 카메라의 월드에 있어서, 바 화면용 안내 오버레이로는 뚫어 보여 줄 자리를 잡을 수 없다.
    /// 여기서는 지금 단계에 맞는 문구만 골라 넘긴다.
    /// </summary>
    void UpdateOpenLesson(CapManager cap)
    {
        if (lessonCap != cap)
        {
            ReleaseLessonCap();
            lessonCap = cap;
            lessonCap.Finished += OnOpenLessonFinished;
        }
        view.Hide();
        displayedText = null;
        ActiveLesson = "open";

        string key = cap.Opened ? "openPop" : cap.OpenPhase switch
        {
            ECapOpenPhase.Power => "openPower",
            ECapOpenPhase.PowerDone => "openPowerDone",
            ECapOpenPhase.Rhythm => "openRhythm",
            ECapOpenPhase.Broken => "openBroken",
            _ => "openReady",
        };
        cap.SetLesson(Copy(key).text);
    }

    void OnOpenLessonFinished() => OpenLessonCompleted = true;

    void ReleaseLessonCap()
    {
        if (lessonCap == null) return;
        lessonCap.Finished -= OnOpenLessonFinished;
        lessonCap.SetLesson(null);
        lessonCap = null;
    }

    void UpdatePreparation(CraftPrepStageScreen stage, CraftPreparation prep)
    {
        if (stage.IsCameraMoving) return;
        switch (preparationStep)
        {
            case "readRecipe":
                if (prep.HasReadRecipeNote) preparationStep = "glass";
                else if (stage.IsRecipeOpen) preparationStep = "recipeRead";
                break;
            case "recipeRead":
                // 확인 버튼 전에 닫아도 읽은 것으로 보고 다음 단계로 간다.
                if (prep.HasReadRecipeNote) preparationStep = "glass";
                else if (recipeConfirmed) preparationStep = "recipeClose";
                break;
            case "recipeClose": if (prep.HasReadRecipeNote) preparationStep = "glass"; break;
            case "glass": if (prep.GlassId == prep.Cocktail.Glass) preparationStep = Beer ? "beerToolNavigate" : GinFizz ? "shaker" : "navigateGin"; break;
            case "beerToolNavigate": if (stage.CurrentStage == CraftPrepStageScreen.Stage.Tool) preparationStep = "beerOpener"; break;
            case "beerOpener": if (prep.IsBottleOpenerSelected) preparationStep = "beerFridgeNavigate"; break;
            case "beerFridgeNavigate": if (stage.CurrentStage == CraftPrepStageScreen.Stage.Fridge) preparationStep = "beerAdd"; break;
            case "beerAdd": if (prep.IsSelected("beer")) preparationStep = "start"; break;
            case "shaker": if (prep.ToolId == prep.Cocktail.TargetToolId) preparationStep = "navigateGin"; break;
            case "navigateGin": if (stage.CurrentStage == CraftPrepStageScreen.Stage.Liquor) preparationStep = "hoverGin"; break;
            case "hoverGin": if (stage.HoveredItemId == "gin") preparationStep = "addGin"; break;
            case "addGin": if (prep.IsSelected("gin")) preparationStep = GinFizz ? "navigateSoda" : "removeGin"; break;
            case "removeGin": if (!prep.IsSelected("gin")) preparationStep = "ginAgain"; break;
            case "ginAgain": if (prep.IsSelected("gin")) preparationStep = "navigateSoda"; break;
            case "navigateSoda": if (stage.CurrentStage == CraftPrepStageScreen.Stage.Fridge) preparationStep = "hoverSoda"; break;
            case "hoverSoda": if (stage.HoveredItemId == "soda_water") preparationStep = "addSoda"; break;
            case "addSoda": if (prep.IsSelected("soda_water")) preparationStep = "start"; break;
        }
        ActiveLesson = preparationStep;
        string key = preparationStep switch
        {
            "glass" when Beer => "beerGlass",
            "start" => !IsReadyToStart(prep) ? "startNotReady" : Beer ? "beerStart" : "start",
            _ => preparationStep,
        };
        Show(key, preparationStep == "recipeRead" ? () => recipeConfirmed = true : null);
        view.SetGuideTargets(stage.GetTutorialTarget(StageTargetStep(preparationStep), Beer));
    }

    bool IsReadyToStart(CraftPreparation prep) => Beer
        ? prep.GlassId == prep.Cocktail.Glass && prep.IsBottleOpenerSelected && prep.IsSelected("beer")
        : prep.IsTargetSetupComplete;

    public void EndLessons()
    {
        craftLesson = false;
        ActiveLesson = null;
        ReleaseLessonCap();
        if (guidedStage != null) guidedStage.TutorialCanStart = null;
        guidedStage = null;
        if (view != null) view.Hide();
    }

    void OnDestroy()
    {
        EndLessons();
        if (view != null) Destroy(view.gameObject);
    }
}
