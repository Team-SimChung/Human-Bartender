using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>현재 상호작용 소유자만 플레이어 입력을 복원한다.</summary>
public sealed class InteractionStateLease : IDisposable
{
    static readonly Dictionary<IInteractor, List<InteractionStateLease>> owners = new();
    readonly IInteractor player;
    readonly EInteractorState previous;
    readonly EInteractorState held;
    InteractionStateLease(IInteractor player, EInteractorState previous, EInteractorState held) { this.player=player; this.previous=previous; this.held=held; }
    public bool IsCurrent => owners.TryGetValue(player, out var stack) &&
        stack.Count > 0 && stack[stack.Count - 1] == this;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => owners.Clear();
    public static InteractionStateLease Acquire(IInteractor player, EInteractorState state = EInteractorState.Interct)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (!owners.TryGetValue(player, out var stack))
            owners[player] = stack = new List<InteractionStateLease>();
        var oldState = stack.Count > 0 ? stack[0].previous : player.State;
        var lease=new InteractionStateLease(player,oldState,state);
        stack.Add(lease);
        player.State=state;
        return lease;
    }
    public void Dispose()
    {
        if (!owners.TryGetValue(player, out var stack)) return;
        bool wasCurrent = IsCurrent;
        if (!stack.Remove(this)) return;
        if (!wasCurrent) return;
        if (stack.Count == 0) owners.Remove(player);
        if (player is UnityEngine.Object obj && obj == null) return;
        if (player.State == held)
            player.State = stack.Count > 0 ? stack[stack.Count - 1].held : previous;
    }
}
