using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>바 오프닝 직전 플레이어 상태만 보관한다. 저장 슬롯이나 매출 장부를 소유하지 않는다.</summary>
public sealed class BarSessionCheckpoint
{
    sealed class Snapshot
    {
        readonly int money;
        readonly int skill;
        readonly Dictionary<string, int> affinity;
        readonly Dictionary<string, bool> flags;

        public Snapshot(PlayerDataSO player)
        {
            money = player.HasMoney();
            skill = player.GetSkillValue();
            affinity = player.ReadAffinity();
            flags = player.ReadFlags();
        }

        public void Restore(PlayerDataSO player) => player.ReplaceProgress(money, skill, affinity, flags);
    }

    readonly PlayerDataSO player;
    readonly GameStateManager gameState;
    Snapshot opening;
    int day;

    public BarSessionCheckpoint(PlayerDataSO player, GameStateManager gameState)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
    }

    public void Capture()
    {
        if (opening != null) throw new InvalidOperationException("영업 시작 상태는 한 번만 보관합니다.");
        day = gameState.CurrentDay;
        opening = new Snapshot(player);
    }

    public UniTask<GameProgressionResult> RestartAsync(IGameProgressionService progression,
        CancellationToken cancellationToken)
    {
        if (opening == null || day != gameState.CurrentDay)
            throw new InvalidOperationException("현재 영업의 시작 상태가 없습니다.");
        var rollback = new Snapshot(player);
        return progression.RestartBarAsync(() => opening.Restore(player), () => rollback.Restore(player), cancellationToken);
    }
}
