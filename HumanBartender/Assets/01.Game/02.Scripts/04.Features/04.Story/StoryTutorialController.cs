using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Clickable lessons use Unity's existing camera, shelves, crafting and serving resources.</summary>
public sealed class StoryTutorialController : MonoBehaviour
{
    [SerializeField] StoryTutorialView viewPrefab;
    [SerializeField] PlayCamera slotCamera;
    [SerializeField] DialogueCharacterManager characters;
    [SerializeField] UIDialogueTextView dialogue;
    [SerializeField] CraftServingView serving;
    [SerializeField] CraftFlowController craft;
    [SerializeField] CraftPrepStageHost preparationHost;
    [SerializeField] RecipeBrowserScreen recipes;

    StoryTutorialView view;
    CraftServingView.CoasterLesson coaster;
    UniTaskCompletionSource lessonComplete;
    CancellationToken lessonToken;
    ESlotType currentSeat, originSeat;
    bool visitedLeft, visitedRight, cameraMoving;
    bool craftLesson;
    string cocktail;
    string preparationStep;
    string displayedText;
    CraftPrepStageScreen guidedStage;
    bool introCompleted;
    CocktailRecipeBrowser recipeBrowser;
    ServicePanelController servicePanel;

    static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };

    public string ActiveLesson { get; private set; }

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

    public async UniTask RunIntroAsync(ICutScenePlayer cutscenes, CancellationToken token)
    {
        if (introCompleted) return;
        if (!await OptionalStoryCutscene.PlayAsync(cutscenes, "luna-dream", token))
        {
            EnsureView();
            await view.WaitForIntroClickAsync(token);
            // Consume the notice click before enabling the first dialogue's advance input.
            await UniTask.NextFrame(cancellationToken: token);
        }
        introCompleted = true;
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
                    coaster?.Dispose();
                    coaster = serving.CreateCoasterLesson(actor);
                    view.ShowGuide("손님 맞이하기", "코스터를 크리스 앞 책상 위로 드래그하세요.",
                        serving.CoasterSupply, serving.CoasterTarget, true);
                    await coaster.WaitAsync(token);
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
        string text = !visitedLeft ? "Q를 눌러 왼쪽 끝 좌석까지 둘러보세요." :
            !visitedRight ? "E를 눌러 오른쪽 끝 좌석까지 둘러보세요." : "Q / E로 크리스 앞 좌석으로 돌아오세요.";
        view.SeatHud.SetExploration(true, currentSeat, cameraMoving);
        ESlotType target = !visitedLeft ? ESlotType.Left : !visitedRight ? ESlotType.Right : originSeat;
        RectTransform key = Array.IndexOf(SeatOrder, target) < Array.IndexOf(SeatOrder, currentSeat)
            ? view.SeatHud.LeftKey : view.SeatHud.RightKey;
        view.ShowGuide("바 둘러보기", "Q: 왼쪽 이동 · E: 오른쪽 이동\n" + text, key, view.SeatHud.Indicators);
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

    public void BeginCraftLesson(string id)
    {
        EnsureView();
        EndCoasterLesson();
        dialogue.ClearText();
        cocktail = id;
        craftLesson = true;
        preparationStep = "readRecipe";
        displayedText = null;
        ActiveLesson = "recipeSelect";
        Show("레시피 선택", id == "bottle_beer" ? "맥주 레시피를 선택하고 제조 준비를 시작하세요." : "진토닉 레시피를 선택하고 제조 준비를 시작하세요.");
    }

    void Show(string title, string text)
    {
        if (displayedText == text) return;
        displayedText = text;
        view.ShowGuide(title, text, GuideTarget());
    }

    Transform GuideTarget() => ActiveLesson == "recipeSelect"
        ? recipes.IsOpen ? recipeBrowser?.GetTutorialTarget(cocktail) : servicePanel?.ToggleControl :
        craft.IsPreparing ? preparationHost.ActiveStage?.GetTutorialTarget(preparationStep, cocktail == "bottle_beer") : null;

    void Update()
    {
        if (!craftLesson || view == null) return;
        if (!craft.IsBusy && !craft.IsCraftFlowActive && craft.PendingDrink == null)
        {
            if (guidedStage != null) { guidedStage.TutorialCanStart = null; guidedStage = null; }
            preparationStep = "readRecipe";
            ActiveLesson = "recipeSelect";
            Show("레시피 선택", recipes.IsOpen
                ? cocktail == "bottle_beer" ? "맥주 레시피를 선택하고 제조 준비를 시작하세요." : "진토닉 레시피를 선택하고 제조 준비를 시작하세요."
                : "Tab 키 또는 제조 메뉴 버튼으로 레시피 목록을 다시 열어 주세요.");
            view.SetGuideTargets(GuideTarget());
            return;
        }
        if (craft.IsPreparing && craft.Preparation != null)
        {
            var stage = preparationHost.ActiveStage;
            if (stage == null) return;
            if (guidedStage != stage)
            {
                guidedStage = stage;
                preparationStep = "readRecipe";
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
            var step = craft.Plan.Steps[index];
            string text = step.Type switch
            {
                ECraftGimmick.Pour or ECraftGimmick.FillUp => "목표량을 확인하고 마우스 버튼을 눌러 따르세요.\n놓으면 멈춥니다. 화면의 완료 버튼으로 다음 단계로 진행하세요.",
                ECraftGimmick.Open => "병뚜껑 열기 화면의 안내에 맞춰 클릭하세요.",
                ECraftGimmick.Shake => "쉐이킹 화면의 타이밍에 맞춰 마우스 버튼을 누르세요.",
                ECraftGimmick.Stir => "스터 화면의 표시 순서에 맞춰 방향키를 누르세요.",
                _ => "제조 화면의 안내에 따라 진행하세요."
            };
            Show("칵테일 제조", text);
            return;
        }
        ActiveLesson = "serveDrink";
        Show("완성된 잔 서빙", craft.PendingDrink != null ?
            "완성된 잔을 크리스 앞 코스터로 드래그하세요." : "제조 결과를 확인하고 잔을 트레이로 옮기는 버튼을 누르세요.");
    }

    void UpdatePreparation(CraftPrepStageScreen stage, CraftPreparation prep)
    {
        bool beer = cocktail == "bottle_beer";
        if (stage.IsCameraMoving) return;
        switch (preparationStep)
        {
            case "readRecipe": if (prep.HasReadRecipeNote) preparationStep = "glass"; break;
            case "glass": if (prep.GlassId == prep.Cocktail.Glass) preparationStep = beer ? "opener" : "navigateGin"; break;
            case "opener": if (prep.IsBottleOpenerSelected) preparationStep = "navigateSoda"; break;
            case "navigateGin": if (stage.CurrentStage == CraftPrepStageScreen.Stage.Liquor) preparationStep = "hoverGin"; break;
            case "hoverGin": if (stage.HoveredItemId == "gin") preparationStep = "addGin"; break;
            case "addGin": if (prep.IsSelected("gin")) preparationStep = "removeGin"; break;
            case "removeGin": if (!prep.IsSelected("gin")) preparationStep = "ginAgain"; break;
            case "ginAgain": if (prep.IsSelected("gin")) preparationStep = "navigateSoda"; break;
            case "navigateSoda": if (stage.CurrentStage == CraftPrepStageScreen.Stage.Fridge) preparationStep = beer ? "addSoda" : "hoverSoda"; break;
            case "hoverSoda": if (stage.HoveredItemId == "soda_water") preparationStep = "addSoda"; break;
            case "addSoda": if (prep.IsSelected(beer ? "beer" : "soda_water")) preparationStep = "start"; break;
        }
        ActiveLesson = preparationStep;
        string text = preparationStep switch
        {
            "readRecipe" => "왼쪽 위 레시피 노트를 열어 잔·재료·목표량을 확인한 뒤 닫으세요.",
            "glass" => beer ? "맥주 레시피에 맞는 잔을 선택하세요." : "잔 선반에서 롱드링크잔을 선택하세요.",
            "opener" => "A / D 또는 화살표 버튼으로 도구 선반에 가서 병따개를 선택하세요.",
            "navigateGin" => "A / D 또는 화살표 버튼으로 술 선반까지 이동하세요.",
            "hoverGin" => "진에 마우스를 올려 이름과 설명을 확인하세요.",
            "addGin" => "진을 클릭해 아래 재료 슬롯에 담으세요.",
            "removeGin" => "아래 슬롯에 담긴 진을 클릭해 다시 빼 보세요.",
            "ginAgain" => "술 선반의 진을 다시 클릭해 담으세요.",
            "navigateSoda" => "A / D 또는 화살표 버튼으로 냉장고 선반까지 이동하세요.",
            "hoverSoda" => "탄산수에 마우스를 올려 이름과 설명을 확인하세요.",
            "addSoda" => beer ? "냉장고에서 병맥주를 클릭해 담으세요." : "탄산수를 클릭해 담으세요.",
            _ => IsReadyToStart(prep) ? "준비됐어요. 제조 시작 버튼을 누르세요." : "레시피에 맞는 잔과 재료가 모두 담겼는지 확인하세요."
        };
        Show("제조 준비", text);
        view.SetGuideTargets(stage.GetTutorialTarget(preparationStep, beer));
    }

    bool IsReadyToStart(CraftPreparation prep) => cocktail == "bottle_beer"
        ? prep.GlassId == prep.Cocktail.Glass && prep.IsBottleOpenerSelected && prep.IsSelected("beer")
        : prep.IsTargetSetupComplete;

    public void EndCoasterLesson() { coaster?.Dispose(); coaster = null; }

    public void EndLessons()
    {
        craftLesson = false;
        ActiveLesson = null;
        if (guidedStage != null) guidedStage.TutorialCanStart = null;
        guidedStage = null;
        EndCoasterLesson();
        if (view != null) view.Hide();
    }

    void OnDestroy()
    {
        EndLessons();
        if (view != null) Destroy(view.gameObject);
    }
}
