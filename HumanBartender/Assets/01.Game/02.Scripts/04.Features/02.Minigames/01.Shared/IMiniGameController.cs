using Cysharp.Threading.Tasks;

/// <summary>쉐이커/스터/빌드 등 각 제조 미니게임 프리팹이 구현해야 하는 공통 제어 인터페이스.</summary>
public interface IMiniGameController
{
    public void InitGame(UniTaskCompletionSource tcs);
    public void CompleteMade();
    public void OnNextButton();
    public void Serve();
    public void Retry();
}
