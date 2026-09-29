# 현재 프로젝트 폴더 트리

실제 생성된 폴더를 기준으로 기록했다. 특수 폴더와 외부 패키지는 이름을 유지한다.

## Assets/01.Game

```text
01.Game/
├─ 01.Scenes/
├─ 02.Scripts/
│  ├─ 01.Bootstrap/
│  ├─ 02.Core/
│  │  ├─ 01.Progression/
│  │  │  └─ PlayFlow/
│  │  ├─ 02.Save/
│  │  ├─ 03.SceneFlow/
│  │  ├─ 04.Input/
│  │  ├─ 05.Events/
│  │  ├─ 06.Pooling/
│  │  └─ 07.Utilities/
│  ├─ 03.Data/
│  │  ├─ 01.Loading/
│  │  └─ 02.Models/
│  ├─ 04.Features/
│  │  ├─ 01.Crafting/
│  │  │  ├─ 01.Preparation/
│  │  │  ├─ 02.Flow/
│  │  │  ├─ 03.Judgement/
│  │  │  ├─ 04.Serving/
│  │  │  └─ 05.UI/
│  │  │     └─ Money/
│  │  ├─ 02.Minigames/
│  │  │  ├─ 01.Shared/
│  │  │  ├─ 02.Shake/
│  │  │  ├─ 03.Stir/
│  │  │  ├─ 04.Pour/
│  │  │  │  └─ Sph/
│  │  │  └─ 05.Cap/
│  │  ├─ 03.Tycoon/
│  │  │  └─ UI/
│  │  ├─ 04.Story/
│  │  ├─ 05.Dialogue/
│  │  ├─ 06.Cutscenes/
│  │  │  ├─ Outside Timeline/
│  │  │  │  └─ Outside/
│  │  │  └─ UI Timeline/
│  │  ├─ 07.Outside/
│  │  │  ├─ Camera/
│  │  │  ├─ Entity/
│  │  │  │  └─ Action/
│  │  │  ├─ Environment/
│  │  │  └─ gimmic/
│  │  ├─ 08.Home/
│  │  ├─ 09.Player/
│  │  ├─ 10.Title/
│  │  ├─ 11.Ending/
│  │  └─ 12.Options/
│  └─ 05.Presentation/
│     ├─ 01.Camera/
│     ├─ 02.Audio/
│     ├─ 03.UI/
│     └─ 04.Vfx/
├─ 03.Content/
│  ├─ 01.Characters/
│  │  ├─ 01.Luna/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 02.Field/
│  │  │  └─ 02.Animations/
│  │  │     ├─ 01.Clips/
│  │  │     └─ 02.Controllers/
│  │  ├─ 02.Chris/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 01.Portrait/
│  │  │  └─ 02.Animations/
│  │  │     └─ 01.Clips/
│  │  ├─ 03.Samho/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 01.Portrait/
│  │  │  └─ 02.Animations/
│  │  │     ├─ 01.Clips/
│  │  │     └─ 02.Controllers/
│  │  ├─ 04.Bubi/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 01.Portrait/
│  │  │  └─ 02.Animations/
│  │  │     └─ 01.Clips/
│  │  ├─ 05.Port/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 01.Portrait/
│  │  │  └─ 02.Animations/
│  │  │     └─ 01.Clips/
│  │  ├─ 06.Aili/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 01.Portrait/
│  │  │  └─ 02.Animations/
│  │  │     └─ 01.Clips/
│  │  ├─ 08.Shiba/
│  │  │  ├─ 01.Sprites/
│  │  │  └─ 02.Animations/
│  │  │     ├─ 01.Clips/
│  │  │     └─ 02.Controllers/
│  │  └─ 09.Shared/
│  │     ├─ 01.Sprites/
│  │     └─ 02.Animations/
│  │        └─ 01.Clips/
│  ├─ 02.Environments/
│  │  ├─ 01.Bar/
│  │  │  └─ 01.Sprites/
│  │  │     └─ Layered/
│  │  ├─ 02.Street/
│  │  │  ├─ 01.Sprites/
│  │  │  │  ├─ Elevator/
│  │  │  │  ├─ Layers/
│  │  │  │  ├─ Lighting/
│  │  │  │  ├─ Lights/
│  │  │  │  └─ Objects/
│  │  │  ├─ 04.Prefabs/
│  │  │  └─ Phone/
│  │  │     └─ 01.Sprites/
│  │  └─ 03.Home/
│  │     ├─ 01.Sprites/
│  │     └─ Interior/
│  │        └─ 01.Sprites/
│  ├─ 03.Crafting/
│  │  ├─ 01.Preparation/
│  │  │  ├─ 04.Prefabs/
│  │  │  ├─ Background/
│  │  │  │  └─ 01.Sprites/
│  │  │  ├─ Glasses/
│  │  │  │  └─ 01.Sprites/
│  │  │  ├─ Ingredients/
│  │  │  │  └─ 01.Sprites/
│  │  │  ├─ Methods/
│  │  │  │  └─ 01.Sprites/
│  │  │  ├─ Tools/
│  │  │  │  └─ 01.Sprites/
│  │  │  └─ UI/
│  │  │     └─ 01.Sprites/
│  │  ├─ 02.Ingredients/
│  │  │  ├─ Refrigerator/
│  │  │  │  └─ 01.Sprites/
│  │  │  └─ Shelf/
│  │  │     └─ 01.Sprites/
│  │  ├─ 03.Cocktails/
│  │  │  ├─ Preview/
│  │  │  │  └─ 01.Sprites/
│  │  │  └─ Reveal/
│  │  │     └─ 01.Sprites/
│  │  ├─ 04.Minigames/
│  │  │  ├─ 01.Shared/
│  │  │  │  └─ Background/
│  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 05.Cap/
│  │  │  ├─ 02.Shake/
│  │  │  │  ├─ 01.Sprites/
│  │  │  │  ├─ 02.Animations/
│  │  │  │  ├─ 03.Materials/
│  │  │  │  ├─ 04.Prefabs/
│  │  │  │  └─ 07.Shaders/
│  │  │  ├─ 03.Stir/
│  │  │  │  ├─ 01.Sprites/
│  │  │  │  ├─ 02.Animations/
│  │  │  │  ├─ 04.Prefabs/
│  │  │  │  └─ UI/
│  │  │  ├─ 04.Pour/
│  │  │  │  ├─ 01.Sprites/
│  │  │  │  ├─ 04.Prefabs/
│  │  │  │  └─ 07.Shaders/
│  │  │  └─ 05.Cap/
│  │  │     ├─ 01.Sprites/
│  │  │     └─ 04.Prefabs/
│  │  ├─ 05.Serving/
│  │  │  ├─ 04.Prefabs/
│  │  │  ├─ Coaster/
│  │  │  │  └─ 01.Sprites/
│  │  │  ├─ Order/
│  │  │  │  └─ 01.Sprites/
│  │  │  └─ Settlement/
│  │  │     └─ 01.Sprites/
│  │  └─ 06.Recipes/
│  │     ├─ 04.Prefabs/
│  │     ├─ Details/
│  │     │  └─ 01.Sprites/
│  │     └─ Icons/
│  │        └─ 01.Sprites/
│  ├─ 04.Cutscenes/
│  │  ├─ 01.Lab/
│  │  │  ├─ 01.Sprites/
│  │  │  │  ├─ 01.sheet/
│  │  │  │  ├─ 02/
│  │  │  │  ├─ 03.hound_kill_sheet/
│  │  │  │  ├─ 04.soilder_idle/
│  │  │  │  ├─ 05.hound_attack/
│  │  │  │  ├─ 06.hound_door_idle/
│  │  │  │  ├─ 07.hound_door_punch/
│  │  │  │  ├─ 08.yuna_attack/
│  │  │  │  ├─ 09.yuna_hound_idle/
│  │  │  │  ├─ 10.luna_move/
│  │  │  │  └─ 11.luna_move_to_end/
│  │  │  ├─ 02.Animations/
│  │  │  │  ├─ 01.Clips/
│  │  │  │  └─ 02.Controllers/
│  │  │  └─ 04.Prefabs/
│  │  ├─ 03.Samho/
│  │  │  ├─ 03.Materials/
│  │  │  ├─ 05.Timelines/
│  │  │  ├─ 06.Signals/
│  │  │  └─ 07.Shaders/
│  │  └─ 04.Bar/
│  │     ├─ 05.Timelines/
│  │     └─ 06.Signals/
│  ├─ 05.UI/
│  │  ├─ 01.Shared/
│  │  │  ├─ 01.Sprites/
│  │  │  ├─ 04.Prefabs/
│  │  │  ├─ Controls/
│  │  │  │  └─ 01.Sprites/
│  │  │  ├─ Dialogue/
│  │  │  │  ├─ 01.Sprites/
│  │  │  │  └─ 04.Prefabs/
│  │  │  └─ History/
│  │  │     └─ 01.Sprites/
│  │  ├─ 02.Title/
│  │  │  └─ 01.Sprites/
│  │  └─ 03.Options/
│  │     └─ 01.Sprites/
│  ├─ 06.Vfx/
│  │  ├─ 01.Sprites/
│  │  ├─ 03.Materials/
│  │  ├─ 04.Prefabs/
│  │  │  └─ Variants/
│  │  └─ 07.Shaders/
│  ├─ 07.Audio/
│  │  ├─ 01.Bgm/
│  │  ├─ 02.Sfx/
│  │  └─ 03.Mixers/
│  ├─ 08.Fonts/
│  └─ 09.Shared/
│     ├─ 01.Sprites/
│     ├─ 02.Animations/
│     │  ├─ 01.Clips/
│     │  │  └─ Placeholders/
│     │  ├─ 02.Controllers/
│     │  │  └─ Controller/
│     │  └─ Placeholders/
│     ├─ 03.Materials/
│     ├─ 04.Prefabs/
│     │  └─ Bootstrap/
│     └─ 07.Shaders/
├─ 04.Data/
│  ├─ 01.Definitions/
│  │  ├─ Crafting/
│  │  ├─ Minigames/
│  │  │  └─ Pour/
│  │  └─ UI/
│  ├─ 02.RuntimeCaches/
│  │  └─ Player/
│  └─ 03.Events/
│     ├─ Craft/
│     │  └─ UI/
│     │     └─ Money/
│     ├─ Cutscene/
│     │  └─ UI Timeline/
│     │     └─ Playable/
│     ├─ Event SO/
│     │  └─ Typing/
│     ├─ MiniGame/
│     │  ├─ Shaker/
│     │  └─ Stur/
│     ├─ Outside/
│     │  ├─ Camera/
│     │  ├─ Entity/
│     │  └─ SO/
│     │     └─ Track/
│     └─ Player/
├─ 05.Settings/
│  ├─ 01.Input/
│  └─ 02.Rendering/
│     └─ Scenes/
├─ 06.Tests/
│  └─ Editor/
└─ Editor/
   ├─ 01.ContentTools/
   ├─ 02.SceneSetup/
   ├─ 03.AnimationTools/
   └─ 04.Inspectors/
```

## Assets/02.ArtStaging

```text
02.ArtStaging/
└─ 01.GoogleDrive/
   ├─ 01.Characters/
   │  ├─ 01.Luna/
   │  │  └─ 01.Sprites/
   │  │     └─ 02.Field/
   │  ├─ 02.Chris/
   │  │  └─ 01.Sprites/
   │  │     └─ 01.Portrait/
   │  ├─ 03.Samho/
   │  │  └─ 01.Sprites/
   │  │     ├─ 01.Portrait/
   │  │     └─ 02.Field/
   │  ├─ 04.Bubi/
   │  │  └─ 01.Sprites/
   │  │     ├─ 01.Portrait/
   │  │     └─ 02.Field/
   │  ├─ 05.Port/
   │  │  └─ 01.Sprites/
   │  │     └─ 01.Portrait/
   │  ├─ 06.Aili/
   │  │  └─ 01.Sprites/
   │  │     └─ 01.Portrait/
   │  └─ 07.Guests/
   │     └─ 01.Sprites/
   │        └─ 01.Portrait/
   ├─ 02.Environments/
   │  ├─ 01.Bar/
   │  │  └─ 01.Sprites/
   │  │     ├─ Customer_Positions/
   │  │     └─ Legacy/
   │  ├─ 02.Street/
   │  │  └─ 01.Sprites/
   │  │     ├─ Final/
   │  │     └─ Lighting_Test/
   │  └─ 03.Home/
   │     └─ Interior/
   │        └─ 01.Sprites/
   ├─ 03.Crafting/
   │  ├─ 01.Preparation/
   │  │  ├─ Background/
   │  │  │  └─ 01.Sprites/
   │  │  ├─ Tools/
   │  │  │  └─ 01.Sprites/
   │  │  └─ UI/
   │  │     └─ 01.Sprites/
   │  ├─ 02.Ingredients/
   │  │  ├─ 01.Sprites/
   │  │  │  └─ Placeholders/
   │  │  └─ Refrigerator/
   │  │     └─ 01.Sprites/
   │  ├─ 03.Cocktails/
   │  │  ├─ Counter/
   │  │  │  └─ 01.Sprites/
   │  │  └─ Reveal/
   │  │     └─ 01.Sprites/
   │  ├─ 04.Minigames/
   │  │  ├─ 01.Shared/
   │  │  │  ├─ 01.Sprites/
   │  │  │  └─ Background/
   │  │  ├─ 01.Sprites/
   │  │  │  └─ 02.Shake/
   │  │  ├─ 02.Shake/
   │  │  │  └─ 01.Sprites/
   │  │  └─ 03.Stir/
   │  │     ├─ Character/
   │  │     └─ UI/
   │  ├─ 05.Serving/
   │  │  └─ 01.Sprites/
   │  │     └─ Drink_Serving_Screen_Sheets/
   │  └─ 90.PreviousVersions/
   │     └─ 01.Sprites/
   │        └─ Placeholders_Unused/
   ├─ 04.Cutscenes/
   │  ├─ 01.Lab/
   │  │  └─ 01.Sprites/
   │  │     └─ Legacy/
   │  ├─ 02.Bubi/
   │  │  └─ 01.Sprites/
   │  │     └─ Cat_and_Milk/
   │  └─ Characters/
   │     ├─ Street_NPCs/
   │     └─ scene/
   │        └─ s1_Meeting_Bubi/
   └─ 05.UI/
      ├─ 01.Shared/
      │  └─ Dialogue/
      │     └─ 01.Sprites/
      ├─ 02.Title/
      │  ├─ 01.Sprites/
      │  └─ Logo/
      │     └─ 01.Sprites/
      └─ 90.PreviousVersions/
         └─ 01.Sprites/
            ├─ Crafting_Confirmation_Dialog/
            ├─ Drink_Icons/
            ├─ Drink_Making_Button/
            ├─ History/
            ├─ Ingredient_Panel/
            ├─ Ingredients/
            ├─ Menu/
            ├─ Recipes/
            ├─ Settlement_Details/
            ├─ Shaking/
            ├─ Speech_Bubbles/
            ├─ Stirring/
            ├─ order/
            └─ recipe/
```

## Assets/03.Dev

```text
03.Dev/
├─ 01.Scenes/
│  ├─ 01.Minigames/
│  ├─ 02.Lighting/
│  │  └─ LightTest/
│  ├─ 03.Vfx/
│  ├─ 04.Tools/
│  └─ 05.Recovery/
├─ 02.Content/
│  ├─ 01.Minigames/
│  │  └─ Stur/
│  │     ├─ 01.Sprites/
│  │     ├─ 02.Animations/
│  │     │  └─ 02.Controllers/
│  │     └─ 04.Prefabs/
│  ├─ 02.Lighting/
│  │  ├─ AreaA/
│  │  └─ AreaB/
│  ├─ 03.ArtTools/
│  └─ 04.CutsceneReview/
└─ 03.Scripts/
   ├─ 01.Minigames/
   │  └─ Stur/
   ├─ 02.Camera/
   └─ 03.ArtTools/
```

## 01.SourceAssets

```text
01.SourceAssets/
└─ 02.Working/
   ├─ 01.Characters/
   │  └─ 07.Guests/
   │     └─ 01.Sprites/
   │        └─ 01.Portrait/
   ├─ 02.Environments/
   │  └─ 03.Home/
   │     └─ Interior/
   │        └─ 01.Sprites/
   ├─ 03.Crafting/
   │  └─ 05.Serving/
   │     └─ 01.Sprites/
   ├─ 04.Cutscenes/
   │  └─ 01.Lab/
   │     └─ 01.Sprites/
   │        └─ Legacy/
   └─ 05.UI/
      └─ Source/
         └─ 01.Sprites/
```

## 02.ContentAuthoring

```text
02.ContentAuthoring/
└─ system/
```

## 03.Documentation

```text
03.Documentation/
├─ 01.Design/
│  └─ Part2_OperationsSystem_Implementation_Specification/
├─ 02.Architecture/
│  └─ Notes/
├─ 03.ArtReferences/
├─ 04.AssetMigration/
│  └─ 20260929/
├─ 05.Handoff/
└─ 06.Validation/
   ├─ PlayMode/
   └─ RefactoringC/
```
