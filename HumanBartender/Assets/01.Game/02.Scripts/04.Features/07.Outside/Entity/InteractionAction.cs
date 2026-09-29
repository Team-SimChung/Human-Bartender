using UnityEngine;

/// <summary>
/// InteractiveActionEntity가 상호작용 시 실행할 동작을 데이터(SO)로 분리한 전략 패턴 베이스.
/// 같은 엔티티라도 SO만 교체하면 다른 동작을 하도록 만들 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "InteractionAction", menuName = "Scriptable Objects/InteractionAction")]
public abstract  class InteractionAction : ScriptableObject
{
    /// <summary>실제 상호작용 로직. source는 이 액션을 보유한 엔티티, player는 상호작용을 건 주체.</summary>
    public abstract void Execute(InteractiveActionEntity source, IInteractor player);
}
