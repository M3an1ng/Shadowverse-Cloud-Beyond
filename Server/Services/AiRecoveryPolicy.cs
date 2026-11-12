using System.Runtime.CompilerServices;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

// Read-only policy ablation. The native game state, rules, model artifact and learning sampler
// remain identical. A benchmark selects its control seat before either player's first action.
internal static class AiRecoveryPolicy
{
    internal sealed class State {internal Guid? DisabledPlayer;}
    private static readonly ConditionalWeakTable<GameState,State> Games=new();
    private static readonly AsyncLocal<bool?> Current=new();
    internal static State For(GameState game)=>Games.GetOrCreateValue(game);
    internal static bool Enabled=>Current.Value??true;
    private sealed class Lease(Action release):IDisposable
    {
        public void Dispose()=>Interlocked.Exchange(ref release,null!)?.Invoke();
    }
    internal static IDisposable Use(GameState? game,Guid actor)
    {
        var previous=Current.Value;
        Current.Value=game==null||game.LegacyAiPlayerId!=actor&&For(game).DisabledPlayer!=actor;
        return new Lease(()=>Current.Value=previous);
    }
}
