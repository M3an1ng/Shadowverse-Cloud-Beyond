using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public static class ScenarioRunner
    {
        public static ScenarioContext RunScenario(ScenarioCase scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            var dataSet = ScenarioDataLoader.Load();
            var pendingActionService = new ScenarioPendingActionService();
            var seed = StableSeedFromString(string.IsNullOrWhiteSpace(scenario.Id) ? scenario.Name : scenario.Id);

            var services = ScenarioServices.BuildServiceProvider(
                dataSet.DataProvider,
                pendingActionService,
                seed);

            var mapper = services.GetRequiredService<IMapper>();
            var accessor = services.GetRequiredService<IGameContextAccessor>();

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var gameState = BuildGameState(scenario, services);
            var cards = BuildCards(scenario, gameState);
            var processedEvents = new List<IGameEvent>();

            var context = new ScenarioContext(
                scenario,
                services,
                gameState,
                mapper,
                accessor,
                cards,
                pendingActionService,
                new Random(seed),
                processedEvents);

            pendingActionService.BindContext(context);

            RegisterAbilities(gameState, scenario, cards, dataSet.DataProvider);

            var abilityManager = gameState.AbilityManager;
            var effectExecutor = services.GetRequiredService<IEffectExecutor>();
            var transactionService = ScenarioServices.BuildTransactionService(
                services,
                abilityManager,
                pendingActionService,
                effectExecutor);

            foreach (var action in scenario.Actions)
            {
                ExecuteAction(context, transactionService, action);
            }

            using (var assertionContext = new GameContext(
                services,
                mapper,
                accessor,
                gameState,
                gameState.CurrentPlayerId ?? gameState.Player1.PlayerId))
            {
                foreach (var expectation in scenario.Expectations)
                {
                    expectation.Assert(context);
                }
            }

            return context;
        }

        private static GameState BuildGameState(ScenarioCase scenario, IServiceProvider services)
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();

            var player1 = new PlayerState(player1Id)
            {
                IsFirst = scenario.Setup.FirstPlayer == ScenarioPlayer.Player1
            };
            var player2 = new PlayerState(player2Id)
            {
                IsFirst = scenario.Setup.FirstPlayer == ScenarioPlayer.Player2
            };

            ApplyPlayerSetup(player1, scenario.Setup.Player1);
            ApplyPlayerSetup(player2, scenario.Setup.Player2);

            var firstPlayerId = scenario.Setup.FirstPlayer == ScenarioPlayer.Player1 ? player1Id : player2Id;
            var gameState = new GameState(scenario.Id ?? "scenario", player1, player2, firstPlayerId)
            {
                CurrentPlayerId = scenario.Setup.ActivePlayer == ScenarioPlayer.Player1 ? player1Id : player2Id,
                Phase = GamePhase.Main,
                Status = GameStatus.InProgress
            };

            var abilityManager = services.GetRequiredService<IAbilityManager>();
            var buffManager = services.GetRequiredService<IBuffManager>();
            gameState.AbilityManager = abilityManager;
            gameState.BuffManager = buffManager;

            player1.Leader = CreateLeader(gameState, player1, scenario.Setup.Player1.LeaderHealth);
            player2.Leader = CreateLeader(gameState, player2, scenario.Setup.Player2.LeaderHealth);

            return gameState;
        }

        private static void ApplyPlayerSetup(PlayerState player, ScenarioPlayerSetup setup)
        {
            player.MaxPlayPoints = setup.MaxPlayPoints;
            player.CurrentPlayPoints = setup.CurrentPlayPoints;
            player.MaxEvolutionPoints = setup.MaxEvolutionPoints;
            player.CurrentEvolutionPoints = setup.CurrentEvolutionPoints;
            player.MaxSuperEvolutionPoints = setup.MaxSuperEvolutionPoints;
            player.CurrentSuperEvolutionPoints = setup.CurrentSuperEvolutionPoints;
            if (setup.GraveyardCount.HasValue)
            {
                player.GraveyardCount = setup.GraveyardCount.Value;
            }
            if (setup.BloodPool.HasValue)
            {
                player.BloodPool = setup.BloodPool.Value;
            }
            player.TurnNumber = setup.TurnNumber;
            player.Combo = setup.PlayedCardCountThisTurn;
            player.IsEvolutionUnlocked = setup.MaxEvolutionPoints > 0 || setup.CanEvolve;
            player.IsSuperEvolutionUnlocked = setup.MaxSuperEvolutionPoints > 0 || setup.CanSuperEvolve;
            player.CanEvolve = setup.CanEvolve;
            player.CanSuperEvolve = setup.CanSuperEvolve;
        }

        private static CardInstance CreateLeader(GameState gameState, PlayerState owner, int? desiredHealth)
        {
            var leader = new CardInstance(Guid.NewGuid(), 0, owner.PlayerId)
            {
                CurrentZone = CardZone.Emblem
            };

            if (desiredHealth.HasValue)
            {
                var max = leader.BaseHealth;
                leader.Damage = Math.Max(0, max - desiredHealth.Value);
            }

            gameState.RegisterCardInstance(leader);
            owner.Emblem.Add(leader);
            owner.Leader = leader;
            return leader;
        }

        private static Dictionary<string, CardInstance> BuildCards(ScenarioCase scenario, GameState gameState)
        {
            var cards = new Dictionary<string, CardInstance>(StringComparer.OrdinalIgnoreCase);

            foreach (var spec in scenario.Setup.Cards)
            {
                if (string.IsNullOrWhiteSpace(spec.Key))
                {
                    throw new InvalidOperationException($"Scenario card spec missing key for card id {spec.CardId}.");
                }

                if (cards.ContainsKey(spec.Key))
                {
                    throw new InvalidOperationException($"Duplicate scenario card key '{spec.Key}'.");
                }

                var owner = spec.Owner == ScenarioPlayer.Player1 ? gameState.Player1 : gameState.Player2;
                var card = new CardInstance(Guid.NewGuid(), spec.CardId, owner.PlayerId)
                {
                    CurrentZone = spec.Zone
                };

                if (spec.Damage.HasValue)
                {
                    card.Damage = spec.Damage.Value;
                }

                if (spec.AttacksThisTurn.HasValue)
                {
                    card.AttacksThisTurn = spec.AttacksThisTurn.Value;
                }

                if (spec.FieldTurn.HasValue)
                {
                    card.FieldTurn = spec.FieldTurn.Value;
                }

                if (spec.IsSuperEvolved == true)
                {
                    card.ApplyEvolutionChange(null!, isSuperEvolution: true);
                }
                else if (spec.IsEvolved.HasValue)
                {
                    card.IsEvolved = spec.IsEvolved.Value;
                }

                if (spec.CountdownOverride.HasValue && card.Countdown.HasValue)
                {
                    var delta = spec.CountdownOverride.Value - card.Countdown.Value;
                    card.ApplyCountdownChange(delta);
                }

                gameState.RegisterCardInstance(card);
                AddToZone(owner, card, spec.Zone);
                cards[spec.Key] = card;
            }

            return cards;
        }

        private static void AddToZone(PlayerState owner, CardInstance card, CardZone zone)
        {
            List<CardInstance> list = zone switch
            {
                CardZone.Hand => owner.Hand,
                CardZone.Field => owner.Field,
                CardZone.Deck => owner.Deck,
                CardZone.Graveyard => owner.Graveyard,
                CardZone.Extra => owner.Extra,
                CardZone.Emblem => owner.Emblem,
                _ => owner.Hand
            };

            list.Add(card);
            card.ZoneIndex = list.Count - 1;
        }

        private static void RegisterAbilities(GameState gameState, ScenarioCase scenario, IReadOnlyDictionary<string, CardInstance> cards, TestGameDataProvider dataProvider)
        {
            foreach (var spec in scenario.Setup.Cards)
            {
                if (!spec.RegisterAbilities)
                {
                    continue;
                }

                var card = cards[spec.Key];
                var configs = dataProvider.GetAbilityConfigs(card.CurrentDefinitionId);
                if (configs.Count == 0)
                {
                    continue;
                }

                gameState.AbilityManager.CreateAbilitiesForCard(card, configs, null, gameState);
            }
        }

        private static void ExecuteAction(ScenarioContext context, GameActionTransactionService transactionService, ScenarioAction action)
        {
            var player = context.GetPlayer(action.Player);
            var initialEvent = action switch
            {
                PlayCardAction play => BuildPlayCardEvent(context, player, play),
                EvolveAction evolve => BuildEvolveEvent(context, player, evolve),
                SuperEvolveAction evolve => BuildSuperEvolveEvent(context, player, evolve),
                ActAction act => BuildActEvent(context, player, act),
                AttackAction attack => BuildAttackEvent(context, player, attack),
                TurnStartAction => BuildTurnStartEvent(player),
                TurnEndAction => BuildTurnEndEvent(player),
                CountdownTickAction tick => BuildCountdownTickEvent(context, tick),
                DestroyAction destroy => BuildDestroyEvent(context, destroy),
                ReturnAction ret => BuildReturnEvent(context, player, ret),
                TuckAction tuck => BuildTuckEvent(context, tuck),
                VanishAction vanish => BuildVanishEvent(context, vanish),
                _ => throw new InvalidOperationException($"Unsupported action type {action.GetType().Name}.")
            };

            using var gameContext = new GameContext(
                context.Services,
                context.Mapper,
                context.ContextAccessor,
                context.GameState,
                player.PlayerId,
                triggeringEvent: initialEvent);

            var result = transactionService.ProcessTransactionAsync(gameContext, new[] { initialEvent }).GetAwaiter().GetResult();
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Scenario action failed: {result.ErrorMessage}");
            }

            context.RecordEvents(result.ProcessedEvents);
        }

        private static IGameEvent BuildPlayCardEvent(ScenarioContext context, PlayerState player, PlayCardAction action)
        {
            var card = context.GetCard(action.CardKey);
            var dto = new PlayCardActionDto
            {
                CardUid = card.InstanceId,
                PlayMode = action.PlayMode,
                PaidCost = action.PaidCost
            };
            return new PlayCardRequestedEvent(player.PlayerId, dto);
        }

        private static IGameEvent BuildEvolveEvent(ScenarioContext context, PlayerState player, EvolveAction action)
        {
            var card = context.GetCard(action.CardKey);
            var dto = new EvolveActionDto
            {
                UnitUid = card.InstanceId.ToString()
            };
            return new EvolveRequestedEvent(player.PlayerId, dto);
        }

        private static IGameEvent BuildSuperEvolveEvent(ScenarioContext context, PlayerState player, SuperEvolveAction action)
        {
            var card = context.GetCard(action.CardKey);
            var dto = new SuperEvolveActionDto
            {
                UnitUid = card.InstanceId.ToString()
            };
            return new SuperEvolveRequestedEvent(player.PlayerId, dto);
        }

        private static IGameEvent BuildActEvent(ScenarioContext context, PlayerState player, ActAction action)
        {
            var card = context.GetCard(action.CardKey);
            var dto = new ActActionDto
            {
                CardUid = card.InstanceId
            };
            return new ActRequestedEvent(player.PlayerId, dto);
        }

        private static IGameEvent BuildAttackEvent(ScenarioContext context, PlayerState player, AttackAction action)
        {
            var attacker = context.GetCard(action.AttackerKey);
            var target = context.GetCard(action.TargetKey);
            var dto = new AttackActionDto
            {
                AttackerUid = attacker.InstanceId,
                TargetUid = target.InstanceId
            };
            return new AttackRequestedEvent(player.PlayerId, dto);
        }

        private static IGameEvent BuildTurnStartEvent(PlayerState player)
        {
            return new TurnStartEvent(player);
        }

        private static IGameEvent BuildTurnEndEvent(PlayerState player)
        {
            return new TurnEndEvent(player);
        }

        private static IGameEvent BuildCountdownTickEvent(ScenarioContext context, CountdownTickAction action)
        {
            var player = context.GetPlayer(action.Player);
            var card = context.GetCard(action.CardKey);
            return new CountdownTickEvent(player, card, action.ChangeAmount);
        }

        private static IGameEvent BuildDestroyEvent(ScenarioContext context, DestroyAction action)
        {
            var card = context.GetCard(action.CardKey);
            return new DestroyEvent(card, card, action.Reason);
        }

        private static IGameEvent BuildReturnEvent(ScenarioContext context, PlayerState player, ReturnAction action)
        {
            var card = context.GetCard(action.CardKey);
            return new ScenarioReturnEvent(player, card, action.Reason);
        }

        private static IGameEvent BuildTuckEvent(ScenarioContext context, TuckAction action)
        {
            var card = context.GetCard(action.CardKey);
            return new TuckEvent(card, action.Reason);
        }

        private static IGameEvent BuildVanishEvent(ScenarioContext context, VanishAction action)
        {
            var card = context.GetCard(action.CardKey);
            return new CardVanishedEvent(card, card.CurrentZone, action.Reason);
        }

        private static int StableSeedFromString(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 1;
            }

            unchecked
            {
                var hash = 17;
                foreach (var ch in value)
                {
                    hash = hash * 31 + ch;
                }
                return hash;
            }
        }
    }
}
