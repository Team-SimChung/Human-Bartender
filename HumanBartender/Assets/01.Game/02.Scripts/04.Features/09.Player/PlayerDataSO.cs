using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어의 재화, 캐릭터 호감도, 스킬 숙련도, 스토리 플래그를 보관하는 세이브 데이터 SO.
/// 여러 매니저가 IPlayerDataReader/IPlayerDataWriter로 주입받아 공유하는 런타임 상태 저장소.
/// </summary>
[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerDataSO : ScriptableObject, IPlayerDataReader, IPlayerDataWriter
{
    [Header("Header")]
    [SerializeField] int money;
    [SerializeField] IntEvent addMoneyEvent;
    [SerializeField] IntEvent setMoneyEvent;

    [Header("Character Affinity")]
    [Tooltip("캐릭터별 호감도의 단일 원본")]
    [FormerlySerializedAs("characterTierDatas")]
    [SerializeField] List<CharacterAffinityData> characterAffinityDatas = new();


    [Header("Skill Tier")]
    [Tooltip("숙련도 원시 수치. 지금 AddSkillTier를 부르는 곳이 없어 계속 0이다.")]
    [SerializeField] int skillTierAmount = 0;


    [Header("Flag")]
    [SerializeField] Dictionary<string, bool> flagList = new(); // Dictionary는 인스펙터에 표시되지 않음(직렬화 안 됨), 코드로만 조작

    /// <summary>새 게임/데이터 초기화. 모든 캐릭터 호감도와 플래그를 비우고 재화·스킬을 0으로 되돌린다.</summary>
    public void Init()
    {
        ReplaceProgress(0, 0, new Dictionary<string, int>(), new Dictionary<string, bool>());
    }

    #region Money
    /// <summary>
    /// 재화를 증감하고 0 이상으로 확정한 뒤 실제 반영된 변화량을 알린다.
    /// 요청값과 실제 변화량이 다를 수 있으므로 구독자는 이벤트 값을 delta로만 사용한다.
    /// </summary>
    public void AddMoney(int val)
    {
        int previousMoney = money;
        long requestedMoney = (long)previousMoney + val;
        if (requestedMoney < 0)
        {
            money = 0;
        }
        else if (requestedMoney > int.MaxValue)
        {
            money = int.MaxValue;
        }
        else
        {
            money = (int)requestedMoney;
        }

        int appliedDelta = money - previousMoney;
        if (appliedDelta != 0)
        {
            NotifyMoneyEvent(addMoneyEvent, appliedDelta, "OnAddMoney");
        }
    }

    /// <summary>재화를 절대값으로 설정하고 확정된 최종 잔액을 알린다.</summary>
    public void SetMoney(int val)
    {
        if (val < 0)
        {
            money = 0;
        }
        else
        {
            money = val;
        }

        NotifyMoneyEvent(setMoneyEvent, money, "OnSetMoney");
    }

    public int HasMoney()
    {
        return money;
    }

    /// <summary>
    /// 요청한 비용만 차감한다. 음수 비용은 허용하지 않는다.
    /// </summary>
    public bool TrySpend(int cost)
    {
        if (cost < 0) throw new System.ArgumentOutOfRangeException(nameof(cost));
        if (cost == 0) return true;
        if (HasEnoughMoney(cost))
        {
            AddMoney(-cost);
            return true;
        }

        return false;
    }

    public bool HasEnoughMoney(int val)
    {
        if (val < 0) return false;
        return money >= val;
    }

    static void NotifyMoneyEvent(IntEvent channel, int value, string eventName)
    {
        if (channel == null) return;

        try
        {
            channel.Raise(value);
        }
        catch (System.Exception error)
        {
            Debug.LogError($"[PlayerData] {eventName} 구독자 처리 중 오류가 발생했습니다. 상태 변경은 이미 확정되었습니다.");
            Debug.LogException(error);
        }
    }


    #endregion

    
    #region CharacterAffinity

    /// <summary>캐릭터 호감도를 누적한다. 등록되지 않은 캐릭터는 호감도 0에서 시작한다.</summary>
    public void AddCharacterAffinityAmount(string id, int val = 0)
    {
        CharacterAffinityData affinity = GetOrCreateCharacterAffinity(id);
        affinity.affinityAmount += val;
    }

    /// <summary>캐릭터 호감도를 절대값으로 설정한다 (세이브 로드 등).</summary>
    public void SetCharacterAffinityAmount(string id, int val)
    {
        CharacterAffinityData affinity = GetOrCreateCharacterAffinity(id);
        affinity.affinityAmount = val;
    }

    /// <summary>호감도 원시 수치를 반환한다 (미등록 시 0).</summary>
    public int GetCurCharacterAffinityValue(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return 0;
        CharacterAffinityData affinity = FindCharacterAffinity(id);
        if (affinity == null) return 0;

        return affinity.affinityAmount;
    }

    CharacterAffinityData GetOrCreateCharacterAffinity(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("캐릭터 ID가 비어 있습니다.", nameof(id));

        CharacterAffinityData existingAffinity = FindCharacterAffinity(id);
        if (existingAffinity != null) return existingAffinity;

        CharacterAffinityData newAffinity = new(id, 0);
        characterAffinityDatas.Add(newAffinity);
        return newAffinity;
    }

    CharacterAffinityData FindCharacterAffinity(string id)
    {
        foreach (CharacterAffinityData affinity in characterAffinityDatas)
        {
            if (affinity != null && string.Equals(affinity.characterId, id, System.StringComparison.Ordinal))
                return affinity;
        }

        return null;
    }

    #endregion


    #region SkillTier

    /// <summary>스킬 숙련도 수치를 누적한다.</summary>
    public void AddSkillTier(int val)
    {
        skillTierAmount += val;
    }
    public void SetSkillAmount(int value) => skillTierAmount = value;
    public int GetSkillValue()
    {
        return skillTierAmount;
    }

    #endregion


    #region Flag

    /// <summary>스토리 플래그 값을 설정한다 (없으면 추가, 있으면 갱신).</summary>
    public void AddFlag(string id, bool value)
    {
        flagList[CanonicalFlagKey(id)] = value;
    }
    /// <summary>플래그 값을 조회한다. 등록되지 않았으면 false.</summary>
    public bool CheckFlag(string id)
    {
        return flagList.TryGetValue(CanonicalFlagKey(id), out bool value) && value;
    }

    public static string CanonicalFlagKey(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("플래그 키가 비어 있습니다.", nameof(id));
        string key = id.StartsWith("flag.", StringComparison.Ordinal) ? id.Substring(5) : id;
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("플래그 키가 비어 있습니다.", nameof(id));
        return key;
    }

    public Dictionary<string, int> ReadAffinity()
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in characterAffinityDatas)
            if (item != null) values.Add(item.characterId, item.affinityAmount);
        return values;
    }

    public Dictionary<string, bool> ReadFlags() => new(flagList, StringComparer.Ordinal);

    /// <summary>검증된 진행 값 전체를 알림 없이 교체한 뒤 최종 잔액만 알린다.</summary>
    public void ReplaceProgress(int newMoney, int newSkill, IReadOnlyDictionary<string, int> affinity,
        IReadOnlyDictionary<string, bool> flags)
    {
        if (newMoney < 0) throw new ArgumentOutOfRangeException(nameof(newMoney));
        if (affinity == null || flags == null) throw new ArgumentNullException("진행 목록");
        var nextAffinity = new List<CharacterAffinityData>();
        foreach (var pair in affinity)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) throw new ArgumentException("빈 캐릭터 ID입니다.");
            nextAffinity.Add(new CharacterAffinityData(pair.Key, pair.Value));
        }
        var nextFlags = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var pair in flags)
            if (!nextFlags.TryAdd(CanonicalFlagKey(pair.Key), pair.Value))
                throw new ArgumentException($"중복 플래그 키입니다: {pair.Key}");

        money = newMoney;
        skillTierAmount = newSkill;
        characterAffinityDatas = nextAffinity;
        flagList = nextFlags;
        NotifyMoneyEvent(setMoneyEvent, money, "OnSetMoney");
    }


    #endregion
}


/// <summary>캐릭터 한 명의 호감도 누적치.</summary>
[System.Serializable]
public class CharacterAffinityData
{
    public string characterId;
    public int affinityAmount;

    public CharacterAffinityData(string characterId, int affinityAmount)
    {
        this.characterId = characterId;
        this.affinityAmount = affinityAmount;
    }
}
