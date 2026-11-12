using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiSeededBattleTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly PropertyInfo GameSeed = typeof(GameState).GetProperty("BenchmarkRandom", Hidden)!;
    private static readonly PropertyInfo ContextRandom = typeof(GameContext).GetProperty("BenchmarkRandom", Hidden)!;

    internal static void PreviewAndTransactionIsolation()
    {
        var original = GameServiceProvider.ContextAccessor;
        try
        {
            var ordinary = Run(83101, 0, false);
            Check(!ordinary.Seeded, "ordinary room received benchmark random numbers");
            var baseline = Run(83101, 0, true);
            var withPreviews = Run(83101, 31, true);
            var independent = Run(83102, 9, true);
            Check(baseline.Seeded && baseline.Actions.SequenceEqual(withPreviews.Actions),
                "read-only previews or another game changed the seeded native sequence");
            Check(!baseline.Actions.SequenceEqual(independent.Actions), "distinct seeds produced the same complete fixture sequence");
            Check(baseline.Actions.Distinct().Count() > 1, "each transaction repeated the same seeded selection");
        }
        finally { GameServiceProvider.ContextAccessor = original; }
    }

    private static (bool Seeded, string[] Actions) Run(int seed, int previews, bool local)
    {
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        using var services = AiEndTurnEngineTests.Services(GameDataProvider.Instance, accessor);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var own = new PlayerState(Guid.NewGuid()) { TurnNumber = 2 };
        var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber = 2 };
        var game = new GameState("seeded-battle-" + Guid.NewGuid(), own, enemy, own.PlayerId)
        {
            IsLocalAiTraining = local, Phase = GamePhase.Main, Status = GameStatus.InProgress,
            AbilityManager = sp.GetRequiredService<IAbilityManager>(), BuffManager = sp.GetRequiredService<IBuffManager>()
        };
        var seedState = Activator.CreateInstance(GameSeed.PropertyType, Hidden, null, [seed], null);
        GameSeed.SetValue(game, seedState);
        foreach (var id in Enumerable.Range(1, 10))
            own.Deck.Add(new CardInstance(Guid.NewGuid(), id, own.PlayerId) { CurrentZone = CardZone.Deck });
        GameContext Context() => new(sp, sp.GetRequiredService<IMapper>(), accessor, game, own.PlayerId);
        var getter = sp.GetRequiredService<IDslCompiler>().CompileGetter<object>("context.owner.deck.take(random 3)");
        int[] Selected(GameContext context) => ((IEnumerable<CardInstance>)getter(new object(), context)).Select(card => card.BaseDefinitionId).ToArray();

        using (var inspection = Context())
            Check((ContextRandom.GetValue(inspection) != null) == local, "context did not respect the local training boundary");
        int[]? previous = null;
        for (var i = 0; i < previews; i++)
        {
            using var inspection = Context();
            var cards = Selected(inspection);
            Check(previous == null || previous.SequenceEqual(cards), "repeated read-only DSL at the same revision changed results");
            previous = cards;
        }

        var actions = new List<string>();
        var transactions = sp.GetRequiredService<IGameActionTransactionService>();
        for (var i = 0; i < 3; i++)
        {
            using var context = Context();
            var ev = new SampleEvent(own, ctx =>
            {
                var selected = Selected(ctx);
                var pool = own.Deck.Concat(own.Deck.Take(2).Select(card => new CardInstance(Guid.NewGuid(), card.BaseDefinitionId, own.PlayerId)));
                var discovered = DiscoverEffectHandler.Sample(pool, (Random?)ContextRandom.GetValue(ctx)).Select(card => card.BaseDefinitionId).ToArray();
                Check(selected.Length == 3 && selected.Distinct().Count() == 3 && discovered.Distinct().Count() == 3,
                    "seeded sampling bypassed legal counts or distinct discover definitions");
                actions.Add(string.Join(',', selected) + "/" + string.Join(',', discovered));
            });
            var result = transactions.ProcessTransactionAsync(context, [ev]).GetAwaiter().GetResult();
            Check(result.IsSuccess && !result.IsCancelled, result.ErrorMessage ?? "seeded action failed");
        }
        return (local, actions.ToArray());
    }

    private static void Check(bool success, string reason)
    {
        if (!success) throw new InvalidOperationException(reason);
    }

    private sealed class SampleEvent : GameEventBase, ICommittableEvent
    {
        private readonly Action<GameContext> sample;

        internal SampleEvent(PlayerState player, Action<GameContext> sample) : base(EventType.HandUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            this.sample = sample;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            sample(context);
            return new() { Success = true };
        }
    }
}
