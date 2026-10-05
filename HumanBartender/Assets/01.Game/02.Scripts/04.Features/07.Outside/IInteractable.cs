/// <summary>
/// Unity측에서 IEntity가 2번 상속되는 등의 일을 알아서 처리해줌.
/// 사실 IEntity를 뺴도되지만 좀 더 직관적으로 보게 하기 위함.
/// </summary>
public interface IInteractable : IEntity, ITrackedble
{
    int Priority { get; }           // 여러 상호작용 대상이 겹칠 때 우선순위 결정
    string Label { get; }           // 상호작용 UI에 표시할 텍스트
    bool IsInteracting { get; set; }

    public bool isInteract { get; set; }
    public void Interact(IInteractor player);   // 실제 상호작용 실행
    public void OnFocusEnter();                 // 상호작용 가능 범위 진입(포커스) 시 호출
    public void OnFocusExit();                  // 포커스 이탈 시 호출
}
