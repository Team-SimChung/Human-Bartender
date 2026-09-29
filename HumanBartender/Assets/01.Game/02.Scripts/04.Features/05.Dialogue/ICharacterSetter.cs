using Cysharp.Threading.Tasks;

/// <summary>대화 씬에서 캐릭터 배치/제거/개수 조회를 담당하는 인터페이스.</summary>
public interface ICharacterSetter
{
    public UniTask SetCharacterAsync(string characterId, string expression, ESlotType slotType = ESlotType.Right);
    public int GetCharacterCount();
    public void ResetCharacter(ESlotType slot);
    public void ResetCharacter();
}
