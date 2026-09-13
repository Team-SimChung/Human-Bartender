/// <summary>플레이어 데이터를 읽기 전용으로 노출하는 인터페이스. DI를 통해 조회 전용 접근이 필요한 곳에 주입된다.</summary>
public interface IPlayerDataReader
{
    int HasMoney();
    bool HasEnoughMoney(int cost);

    int GetCurCharacterAffinityValue(string id);
    int GetSkillValue();

    bool CheckFlag(string id);
}

/// <summary>플레이어 데이터를 변경하는 인터페이스. DI를 통해 데이터 갱신이 필요한 곳에 주입된다.</summary>
public interface IPlayerDataWriter
{
    void AddMoney(int val);
    bool TrySpend(int cost);

    void SetCharacterAffinityAmount(string id, int val);
    void AddCharacterAffinityAmount(string id, int val);
    void SetCharacterKarmaAmount(string id, int val);
    void AddCharacterKarmaAmount(string id, int val);

    void AddSkillTier(int val);

    void AddFlag(string id, bool value);
}
