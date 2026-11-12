using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class ChainedTransformTests
{
    internal static void DestroyReanimatedOriginalAfterCopyTransform()
    {
        const int leaderId = 2000000491;
        const int sourceId = 2000000492;
        const int followerId = 2000000493;
        var definitions = new Dictionary<int, CardDefinition>
        {
            [leaderId] = new() { Id = leaderId, Type = CardType.Leader, Health = 20 },
            [sourceId] = new() { Id = sourceId, Type = CardType.Follower, Cost = 2, Attack = 2, Health = 2 },
            [followerId] = new() { Id = followerId, Type = CardType.Follower, Cost = 3, Attack = 3, Health = 3 }
        };
        const string reanimatedCards = "context.triggeringEvent.GeneratedCards";
        var reanimate = new EffectConfig
        {
            Type = EffectType.Reanimate,
            Parameters = new()
            {
                ["MaxCost"] = new StaticValueConfig { Value = 3 },
                ["PlayerScope"] = new StaticValueConfig { Value = "self" }
            },
            ChainedAbilities =
            [
                Chain("context.SourceCard", new EffectConfig
                {
                    Type = EffectType.Transform,
                    Parameters = new()
                    {
                        ["CardSourceMode"] = new StaticValueConfig { Value = "Copy" },
                        ["CopySourceTargets"] = new FormulaValueConfig { Formula = "find(" + reanimatedCards + ", 1)" }
                    }
                }),
                Chain(reanimatedCards, new EffectConfig { Type = EffectType.Destroy })
            ]
        };
        var necromancy = new EffectConfig
        {
            Type = EffectType.Necromancy,
            Parameters = new()
            {
                ["Amount"] = new StaticValueConfig { Value = 2 },
                ["PlayerScope"] = new StaticValueConfig { Value = "self" }
            },
            ChainedAbilities = [Chain(null, reanimate)]
        };
        var ability = new AbilityConfig
        {
            Id = sourceId, CardId = sourceId, Name = "Reanimate, copy and destroy",
            Type = AbilityType.Triggered,
            Trigger = new() { EventTypes = [EventType.PlayCard], RelationType = TriggerRelationType.TargetSelf },
            Effects = [necromancy]
        };
        var abilities = new Dictionary<int, List<AbilityConfig>> { [sourceId] = [ability] };
        var data = Proxy<IGameDataProvider>.Make((method, args) => method.Name switch
        {
            "GetCardDefinition" => definitions.GetValueOrDefault((int)args![0]!),
            "GetAllCardDefinitions" => definitions,
            "GetAbilityConfigs" => abilities.GetValueOrDefault((int)args![0]!) ?? new List<AbilityConfig>(),
            "GetAllAbilityConfigs" => abilities,
            "GetAllBuffDefinitions" => new Dictionary<string, BuffDefinition>(),
            _ => null
        });

        var previousData = GameDataProvider.Instance;
        var previousAccessor = GameServiceProvider.ContextAccessor;
        var accessor = new GameContextAccessor();
        try
        {
            GameDataProvider.SetInstanceForTesting(data);
            GameServiceProvider.ContextAccessor = accessor;
            using var services = AiEndTurnEngineTests.Services(data, accessor);
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;
            var player = new PlayerState(Guid.NewGuid()) { CurrentPlayPoints = 2, MaxPlayPoints = 2, GraveyardCount = 2 };
            var opponent = new PlayerState(Guid.NewGuid());
            var game = new GameState("transform-then-destroy", player, opponent, player.PlayerId)
            {
                Phase = GamePhase.Main, Status = GameStatus.InProgress, IsLocalAiTraining = true,
                AbilityManager = provider.GetRequiredService<IAbilityManager>(),
                BuffManager = provider.GetRequiredService<IBuffManager>()
            };
            var factory = provider.GetRequiredService<ICardFactory>();
            player.Leader = factory.CreateAndRegisterNewCard(game, leaderId, player.PlayerId, CardZone.Field);
            opponent.Leader = factory.CreateAndRegisterNewCard(game, leaderId, opponent.PlayerId, CardZone.Field);
            var source = factory.CreateAndRegisterNewCard(game, sourceId, player.PlayerId, CardZone.Hand);
            var graveCard = factory.CreateAndRegisterNewCard(game, followerId, player.PlayerId, CardZone.Graveyard);
            typeof(CardInstance).GetMethod("SetLastMoveReason", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(graveCard, [CardMoveReason.Destroyed]);

            using var context = new GameContext(provider, provider.GetRequiredService<IMapper>(), accessor, game, player.PlayerId);
            var play = new PlayCardRequestedEvent(player.PlayerId,
                new PlayCardActionDto { CardUid = source.InstanceId, PlayMode = PlayMode.Base, PaidCost = 2 });
            var result = provider.GetRequiredService<IGameActionTransactionService>()
                .ProcessTransactionAsync(context, [play]).GetAwaiter().GetResult();
            if (!result.IsSuccess || result.IsCancelled)
                throw new InvalidOperationException(result.ErrorMessage ?? "Play did not settle.");
            var transformed = result.ProcessedEvents.OfType<TransformEvent>().Single().TransformingCard;
            var destroyed = result.ProcessedEvents.OfType<LeaveFieldEvent>().SingleOrDefault();
            if (player.Field.Count != 1 || transformed != player.Field[0] || transformed.CurrentDefinitionId != followerId
                || game.FindCardInstance(source.InstanceId) != null
                || destroyed == null || destroyed.LeaveSource != source || destroyed.LeaveTarget == transformed
                || destroyed.RoutedKind != LeaveFieldKind.Destroy)
                throw new InvalidOperationException("Only the transformed copy should remain, with the old source credited for destroying the original.");
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(previousData);
            GameServiceProvider.ContextAccessor = previousAccessor;
        }
    }

    private static AbilityConfig Chain(string? sourcePool, EffectConfig effect) => new()
    {
        Name = effect.Type.ToString(), Type = AbilityType.Triggered,
        Trigger = new() { EventTypes = [EventType.EffectCompleted], RelationType = TriggerRelationType.Any },
        Targets = sourcePool == null ? new() : new() { ["target"] = new() { Query = new() { SourcePoolExpression = sourcePool } } },
        Effects = [effect]
    };
}
