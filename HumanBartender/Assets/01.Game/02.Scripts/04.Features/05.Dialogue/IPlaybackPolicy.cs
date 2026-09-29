/// <summary>
/// 캐릭터 파츠 애니메이션 재생 정책 인터페이스 (대사 시작/종료, 일반 재생 시점에 호출됨).
/// </summary>
public interface IPlaybackPolicy
{
    void OnPlay(AnimationPart animPart);
    void OnDialogueStart(AnimationPart animPart);
    void OnDialogueEnd(AnimationPart animPart);
}
