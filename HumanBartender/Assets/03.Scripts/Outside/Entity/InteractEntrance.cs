using UnityEngine;

/// <summary>바(Bar)/집 등 씬 경계에 위치한 출입구. 현재 게임 흐름(GameFlow)이 지정된 값과 일치할 때만 씬 전환을 허용한다.</summary>
public class InteractEntrance : InteractiveEntity
{
    [Space(20f)]
    [SerializeField] public Vector2 spawnPoint;
    [SerializeField] EGameFlow eGameFlow;
    [SerializeField] string targetScene;

    public override void Interact(IInteractor player)
    {
        isInteract = false;
        player.State = EInteractorState.Interct;
        if (GameStateManager.Instance.GameFlow == eGameFlow)
            SceneTransitionManager.Instance.LoadScene(targetScene);
    }
}
