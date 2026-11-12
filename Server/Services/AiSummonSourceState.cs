using System.Runtime.CompilerServices;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

// Offline comparison/telemetry belongs to AI policy, not the native rules entity.
internal static class AiSummonSourceState
{
    internal sealed class State
    {
        internal Guid? DisabledPlayer;
        internal long Uses;
    }
    private static readonly ConditionalWeakTable<GameState, State> States = new();
    internal static State For(GameState game) => States.GetValue(game, _ => new());
}
