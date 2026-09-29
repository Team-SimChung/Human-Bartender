using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

/// <summary>script 스텝(say/move/fx/...)의 종류를 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewStepType
{
    [EnumMember(Value = "say")] Say,
    [EnumMember(Value = "move")] Move,
    [EnumMember(Value = "fx")] Fx,
    [EnumMember(Value = "enter")] Enter,
    [EnumMember(Value = "exit")] Exit,
    [EnumMember(Value = "sfx")] Sfx,
    [EnumMember(Value = "choice")] Choice,
    [EnumMember(Value = "order")] Order,
    [EnumMember(Value = "craft")] Craft,
    [EnumMember(Value = "serve")] Serve,
    [EnumMember(Value = "effect")] Effect,
    [EnumMember(Value = "end_part")] EndPart,
    [EnumMember(Value = "timeline")] Timeline,
    // street.json이 쓰는 값들. 없으면 그 파일 역직렬화가 통째로 끊긴다.
    [EnumMember(Value = "set_state")] SetState,
    [EnumMember(Value = "goto")] Goto,
}

/// <summary>씬이 진행되는 하루 중 구간을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewScenePhase
{
    [EnumMember(Value = "intro")] Intro,
    [EnumMember(Value = "home")] Home,
    [EnumMember(Value = "street")] Street,
    [EnumMember(Value = "commute_in")] CommuteIn,
    [EnumMember(Value = "commute_out")] CommuteOut,
    [EnumMember(Value = "bar")] Bar,
    [EnumMember(Value = "bar_open")] BarOpen,
    [EnumMember(Value = "dream")] Dream,
}

/// <summary>씬이 시작되는 방식을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewSceneTrigger
{
    [EnumMember(Value = "auto")] Auto,
    [EnumMember(Value = "interact")] Interact,
    [EnumMember(Value = "cameo")] Cameo,
    [EnumMember(Value = "manual")] Manual,
    [EnumMember(Value = "proximity")] Proximity
}

/// <summary>캐릭터의 등장 역할군을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewCharacterRole
{
    [EnumMember(Value = "player")] Player,
    [EnumMember(Value = "master")] Master,
    [EnumMember(Value = "guest")] Guest,
    [EnumMember(Value = "cutscene")] Cutscene,
    [EnumMember(Value = "npc_street")] NpcStreet,
}

/// <summary>칵테일 제조 기법을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewMixMethod
{
    [EnumMember(Value = "build")] Build,
    [EnumMember(Value = "shake")] Shake,
    [EnumMember(Value = "stir")] Stir,
    [EnumMember(Value = "none")] None,
}

/// <summary>
/// 액체·가루 재료 하나를 다루는 동작.
///
/// 레시피 한 줄의 action과 재료의 default_action이 같은 값 집합을 쓴다. 둘이 같은 열거형을 공유하는
/// 게 중요하다 — 레시피에 있는 재료는 레시피의 action대로, 없는 재료(오선택)는 자기 default_action대로
/// 기믹이 만들어지므로, 두 값을 같은 자리에서 비교할 수 있어야 한다.
///
/// fill_up은 조작 방식이 pour와 완전히 같고(같은 따르기 기믹을 재사용한다) 제조 순서상의 위치만
/// 다르다 — 믹스 이후에 잔을 채운다. 그래서 별도 기믹이 아니라 별도 action으로 구분한다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewRecipeAction
{
    [EnumMember(Value = "pour")] Pour,
    [EnumMember(Value = "squeeze")] Squeeze,
    [EnumMember(Value = "powder")] Powder,
    [EnumMember(Value = "fill_up")] FillUp,
}

/// <summary>
/// 재료가 진열되는 선반. 술 선반과 냉장고는 진열 방식이 달라서(술은 재료당 1병 개별,
/// 냉장고는 3개 묶음) 재료 분류로 유도하지 않고 데이터가 직접 지정한다 —
/// 레드와인·샴페인은 술 선반이지만 병맥주는 냉장고다.
///
/// 자동으로 넣어 주는 재료(레몬·라임·설탕)는 선반에 오브젝트가 없어서 값이 비어 있다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewShelfGroup
{
    [EnumMember(Value = "liquor")] Liquor,
    [EnumMember(Value = "fridge")] Fridge,
}

/// <summary>
/// 따르기 전에 먼저 해야 하는 손질. 지금은 병뚜껑을 여는 것 하나뿐이다.
///
/// 손질은 재료의 기본 동작을 대체하지 않고 앞에 한 단계 더 붙는다 —
/// 병맥주는 열고 나서 따라야 제조가 끝난다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewPrepAction
{
    [EnumMember(Value = "open")] Open,
}

/// <summary>
/// 데이터 한 줄이 확정된 내용인지, 아직 채우는 중인지 구분한다.
///
/// tbd가 먼저다 — json에 status가 없으면 0번이 들어오는데, 그때 "확정됨"으로 읽히면 아직
/// 손대지 않은 데이터를 완성된 것으로 착각하게 된다. 빠진 값은 미확정으로 보는 게 안전하다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewDataStatus
{
    [EnumMember(Value = "tbd")] Tbd,
    [EnumMember(Value = "confirmed")] Confirmed,
}

/// <summary>
/// 점수 구간표가 무엇을 재는 구간인지. 수량 오차와 시간 초과가 같은 표에 섞여 있어 이 값으로 갈라 본다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewScoreBandType
{
    /// <summary>수량 오차율 → 개별 기믹 점수.</summary>
    [EnumMember(Value = "quantity")] Quantity,

    /// <summary>전체 제조시간 초과율 → 감점.</summary>
    [EnumMember(Value = "overtime")] Overtime,
}

/// <summary>
/// 채점이 정상적으로 끝났는지, 원본 데이터가 미확정이라 채점 자체를 막았는지.
///
/// 둘뿐이다. 플레이를 못한 것(기믹 미수행)과 데이터가 잘못된 것은 다른 층위라서,
/// 전자는 0점이라는 정상 결과로 남고 후자만 여기서 갈린다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewScoreStatus
{
    [EnumMember(Value = "scored")] Scored,
    /// <summary>Target 수량·단위·Config 값이 미확정이라 계산할 수 없다. 임의값으로 메우지 않는다.</summary>
    [EnumMember(Value = "data_error")] DataError,
}

/// <summary>레시피 계량 단위를 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewUnit
{
    [EnumMember(Value = "oz")] Oz,
    [EnumMember(Value = "tsp")] Tsp,
    [EnumMember(Value = "ml")] Ml,
}

/// <summary>선반 아이템의 종류(재료/잔/도구/가니시)를 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewShelfKind
{
    [EnumMember(Value = "ingredient")] Ingredient,
    [EnumMember(Value = "glass")] Glass,
    [EnumMember(Value = "tool")] Tool,
    [EnumMember(Value = "garnish")] Garnish,
}

/// <summary>
/// 재료(kind=ingredient)의 세부 카테고리를 나타내는 열거형.
/// 잔/도구/가니시는 category가 null이므로 사용하는 쪽에서 nullable로 받아야 한다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewIngredientCategory
{
    [EnumMember(Value = "base")] Base,
    [EnumMember(Value = "wine_beer")] WineBeer,
    [EnumMember(Value = "mixer")] Mixer,
    [EnumMember(Value = "fruit")] Fruit,
    [EnumMember(Value = "powder")] Powder,
    [EnumMember(Value = "juice")] Juice,
    [EnumMember(Value = "liqueur")] Liqueur,
    [EnumMember(Value = "syrup")] Syrup,
    [EnumMember(Value = "dairy")] Dairy,
    /// <summary>위 분류에 들어가지 않는 재료(커피 등).</summary>
    [EnumMember(Value = "other")] Other,
}

/// <summary>거리 인터랙트 포인트의 종류를 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewInteractKind
{
    [EnumMember(Value = "object")] Object,
    // 데이터에서 npc가 actor로 바뀌었다. interact_points.json에 npc는 한 곳도 남아 있지 않다.
    [EnumMember(Value = "actor")] Actor,
    [EnumMember(Value = "shop")] Shop,
    [EnumMember(Value = "gimmick")] Gimmick,

}

/// <summary>인터랙트 포인트가 활성화되는 출퇴근 구간을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewInteractPhase
{
    [EnumMember(Value = "commute_in")] CommuteIn,
    [EnumMember(Value = "commute_out")] CommuteOut,
    [EnumMember(Value = "both")] Both,
}

/// <summary>인터랙트 포인트의 컨텐츠 소비 방식을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewSelectionMode
{
    [EnumMember(Value = "once")] Once,
    [EnumMember(Value = "repeat")] Repeat,
    [EnumMember(Value = "conditional")] Conditional,
    [EnumMember(Value = "sequential")] Sequential,
}
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewStreetGroup
{

    [EnumMember(Value = "np_shiba")] Np_shiba,
    [EnumMember(Value = "")] None,
}
/// <summary>주문 규칙 평가 결과를 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewOrderVerdict
{
    [EnumMember(Value = "fulfill")] Fulfill,
    [EnumMember(Value = "care")] Care,
    [EnumMember(Value = "partial")] Partial,
    [EnumMember(Value = "miss")] Miss,
}

/// <summary>서빙 결과 등급을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewGrade
{
    [EnumMember(Value = "excellent")] Excellent,
    [EnumMember(Value = "good")] Good,
    [EnumMember(Value = "decent")] Decent,
    [EnumMember(Value = "poor")] Poor,
    [EnumMember(Value = "sewage")] Sewage,
}

/// <summary>손님의 칵테일 취향 단계를 나타내는 열거형. tastes.json과 balance.json의 affinity_matrix가 공유한다.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewTasteTier
{
    [EnumMember(Value = "love")] Love,
    [EnumMember(Value = "good")] Good,
    [EnumMember(Value = "ok")] Ok,
    [EnumMember(Value = "dislike")] Dislike,
    [EnumMember(Value = "miss")] Miss,
}

/// <summary>표정 표현 방식(파츠 합성 애니메이션 / 단일 스프라이트)을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewExpressionMode
{
    [EnumMember(Value = "parts_anim")] PartsAnim,
    [EnumMember(Value = "sprite")] Sprite,
}

/// <summary>
/// 표정 파츠 애니메이션의 반복 모드를 나타내는 열거형.
/// 기존 EAnimLoopMode와 의미는 같지만 json 쪽 표기(소문자)에 맞춰 새로 정의했다.
///
/// special_on_dialogue와 on_dialogue는 같은 뜻이다. 데이터가 expressions.json에서
/// character_anim.json으로 넘어오며 표기가 짧아졌는데, 옛 파일도 그대로 읽히게 둘 다 남긴다.
/// 없는 표기를 만나면 Newtonsoft가 예외를 던지고, 그 예외는 뒤따르는 파일 로드를 전부 막는다.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewAnimLoopMode
{
    [EnumMember(Value = "always")] Always,
    [EnumMember(Value = "always_on_dialogue")] AlwaysOnDialogue,
    [EnumMember(Value = "special_on_dialogue")] SpecialOnDialogue,
    [EnumMember(Value = "on_dialogue")] OnDialogue,
}

/// <summary>인물 정보(dossier) 항목의 분류를 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewDossierKind
{
    [EnumMember(Value = "desc")] Desc,
    [EnumMember(Value = "taste")] Taste,
    [EnumMember(Value = "history")] History,
    [EnumMember(Value = "secret")] Secret,
    [EnumMember(Value = "recent")] Recent,
}

/// <summary>컷신 리소스의 재생 방식을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ENewCutSceneKind
{
    [EnumMember(Value = "timeline")] Timeline,
    [EnumMember(Value = "sprite")] Sprite,
    [EnumMember(Value = "gif")] Gif,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum EFacing
{
    [EnumMember(Value = null)] None, // JSON의 null 또는 미지정에 대응
    [EnumMember(Value = "left")] Left,
    [EnumMember(Value = "right")] Right
}

[JsonConverter(typeof(StringEnumConverter))]
public enum EPlayType
{
    [EnumMember(Value = null)] None, // JSON의 null 또는 미지정에 대응
    [EnumMember(Value = "once")] Once,
    [EnumMember(Value = "repeat")] Repeat
}
[JsonConverter(typeof(StringEnumConverter))]
public enum EActivationMode
{
    [EnumMember(Value = null)] None,
    [EnumMember(Value = "interact")] Interact,
    [EnumMember(Value = "proximity")] Proximity
}
[JsonConverter(typeof(StringEnumConverter))]
public enum EActionType
{
    [EnumMember(Value = null)] None, // JSON의 null 또는 미지정에 대응
    [EnumMember(Value = "scene")] Scene,
    [EnumMember(Value = "scene_group")] Scene_group,
    [EnumMember(Value = "transition")] Transition,
    [EnumMember(Value = "dialogue")] Dialogue,

    /// <summary>대화도 이동도 아닌, 그 자리에서 게임이 처리하는 일(소파에 앉기·엘리베이터 조작 등).</summary>
    [EnumMember(Value = "system")] System
}
public enum ELanguage
{
    Ko,
    En
}