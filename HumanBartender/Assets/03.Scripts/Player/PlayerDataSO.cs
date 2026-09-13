using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 재화/캐릭터 호감도·카르마/스킬 숙련도/스토리 플래그를 보관하는 세이브 데이터 SO.
/// 여러 매니저가 IPlayerDataReader/IPlayerDataWriter로 주입받아 공유하는 런타임 상태 저장소.
/// </summary>
[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerDataSO : ScriptableObject, IPlayerDataReader, IPlayerDataWriter
{
    [Header("Header")]
    [SerializeField] int money;
    [SerializeField] IntEvent addMoneyEvent;
    [SerializeField] IntEvent setMoneyEvent;

    [Header("Character Tier")]
    [Tooltip("인스펙터 보여주기 용, 내부적으로 Dic 사용")]
    [SerializeField] List<CharacterTierData> characterTierDatas = new();
    Dictionary<string, CharacterTierData> characterTierDics = new();


    [Header("Skill Tier")]
    [Tooltip("숙련도 원시 수치. 지금 AddSkillTier를 부르는 곳이 없어 계속 0이다.")]
    [SerializeField] int skillTierAmount = 0;


    [Header("Flag")]
    [SerializeField] Dictionary<string, bool> flagList = new(); // Dictionary는 인스펙터에 표시되지 않음(직렬화 안 됨), 코드로만 조작

    /// <summary>새 게임/데이터 초기화. 모든 캐릭터 티어/플래그를 비우고 재화·스킬을 0으로 되돌린다.</summary>
    public void Init()
    {
        characterTierDatas = new List<CharacterTierData>();
        characterTierDics = new Dictionary<string, CharacterTierData>();

        flagList = new Dictionary<string, bool>();

        money = 0;
        skillTierAmount = 0;
    }

    #region Money
    /// <summary>재화를 증감(음수 가능)시키고 이벤트를 발생시킨다. 0 미만이 되지 않도록 clamp.</summary>
    public void AddMoney(int val)
    {
        money += val;
        addMoneyEvent?.Raise(val);

        if (money <= 0) money = 0;
    }
    public int HasMoney()
    {
        return money;
    }

    /// <summary>
    /// (인수인계 메모) 매개변수 cost를 사용하지 않고 100으로 고정 차감한다 — 호출부와 의도가 다를 수 있으니 확인 필요.
    /// </summary>
    public bool TrySpend(int cost)
    {
        if (HasEnoughMoney(100))
        {
            AddMoney(-100);
            return true;
        }

        return false;
    }
    public bool HasEnoughMoney(int val) { return money >= val; }


    #endregion

    #region CharacterTier

    /// <summary>아직 등록되지 않은 캐릭터라면 초기 호감도/카르마 값으로 새 항목을 추가한다.</summary>
    public void AddNewCharacter(string id, int defaultVal = 0)
    {
        if(!characterTierDics.ContainsKey(id))
        {
            characterTierDics.Add(id, new CharacterTierData(id, defaultVal, defaultVal));
            characterTierDatas.Add(characterTierDics[id]);
        }
    }
    /// <summary>캐릭터 호감도를 누적한다. 미등록 캐릭터면 val을 초기값으로 새로 등록한다.</summary>
    public void AddCharacterAffinityAmount(string id, int val = 0)
    {
        if (!characterTierDics.ContainsKey(id))
        {
            AddNewCharacter(id, val);
        }
        else
        {
            characterTierDics[id].affinityAmount += val;
        }
    }
    /// <summary>캐릭터 카르마를 누적한다. 미등록 캐릭터면 val을 초기값으로 새로 등록한다.</summary>
    public void AddCharacterKarmaAmount(string id, int val = 0)
    {
        if (!characterTierDics.ContainsKey(id))
        {
            AddNewCharacter(id, val);
        }
        else
        {
            characterTierDics[id].karamaAmount += val;
        }
    }

    /// <summary>캐릭터 호감도를 절대값으로 설정한다 (세이브 로드 등).</summary>
    public void SetCharacterAffinityAmount(string id, int val)
    {
        if (!characterTierDics.ContainsKey(id))
        {
            AddNewCharacter(id, val);
        }
        else
        {
            characterTierDics[id].affinityAmount = val;
        }
    }
    /// <summary>캐릭터 카르마를 절대값으로 설정한다 (세이브 로드 등).</summary>
    public void SetCharacterKarmaAmount(string id, int val)
    {
        if (!characterTierDics.ContainsKey(id))
        {
            AddNewCharacter(id, val);
        }
        else
        {
            characterTierDics[id].karamaAmount = val;
        }
    }

    /// <summary>호감도 원시 수치를 반환한다 (미등록 시 0).</summary>
    public int GetCurCharacterAffinityValue(string id)
    {
        if (!characterTierDics.ContainsKey(id)) return 0;

        return characterTierDics[id].affinityAmount;
    }

    #endregion


    #region SkillTier

    /// <summary>스킬 숙련도 수치를 누적한다.</summary>
    public void AddSkillTier(int val)
    {
        skillTierAmount += val;
    }
    public int GetSkillValue()
    {
        return skillTierAmount;
    }

    #endregion


    #region Flag

    /// <summary>스토리 플래그 값을 설정한다 (없으면 추가, 있으면 갱신).</summary>
    public void AddFlag(string id, bool value)
    {
        if (flagList.ContainsKey(id))
            flagList[id] = value;
        else
            flagList.Add(id, value);

    }
    /// <summary>플래그 값을 조회한다. 등록되지 않았으면 false.</summary>
    public bool CheckFlag(string id)
    {
        if (!flagList.ContainsKey(id)) return false;

        return flagList[id];
    }


    #endregion
}


/// <summary>캐릭터 한 명의 호감도/카르마 누적치.</summary>
[System.Serializable]
public class CharacterTierData
{
    public string characterId;
    public int affinityAmount;
    public int karamaAmount;

    public CharacterTierData(string characterId, int affinityAmount, int karamaAmount)
    {
        this.characterId = characterId;
        this.affinityAmount = affinityAmount;
        this.karamaAmount = karamaAmount;
    }
}
