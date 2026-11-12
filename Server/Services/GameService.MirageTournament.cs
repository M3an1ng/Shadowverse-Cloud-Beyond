using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed record MirageTournamentFreezePayload(int ExpectedRoundNumber, Guid CombatId);
    public sealed record MirageTournamentForfeitTerminatePayload(int FinalPlacement, string Reason);

    public partial class GameService
    {
        public async Task<GameState?> CreateMirageTournamentSeatGameAsync(
            string seatGameId,
            string tournamentRoomId,
            int seatIndex,
            Guid playerId,
            bool isAi,
            string displayName,
            string avatarUrl,
            int recruitTimeoutSeconds,
            DateTime roundStartedAtUtc,
            DateTime roundEndsAtUtc,
            MirageMatchState sharedPoolSource,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(seatGameId)
                || string.IsNullOrWhiteSpace(tournamentRoomId)
                || playerId == Guid.Empty
                || sharedPoolSource == null)
            {
                return null;
            }

            if (_gameStates.TryGetValue(seatGameId, out var existing))
            {
                return existing;
            }

            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                var cardFactory = sp.GetRequiredService<ICardFactory>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
                var triggerOrderingPolicy = sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Abilities.Ordering.ITriggerOrderingPolicy>();
                var systemAbilities = sp.GetServices<IAbility>();

                var proxyPlayerId = Guid.NewGuid();
                var player1State = new PlayerState(playerId)
                {
                    IsFirst = true,
                    IsAI = isAi,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? (isAi ? "幻境AI" : "PLAYER") : displayName.Trim(),
                    AvatarUrl = avatarUrl ?? string.Empty,
                    Class = CardClass.Neutral,
                    Environment = DeckEnvironment.Open,
                    Rule = DeckRule.Standard,
                    IsTakeTwo = false,
                    HasCompletedMulligan = true,
                    HasTakenFirstTurn = true,
                    TurnNumber = 1,
                    PpBoostUsesAvailable = 0
                };

                // Minimal inert opponent shell: no shop, no candidate cards, no real abilities/buffs.
                var player2State = new PlayerState(proxyPlayerId)
                {
                    IsFirst = false,
                    IsAI = false,
                    IsMirageOpponentProxy = true,
                    DisplayName = "对手预览",
                    AvatarUrl = string.Empty,
                    Class = CardClass.Neutral,
                    Environment = DeckEnvironment.Open,
                    Rule = DeckRule.Standard,
                    IsTakeTwo = false,
                    HasCompletedMulligan = true,
                    HasTakenFirstTurn = true,
                    TurnNumber = 1,
                    PpBoostUsesAvailable = 0
                };

                var newGameState = new GameState(seatGameId, player1State, player2State, playerId)
                {
                    Status = GameStatus.InProgress,
                    Phase = GamePhase.Recruit,
                    Mode = GameMode.Mirage,
                    TurnTimeoutSeconds = recruitTimeoutSeconds,
                    TurnStartedAtUtc = roundStartedAtUtc,
                    TurnEndsAtUtc = roundEndsAtUtc
                };

                var abilityManager = new AbilityManager(loggerFactory.CreateLogger<AbilityManager>(), abilityFactory, systemAbilities, triggerOrderingPolicy);
                var buffManager = new BuffManager(loggerFactory.CreateLogger<BuffManager>(), abilityManager);
                var auraManager = new AuraManager(
                    loggerFactory.CreateLogger<AuraManager>(),
                    sp.GetRequiredService<IValueSourceResolver>(),
                    sp.GetRequiredService<IBuffService>(),
                    sp.GetRequiredService<IBuffFactory>());

                newGameState.AbilityManager = abilityManager;
                newGameState.BuffManager = buffManager;
                newGameState.AuraManager = auraManager;

                var seatMirageState = new MirageMatchState
                {
                    TournamentRoomId = tournamentRoomId,
                    TournamentSeatIndex = seatIndex,
                    RoundNumber = 1,
                    SharedPoolSyncRoot = sharedPoolSource.SharedPoolSyncRoot,
                    SharedCardPool = sharedPoolSource.SharedCardPool,
                    InitialCardPoolCaps = sharedPoolSource.InitialCardPoolCaps,
                    ReturnedEliminatedPlayerIds = sharedPoolSource.ReturnedEliminatedPlayerIds,
                    Rules = new MirageRuleConfig
                    {
                        HardGoldCap = sharedPoolSource.Rules.HardGoldCap,
                        RecruitTimeoutSeconds = recruitTimeoutSeconds,
                        BuyCost = sharedPoolSource.Rules.BuyCost,
                        RefreshCost = sharedPoolSource.Rules.RefreshCost,
                        SellRefund = sharedPoolSource.Rules.SellRefund,
                        MaxTavernTier = sharedPoolSource.Rules.MaxTavernTier,
                        MaxCombatSteps = sharedPoolSource.Rules.MaxCombatSteps,
                        BaseUpgradeCostByTargetTier = new Dictionary<int, int>(sharedPoolSource.Rules.BaseUpgradeCostByTargetTier),
                        InitialCopiesByTier = new Dictionary<int, int>(sharedPoolSource.Rules.InitialCopiesByTier)
                    }
                };
                newGameState.Mirage = seatMirageState;

                var leaderCandidates = (_gameDataProvider as IEnvironmentScopedGameDataProvider)?
                    .GetMirageLeaderDefinitions()
                    .Select(c => c.Id)
                    .Distinct()
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(3)
                    .ToList() ?? new List<int>();

                player1State.Mirage = new MiragePlayerState
                {
                    TavernTier = 1,
                    UpgradeCost = seatMirageState.Rules.GetBaseUpgradeCostForNextTier(1),
                    HardGoldCap = seatMirageState.Rules.HardGoldCap,
                    LeaderCandidateCardIds = leaderCandidates,
                    HasSelectedLeader = leaderCandidates.Count == 0
                };

                var round1Gold = seatMirageState.Rules.GetRoundBaseGold(1);
                player1State.MaxPlayPoints = round1Gold;
                player1State.CurrentPlayPoints = round1Gold;

                var shopService = sp.GetRequiredService<IMirageShopService>();
                shopService.RefreshShopForNewRound(player1State, seatMirageState);

                player1State.Leader = cardFactory.CreateAndRegisterNewCard(
                    newGameState,
                    GameConstants.Cards.MirageLeaderCardId,
                    playerId,
                    CardZone.Field);
                player2State.Leader = cardFactory.CreateAndRegisterNewCard(
                    newGameState,
                    GameConstants.Cards.MirageLeaderCardId,
                    proxyPlayerId,
                    CardZone.Field);

                if (!isAi)
                {
                    await ApplyPlayerIdentityAsync(sp, player1State);
                }

                if (_gameStates.TryAdd(seatGameId, newGameState))
                {
                    return newGameState;
                }

                return _gameStates.TryGetValue(seatGameId, out var raced) ? raced : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Failed to create Mirage tournament seat game {SeatGameId}.", seatGameId);
                return null;
            }
        }

        public void RetireMirageTournamentSeatGame(string seatGameId)
        {
            if (string.IsNullOrWhiteSpace(seatGameId))
            {
                return;
            }

            RetireDetachedGame(seatGameId);
        }
    }
}
