using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public interface IMirageCombatSimulator
    {
        Task<MirageCombatReplayDto> SimulateCombatAsync(
            GameState mainGameState,
            Guid combatId,
            CancellationToken cancellationToken = default);

        Task<MirageCombatReplayDto> SimulateSidesAsync(
            MirageCombatSideSnapshot sideA,
            MirageCombatSideSnapshot sideB,
            Guid combatId,
            int roundNumber,
            CancellationToken cancellationToken = default);
    }

    public sealed class MirageCombatSimulator : IMirageCombatSimulator
    {
        public const int MaxCombatSteps = 100;

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IMirageCombatSnapshotBuilder _snapshotBuilder;
        private readonly IMirageShopService _shopService;
        private readonly IMirageGhostService _ghostService;
        private readonly ILogger<MirageCombatSimulator> _logger;

        public MirageCombatSimulator(
            IServiceScopeFactory serviceScopeFactory,
            IMirageCombatSnapshotBuilder snapshotBuilder,
            IMirageShopService shopService,
            IMirageGhostService ghostService,
            ILogger<MirageCombatSimulator> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
            _shopService = shopService ?? throw new ArgumentNullException(nameof(shopService));
            _ghostService = ghostService ?? throw new ArgumentNullException(nameof(ghostService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MirageCombatReplayDto> SimulateCombatAsync(
            GameState mainGameState,
            Guid combatId,
            CancellationToken cancellationToken = default)
        {
            if (mainGameState == null) throw new ArgumentNullException(nameof(mainGameState));
            var roundNumber = mainGameState.Mirage?.RoundNumber ?? 1;
            var sideA = _snapshotBuilder.CaptureSideSnapshot(mainGameState, mainGameState.Player1.PlayerId);
            if (!mainGameState.Player1.IsAI)
            {
                _ghostService.RecordGhostSnapshot(roundNumber, sideA);
            }

            MirageCombatSideSnapshot sideB;
            bool isSideBGhost = false;
            var boundGhostId = mainGameState.Mirage?.NextCombatGhostId;
            if (boundGhostId.HasValue)
            {
                mainGameState.Mirage!.NextCombatGhostId = null;
                sideB = _ghostService.GetOrCreateGhostCombatSide(
                    mainGameState,
                    mainGameState.Player2,
                    roundNumber,
                    excludeSourcePlayerId: null,
                    specificGhostId: boundGhostId.Value);
                isSideBGhost = true;
            }
            else
            {
                sideB = _snapshotBuilder.CaptureSideSnapshot(mainGameState, mainGameState.Player2.PlayerId);
                if (!mainGameState.Player2.IsAI)
                {
                    _ghostService.RecordGhostSnapshot(roundNumber, sideB);
                }
            }

            var replay = await SimulateSidesAsync(sideA, sideB, combatId, roundNumber, cancellationToken);
            if (isSideBGhost && replay.Outcome.PermanentGrowthLog.Count > 0)
            {
                // Contract 8: Discard the Ghost side's PermanentGrowthLog after combat
                replay = replay with
                {
                    Outcome = replay.Outcome with
                    {
                        PermanentGrowthLog = replay.Outcome.PermanentGrowthLog
                            .Where(entry => entry != null && entry.PlayerId != sideB.PlayerId)
                            .ToList()
                    }
                };
            }

            return replay;
        }

        public async Task<MirageCombatReplayDto> SimulateSidesAsync(
            MirageCombatSideSnapshot sideA,
            MirageCombatSideSnapshot sideB,
            Guid combatId,
            int roundNumber,
            CancellationToken cancellationToken = default)
        {
            if (sideA == null) throw new ArgumentNullException(nameof(sideA));
            if (sideB == null) throw new ArgumentNullException(nameof(sideB));

            await using var sandboxScope = _serviceScopeFactory.CreateAsyncScope();
            var provider = sandboxScope.ServiceProvider;
            var accessor = provider.GetRequiredService<IGameContextAccessor>();

            using var suspendScope = accessor.SuspendAmbient();
            using var guardScope = SandboxExecutionGuard.Enter();

            var sandboxAbilityManager = provider.GetRequiredService<AbilityManager>();
            var sandboxBuffManager = provider.GetRequiredService<BuffManager>();
            var sandboxBuffService = provider.GetRequiredService<IBuffService>();
            var sandboxAuraManager = ActivatorUtilities.CreateInstance<AuraManager>(provider);
            var sandboxHistoryService = new InMemorySandboxEventHistoryService();
            var sandboxTransactionService = provider.GetRequiredService<IGameActionTransactionService>();
            var mapper = provider.GetRequiredService<IMapper>();

            var countA = CountInitialFieldFollowers(sideA);
            var countB = CountInitialFieldFollowers(sideB);
            var firstAttackerPlayerId = countA > countB
                ? sideA.PlayerId
                : countB > countA
                    ? sideB.PlayerId
                    : (Random.Shared.Next(2) == 0 ? sideA.PlayerId : sideB.PlayerId);

            var (sandboxGameState, initialSandboxBuffIds) = _snapshotBuilder.PopulateSandboxState(
                sideA,
                sideB,
                roundNumber,
                firstAttackerPlayerId,
                provider,
                sandboxAbilityManager,
                sandboxBuffManager,
                sandboxAuraManager);

            foreach (var buffId in initialSandboxBuffIds)
            {
                sandboxHistoryService.InitialSandboxBuffIds.Add(buffId);
            }

            using var sandboxContext = GameContext.CreateForSandbox(
                provider,
                mapper,
                accessor,
                sandboxGameState,
                sandboxBuffService,
                sandboxHistoryService,
                firstAttackerPlayerId,
                cancellationToken);

            // Establish initial sandbox auras and settle any generated events (Contract 4.2.3)
            var auraInitResult = MirageCardRetirement.RefreshAllBoardAuras(sandboxContext, "sandbox_init_auras");
            if (!auraInitResult.Success)
            {
                throw new InvalidOperationException($"Sandbox aura initialization failed: {auraInitResult.ErrorMessage}");
            }

            if (auraInitResult.GeneratedEvents.Count > 0)
            {
                var initTx = await sandboxTransactionService.ProcessTransactionAsync(sandboxContext, auraInitResult.GeneratedEvents);
                if (!initTx.IsSuccess)
                {
                    throw new InvalidOperationException($"Sandbox initial aura settlement failed: {initTx.ErrorMessage}");
                }
            }

            sandboxContext.BuffService.RefreshDerivedStateCache(sandboxContext, "sandbox_initial_snapshot");
            var initialStateDto = GameStateProjection.Create(sandboxGameState, sandboxContext);

            sandboxHistoryService.DrainCurrentStepEventDtos();
            sandboxHistoryService.IsCombatStepRecordingEnabled = true;

            var steps = new List<MirageCombatStepDto>();
            var playerA = sandboxGameState.Player1;
            var playerB = sandboxGameState.Player2;
            var actedInPassByPlayer = new Dictionary<Guid, HashSet<Guid>>
            {
                [playerA.PlayerId] = new(),
                [playerB.PlayerId] = new()
            };

            var activeSideId = firstAttackerPlayerId;
            var consecutiveIdleSides = 0;
            var stepIndex = 0;

            while (stepIndex < MaxCombatSteps
                   && HasAnyLivingFollowers(playerA, sandboxContext)
                   && HasAnyLivingFollowers(playerB, sandboxContext)
                   && consecutiveIdleSides < 2)
            {
                var activePlayer = sandboxGameState.GetPlayerState(activeSideId)!;
                var defenderPlayer = sandboxGameState.GetOpponentPlayerState(activeSideId)!;
                var actedInPass = actedInPassByPlayer[activeSideId];

                var attacker = SelectNextAttacker(activePlayer, defenderPlayer, actedInPass, sandboxContext);
                if (attacker == null)
                {
                    consecutiveIdleSides++;
                    activeSideId = defenderPlayer.PlayerId;
                    continue;
                }

                actedInPass.Add(attacker.InstanceId);
                attacker.AttacksThisTurn = 0;

                var performedAtLeastOneAttack = false;
                while (stepIndex < MaxCombatSteps
                       && CanPerformMirageAutoAttack(attacker, sandboxContext)
                       && attacker.AttacksThisTurn < attacker.GetActionUsageLimit(CardActionUsageType.Attack, sandboxContext))
                {
                    var validTargets = GetValidMirageTargets(attacker, defenderPlayer, sandboxContext);
                    if (validTargets.Count == 0)
                    {
                        break;
                    }

                    var target = validTargets[Random.Shared.Next(validTargets.Count)];
                    stepIndex++;
                    performedAtLeastOneAttack = true;
                    consecutiveIdleSides = 0;

                    sandboxGameState.CurrentPlayerId = activePlayer.PlayerId;
                    var declareResult = attacker.PerformAttackDeclared(target);
                    if (!declareResult.Success)
                    {
                        throw new InvalidOperationException($"Sandbox combat step {stepIndex} attack declaration failed: {declareResult.ErrorMessage}");
                    }

                    var stepContext = sandboxContext with { RequestingPlayerId = activePlayer.PlayerId };
                    var txResult = await sandboxTransactionService.ProcessTransactionAsync(stepContext, declareResult.GeneratedEvents);
                    if (!txResult.IsSuccess)
                    {
                        _logger.LogError(
                            "[MirageCombatSimulator] Step {StepIndex} attack transaction failed: {Error}",
                            stepIndex,
                            txResult.ErrorMessage);
                        throw new InvalidOperationException($"Sandbox combat step {stepIndex} transaction failed: {txResult.ErrorMessage}");
                    }

                    var stepEvents = sandboxHistoryService.DrainCurrentStepEventDtos();
                    sandboxContext.BuffService.RefreshDerivedStateCache(sandboxContext, $"sandbox_step_{stepIndex}");
                    steps.Add(new MirageCombatStepDto
                    {
                        StepIndex = stepIndex,
                        AttackerInstanceId = attacker.InstanceId,
                        DefenderInstanceId = target.InstanceId,
                        AttackerOwnerPlayerId = activePlayer.PlayerId,
                        Events = stepEvents,
                        PostStepState = GameStateProjection.Create(sandboxGameState, sandboxContext)
                    });

                    if (!HasAnyLivingFollowers(playerA, sandboxContext)
                        || !HasAnyLivingFollowers(playerB, sandboxContext))
                    {
                        break;
                    }
                }

                if (!performedAtLeastOneAttack)
                {
                    consecutiveIdleSides++;
                }

                activeSideId = defenderPlayer.PlayerId;
            }

            var orderedGrowth = sandboxHistoryService.GetOrderedPermanentGrowthLog();
            var outcome = ComputeOutcome(playerA, playerB, sandboxContext, orderedGrowth);
            return new MirageCombatReplayDto
            {
                CombatId = combatId,
                RoundNumber = Math.Max(1, roundNumber),
                FirstAttackerPlayerId = firstAttackerPlayerId,
                InitialState = initialStateDto,
                Steps = steps,
                Outcome = outcome
            };
        }

        public static bool CanPerformMirageAutoAttack(CardInstance? card, GameContext context)
        {
            if (card == null
                || card.CurrentZone != CardZone.Field
                || !card.IsFollower
                || card.IsLeader
                || card.IsCombatSandboxLeader)
            {
                return false;
            }

            var currentHealth = card.GetMaxHealth(context) - card.Damage;
            if (currentHealth <= 0)
            {
                return false;
            }

            if (card.GetCurrentAttack(context) <= 0)
            {
                return false;
            }

            var keywords = card.GetKeywords(context);
            if (keywords.Contains(CardKeyword.Shackle) || keywords.Contains(CardKeyword.CannotAttackFollower))
            {
                return false;
            }

            return card.GetActionUsageLimit(CardActionUsageType.Attack, context) > 0;
        }

        public static List<CardInstance> GetValidMirageTargets(
            CardInstance attacker,
            PlayerState defenderPlayer,
            GameContext context)
        {
            if (attacker == null || defenderPlayer == null)
            {
                return new List<CardInstance>();
            }

            var candidates = defenderPlayer.Field
                .Where(t => t != null
                    && t.CurrentZone == CardZone.Field
                    && t.IsFollower
                    && !t.IsLeader
                    && !t.IsCombatSandboxLeader
                    && (t.GetMaxHealth(context) - t.Damage) > 0)
                .Where(t =>
                {
                    var kw = t.GetKeywords(context);
                    return !kw.Contains(CardKeyword.Ambush) && !kw.Contains(CardKeyword.Pressure);
                })
                .ToList();

            if (candidates.Count == 0)
            {
                return candidates;
            }

            var attackerKeywords = attacker.GetKeywords(context);
            if (!attackerKeywords.Contains(CardKeyword.IgnoreWard))
            {
                var wardCandidates = candidates
                    .Where(t => t.GetKeywords(context).Contains(CardKeyword.Ward))
                    .ToList();
                if (wardCandidates.Count > 0)
                {
                    return wardCandidates;
                }
            }

            return candidates;
        }

        private static CardInstance? SelectNextAttacker(
            PlayerState activePlayer,
            PlayerState defenderPlayer,
            HashSet<Guid> actedInPass,
            GameContext context)
        {
            var eligibleAttackers = activePlayer.Field
                .Where(c => CanPerformMirageAutoAttack(c, context)
                            && GetValidMirageTargets(c, defenderPlayer, context).Count > 0)
                .ToList();

            if (eligibleAttackers.Count == 0)
            {
                return null;
            }

            var unacted = eligibleAttackers.Where(c => !actedInPass.Contains(c.InstanceId)).ToList();
            if (unacted.Count > 0)
            {
                return unacted[0];
            }

            actedInPass.Clear();
            return eligibleAttackers[0];
        }

        private MirageCombatOutcomeDto ComputeOutcome(
            PlayerState playerA,
            PlayerState playerB,
            GameContext context,
            List<MiragePermanentGrowthEntry> permanentGrowthLog)
        {
            var livingA = GetLivingFollowers(playerA, context);
            var livingB = GetLivingFollowers(playerB, context);

            if ((livingA.Count > 0 && livingB.Count > 0) || (livingA.Count == 0 && livingB.Count == 0))
            {
                return new MirageCombatOutcomeDto
                {
                    WinnerPlayerId = null,
                    LoserPlayerId = null,
                    LeaderDamage = 0,
                    PermanentGrowthLog = permanentGrowthLog
                };
            }

            if (livingA.Count > 0)
            {
                var damage = Math.Max(1, playerA.Mirage?.TavernTier ?? 1)
                    + livingA.Sum(f => ResolveFollowerStarTier(f));
                return new MirageCombatOutcomeDto
                {
                    WinnerPlayerId = playerA.PlayerId,
                    LoserPlayerId = playerB.PlayerId,
                    LeaderDamage = damage,
                    PermanentGrowthLog = permanentGrowthLog
                };
            }
            else
            {
                var damage = Math.Max(1, playerB.Mirage?.TavernTier ?? 1)
                    + livingB.Sum(f => ResolveFollowerStarTier(f));
                return new MirageCombatOutcomeDto
                {
                    WinnerPlayerId = playerB.PlayerId,
                    LoserPlayerId = playerA.PlayerId,
                    LeaderDamage = damage,
                    PermanentGrowthLog = permanentGrowthLog
                };
            }
        }

        private int ResolveFollowerStarTier(CardInstance card)
        {
            var def = GameDataProvider.Instance?.GetCardDefinition(card.CurrentDefinitionId)
                ?? GameDataProvider.Instance?.GetCardDefinition(card.BaseDefinitionId)
                ?? card.CurrentDefinition;
            if (def != null)
            {
                return Math.Max(1, def.Cost);
            }

            var entry = _shopService.GetCatalogEntry(card.BaseDefinitionId);
            return entry != null ? Math.Max(1, entry.TavernTier) : 1;
        }

        private static bool HasAnyLivingFollowers(PlayerState player, GameContext context)
        {
            return GetLivingFollowers(player, context).Count > 0;
        }

        private static List<CardInstance> GetLivingFollowers(PlayerState player, GameContext context)
        {
            return player.Field
                .Where(c => c != null
                    && c.CurrentZone == CardZone.Field
                    && c.IsFollower
                    && !c.IsLeader
                    && !c.IsCombatSandboxLeader
                    && (c.GetMaxHealth(context) - c.Damage) > 0)
                .ToList();
        }

        private static int CountInitialFieldFollowers(MirageCombatSideSnapshot side)
        {
            if (!side.ZoneCardIds.TryGetValue("Field", out var fieldIds) || fieldIds.Count == 0)
            {
                return 0;
            }
            var cardById = side.Cards.ToDictionary(c => c.OriginalInstanceId);
            var count = 0;
            foreach (var id in fieldIds)
            {
                if (cardById.TryGetValue(id, out var card) && !card.IsLeader)
                {
                    var def = GameDataProvider.Instance?.GetCardDefinition(card.BaseDefinitionId);
                    if (def?.Type == CardType.Follower)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
