namespace ShadowverseBeyond.Server.Domain.Entities;

public sealed record ReplayBranchOrigin(string GameId, long AtSeq);

public partial class GameState
{
    public ReplayBranchOrigin? ReplayBranch { get; internal set; }

    internal void RestoreRevisions(long state, long buffs, long refresh)
    {
        StateRevision = state;
        BuffRevision = buffs;
        RefreshCycleId = refresh;
    }
}

public partial class PlayerState
{
    internal void RestoreManualEvolutionRight(bool used) => HasUsedManualEvolutionRightThisTurn = used;
}

public partial class CardInstance
{
    internal void RestoreResumeState(CardRuntimeStateSnapshot state, int healthFloor, long combatVersion)
    {
        CopyRuntimeStateFrom(state);
        HealthFloorFromBuffRemoval = healthFloor;
        CombatContinuityVersion = combatVersion;
    }
}
