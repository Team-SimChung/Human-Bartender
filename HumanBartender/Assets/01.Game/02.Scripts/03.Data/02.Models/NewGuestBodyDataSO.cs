using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewGuestBodyPartData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("gender")] public string Gender { get; set; }
    [field: SerializeField][JsonProperty("personalities")] public string[] Personalities { get; set; }
    [field: SerializeField][JsonProperty("mode")] public string Mode { get; set; }
    [field: SerializeField][JsonProperty("sprite")] public string Sprite { get; set; }
    /// <summary>표정별 스프라이트 경로. 현재 데이터는 전부 null이지만 스키마에는 들어 있다.</summary>
    [JsonProperty("emotions")] public Dictionary<string, string> Emotions { get; set; }
    [JsonProperty("parts")] public Dictionary<string, string> Parts { get; set; }
    [field: SerializeField][JsonProperty("weight")] public int Weight { get; set; }
}

/// <summary>
/// 외형 파츠가 놓이는 슬롯. 값은 겹쳐 그리는 순서이며 작을수록 뒤에 깔린다
/// (일반 손님 외형 생성 시스템 §2의 레이어 표).
///
/// 순서를 바꾸거나 슬롯을 늘릴 때는 데이터 빌드 스크립트의 슬롯 목록과 렌더러 쪽도 함께 고쳐야 한다.
/// 한쪽만 고치면 빌드는 통과하는데 화면에는 새 슬롯이 나오지 않는다.
/// </summary>
public enum EGuestBodySlot
{
    Body = 0,
    Outfit = 10,
    Outerwear = 20,
    Necklace = 30,
    Eyes = 40,
    Eyebrows = 50,
    Mouth = 60,
    Hair = 70,
    ArmAccessory = 80,
}

/// <summary>
/// 함께 쓸 수 없는 파츠 두 개(§4). a와 b의 순서는 뜻이 같다.
/// 예: 팔 액세서리는 민소매가 아닌 상의와 겹치면 소매 위에 그려진다.
/// </summary>
[Serializable]
public struct NewGuestBodyExclusion
{
    [field: SerializeField][JsonProperty("a")] public string A { get; set; }
    [field: SerializeField][JsonProperty("b")] public string B { get; set; }
}

/// <summary>
/// 한 성별의 기본 조합(§5). 데이터가 깨져 외형을 못 고를 때 통째로 이 조합으로 되돌린다 —
/// 문제가 생긴 슬롯 하나만 갈아 끼우면 새 파츠가 나머지와 금지 조합을 이룰 수 있다.
/// 선택 슬롯은 모두 null이다.
/// </summary>
[Serializable]
public class NewGuestBodyDefault
{
    [field: SerializeField][JsonProperty("body")] public string Body { get; set; }
    [field: SerializeField][JsonProperty("outfit")] public string Outfit { get; set; }
    [field: SerializeField][JsonProperty("outerwear")] public string Outerwear { get; set; }
    [field: SerializeField][JsonProperty("necklace")] public string Necklace { get; set; }
    [field: SerializeField][JsonProperty("eyes")] public string Eyes { get; set; }
    [field: SerializeField][JsonProperty("eyebrows")] public string Eyebrows { get; set; }
    [field: SerializeField][JsonProperty("mouth")] public string Mouth { get; set; }
    [field: SerializeField][JsonProperty("hair")] public string Hair { get; set; }
    [field: SerializeField][JsonProperty("arm_accessory")] public string ArmAccessory { get; set; }

    /// <summary>슬롯에 지정된 기본 파츠 id. 선택 슬롯은 null이다.</summary>
    public string GetId(EGuestBodySlot slot) => slot switch
    {
        EGuestBodySlot.Body => Body,
        EGuestBodySlot.Outfit => Outfit,
        EGuestBodySlot.Outerwear => Outerwear,
        EGuestBodySlot.Necklace => Necklace,
        EGuestBodySlot.Eyes => Eyes,
        EGuestBodySlot.Eyebrows => Eyebrows,
        EGuestBodySlot.Mouth => Mouth,
        EGuestBodySlot.Hair => Hair,
        EGuestBodySlot.ArmAccessory => ArmAccessory,
        _ => null,
    };
}

[Serializable]
public class NewGuestBodyDataBase
{
    [field: SerializeField][JsonProperty("bodies")] public NewGuestBodyPartData[] Bodies { get; set; }
    [field: SerializeField][JsonProperty("outfits")] public NewGuestBodyPartData[] Outfits { get; set; }
    [field: SerializeField][JsonProperty("outerwears")] public NewGuestBodyPartData[] Outerwears { get; set; }
    [field: SerializeField][JsonProperty("necklaces")] public NewGuestBodyPartData[] Necklaces { get; set; }
    [field: SerializeField][JsonProperty("eyes")] public NewGuestBodyPartData[] Eyes { get; set; }
    [field: SerializeField][JsonProperty("eyebrows")] public NewGuestBodyPartData[] Eyebrows { get; set; }
    [field: SerializeField][JsonProperty("mouths")] public NewGuestBodyPartData[] Mouths { get; set; }
    [field: SerializeField][JsonProperty("hairs")] public NewGuestBodyPartData[] Hairs { get; set; }
    [field: SerializeField][JsonProperty("arm_accessories")] public NewGuestBodyPartData[] ArmAccessories { get; set; }

    /// <summary>성별별 기본 조합. 키는 "m"/"f"다.</summary>
    [JsonProperty("defaults")] public Dictionary<string, NewGuestBodyDefault> Defaults { get; set; }

    /// <summary>금지 조합. 행이 없으면 빈 배열이며, 그때는 금지가 없는 상태다.</summary>
    [field: SerializeField][JsonProperty("exclusions")] public NewGuestBodyExclusion[] Exclusions { get; set; }

    /// <summary>
    /// 이 슬롯에 등록된 파츠 전부. 슬롯이 늘면 여기와 EGuestBodySlot을 함께 고친다 —
    /// 고르는 쪽과 그리는 쪽이 같은 목록을 봐야 새 슬롯이 조용히 빠지지 않는다.
    /// </summary>
    public NewGuestBodyPartData[] GetParts(EGuestBodySlot slot) => slot switch
    {
        EGuestBodySlot.Body => Bodies,
        EGuestBodySlot.Outfit => Outfits,
        EGuestBodySlot.Outerwear => Outerwears,
        EGuestBodySlot.Necklace => Necklaces,
        EGuestBodySlot.Eyes => Eyes,
        EGuestBodySlot.Eyebrows => Eyebrows,
        EGuestBodySlot.Mouth => Mouths,
        EGuestBodySlot.Hair => Hairs,
        EGuestBodySlot.ArmAccessory => ArmAccessories,
        _ => null,
    };

    /// <summary>
    /// 비워 둘 수 있는 슬롯인지(§3.1). 겉옷·목걸이·팔 액세서리는 안 쓸 수 있고,
    /// 그때 손님 기록에는 null이 들어가며 렌더러는 그 레이어를 만들지 않는다.
    /// </summary>
    public static bool IsOptional(EGuestBodySlot slot)
    {
        return slot == EGuestBodySlot.Outerwear
            || slot == EGuestBodySlot.Necklace
            || slot == EGuestBodySlot.ArmAccessory;
    }

    /// <summary>레이어 순서대로 나열한 슬롯 전체.</summary>
    public static readonly EGuestBodySlot[] AllSlots =
    {
        EGuestBodySlot.Body,
        EGuestBodySlot.Outfit,
        EGuestBodySlot.Outerwear,
        EGuestBodySlot.Necklace,
        EGuestBodySlot.Eyes,
        EGuestBodySlot.Eyebrows,
        EGuestBodySlot.Mouth,
        EGuestBodySlot.Hair,
        EGuestBodySlot.ArmAccessory,
    };

    /// <summary>id로 파츠를 찾는다. 어느 슬롯인지 몰라도 되도록 전 슬롯을 훑는다.</summary>
    public bool TryGetPart(string id, out NewGuestBodyPartData part)
    {
        part = default;
        if (string.IsNullOrEmpty(id)) return false;

        foreach (var slot in AllSlots)
        {
            var parts = GetParts(slot);
            if (parts == null) continue;

            foreach (var candidate in parts)
            {
                if (candidate.Id != id) continue;

                part = candidate;
                return true;
            }
        }

        return false;
    }
}

/// <summary>StreamingAssets/json/guest_bodies.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewGuestBodyDataSO", menuName = "Data/New/GuestBodyDataSO")]
public class NewGuestBodyDataSO : ScriptableObject
{
    public NewGuestBodyDataBase guestBodyData;
}
