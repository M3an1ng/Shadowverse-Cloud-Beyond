using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata(
        "幻境战果提交",
        "当幻境模式自动战斗模拟完成并向主局提交战果与轮次推进时触发。",
        EventType.MirageCombatResultCommitted,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public sealed class MirageCombatResultCommittedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        private const int MaxStagedBuffEventDrainIterations = 512;

        [GameEventParameter("战斗ID", "本次结算的幻境战斗唯一ID。")]
        public Guid CombatId { get; }

        [GameEventParameter("轮次编号", "本次战斗所属的幻境轮次编号。")]
        public int RoundNumber { get; }

        [GameEventParameter("交战步数", "本次战斗模拟执行的总步数。")]
        public int StepCount { get; }

        public MirageCombatOutcomeDto Outcome { get; }

        public bool WasNoOpIdempotent { get; private set; }
        public bool IsGameOverResult { get; private set; }
        public int? AdvancedToRoundNumber { get; private set; }
        public Guid WinnerPlayerId { get; private set; }
        public Guid LoserPlayerId { get; private set; }
        public string? EndReason { get; private set; }

        public MirageCombatResultCommittedEvent(
            Guid combatId,
            int roundNumber,
            MirageCombatOutcomeDto outcome,
            int stepCount = 0,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.MirageCombatResultCommitted, eventId, timestamp)
        {
            CombatId = combatId;
            RoundNumber = roundNumber;
            StepCount = Math.Max(0, stepCount);
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var gameState = context?.CurrentGameState;
            if (gameState == null || gameState.Mode != GameMode.Mirage || gameState.Mirage == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "MirageCombatResultCommittedEvent is only valid in Mirage mode."
                };
            }

            if (gameState.IsMirageTournamentSeat)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "MirageCombatResultCommittedEvent is forbidden on 8-player Mirage tournament seats."
                };
            }

            if (CombatId == Guid.Empty)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "CombatId cannot be empty."
                };
            }

            var mirage = gameState.Mirage;

            // =========================================================================
            // PHASE A: Zero-Mutation Pre-Validate, Pre-Build & Staged Rule Evaluation
            // =========================================================================
            lock (mirage.RoundSyncLock)
            {
                if (mirage.CommittedCombatIds.Contains(CombatId))
                {
                    WasNoOpIdempotent = true;
                    return new DomainOperationResult { Success = true };
                }

                if (gameState.IsGameOver)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Cannot commit Mirage combat result because the game is already over."
                    };
                }

                if (gameState.Phase != GamePhase.AutoCombat)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Mirage combat result can only be committed during AutoCombat phase (Current: {gameState.Phase})."
                    };
                }

                if (mirage.ActiveCombatId != CombatId)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"ActiveCombatId mismatch: expected {mirage.ActiveCombatId}, actual {CombatId}."
                    };
                }

                if (mirage.RoundNumber != RoundNumber)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"RoundNumber mismatch: expected {mirage.RoundNumber}, actual {RoundNumber}."
                    };
                }
            }

            if (Outcome == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Combat outcome payload is required."
                };
            }

            if (Outcome.LeaderDamage < 0)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Combat outcome LeaderDamage cannot be negative."
                };
            }

            PlayerState? winnerPlayer = null;
            PlayerState? loserPlayer = null;
            if (Outcome.IsTie)
            {
                if (Outcome.LeaderDamage != 0 || Outcome.WinnerPlayerId.HasValue || Outcome.LoserPlayerId.HasValue)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Tie combat outcome cannot specify WinnerPlayerId, LoserPlayerId, or non-zero LeaderDamage."
                    };
                }
            }
            else
            {
                if (!Outcome.WinnerPlayerId.HasValue
                    || !Outcome.LoserPlayerId.HasValue
                    || Outcome.WinnerPlayerId.Value == Outcome.LoserPlayerId.Value)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Non-tie combat outcome must specify distinct WinnerPlayerId and LoserPlayerId."
                    };
                }

                winnerPlayer = gameState.GetPlayerState(Outcome.WinnerPlayerId.Value);
                loserPlayer = gameState.GetPlayerState(Outcome.LoserPlayerId.Value);
                if (winnerPlayer == null || loserPlayer == null)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Winner or loser player was not found in the current match."
                    };
                }

                if (loserPlayer.Leader == null)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Loser player {loserPlayer.PlayerId} has no Leader card."
                    };
                }
            }

            var preBuiltPermanentBuffs = new List<(Guid TargetCardInstanceId, Buff BuffInstance)>();
            if (Outcome.PermanentGrowthLog != null)
            {
                foreach (var entry in Outcome.PermanentGrowthLog)
                {
                    if (entry == null)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = "PermanentGrowthLog contains a null entry."
                        };
                    }

                    if (entry.OriginInstanceId == Guid.Empty)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = "PermanentGrowthLog entry has an empty OriginInstanceId."
                        };
                    }

                    var owner = gameState.GetPlayerState(entry.PlayerId);
                    if (owner == null)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = $"Player {entry.PlayerId} referenced in PermanentGrowthLog was not found."
                        };
                    }

                    // Locate permanent card in Field or Hand; skip if retired/sold
                    var targetCard = owner.Field.FirstOrDefault(c => c != null && c.InstanceId == entry.OriginInstanceId)
                        ?? owner.Hand.FirstOrDefault(c => c != null && c.InstanceId == entry.OriginInstanceId);
                    if (targetCard == null || targetCard.CurrentZone == CardZone.Retired)
                    {
                        continue;
                    }

                    Buff restoredBuff;
                    try
                    {
                        var buffNode = JsonNode.Parse(entry.SerializedBuff.GetRawText()) as JsonObject;
                        if (buffNode == null)
                        {
                            return new DomainOperationResult
                            {
                                Success = false,
                                ErrorMessage = $"SerializedBuff for card {entry.OriginInstanceId} is not a JSON object."
                            };
                        }

                        buffNode["id"] = Guid.NewGuid();
                        buffNode["targetCardInstanceId"] = targetCard.InstanceId;
                        buffNode["sourceCardInstanceId"] = targetCard.InstanceId;
                        buffNode["sourceAbilityId"] = Guid.Empty;
                        buffNode["summaryRootAbilityId"] = null;
                        buffNode["isActive"] = true;
                        var remappedBuffElement = JsonSerializer.SerializeToElement(buffNode, ReplayExecutionState.Json);
                        restoredBuff = ReplayExecutionState.RestoreBuff(remappedBuffElement);
                    }
                    catch (Exception ex)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = $"Failed to deserialize permanent growth Buff for card {entry.OriginInstanceId}: {ex.Message}"
                        };
                    }

                    if (restoredBuff == null
                        || restoredBuff.Effects == null
                        || restoredBuff.Effects.Count == 0)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = $"Invalid Buff or empty Effects in PermanentGrowthLog for card {entry.OriginInstanceId}."
                        };
                    }

                    if (restoredBuff.DurationType != DurationType.Permanent || restoredBuff.IsAuraGenerated)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = $"PermanentGrowthLog entry for card {entry.OriginInstanceId} must be a non-aura Permanent buff."
                        };
                    }

                    restoredBuff.IsAuraGenerated = false;
                    preBuiltPermanentBuffs.Add((targetCard.InstanceId, restoredBuff));
                }
            }

            if (gameState.Player1 == null || gameState.Player2 == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Both players must be present in GameState."
                };
            }

            if (context!.AbilityManager is not AbilityManager liveAbilityManager
                || context.BuffManager is not BuffManager liveBuffManager
                || context.AuraManager is not AuraManager liveAuraManager
                || context.BuffService is not BuffService liveBuffService)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Mirage combat commit requires concrete AbilityManager, BuffManager, AuraManager, and BuffService."
                };
            }

            var scopeFactory = context.ServiceProvider.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "IServiceScopeFactory is unavailable for staged Mirage combat commit."
                };
            }

            var liveCardsById = CollectAllCards(gameState);

            using var stagedScope = scopeFactory.CreateScope();
            var stagedProvider = stagedScope.ServiceProvider;

            GameState stagedGameState;
            Dictionary<Guid, CardInstance> stagedCardsById;
            AbilityManager stagedAbilityManager;
            BuffManager stagedBuffManager;
            AuraManager stagedAuraManager;
            BuffService stagedBuffService;
            bool stagedIsGameOver = false;
            Guid stagedWinnerId = Guid.Empty;
            Guid stagedLoserId = Guid.Empty;
            string? stagedEndReason = null;
            int? stagedAdvancedToRound = null;

            try
            {
                var accessor = stagedProvider.GetRequiredService<IGameContextAccessor>();
                using var suspendScope = accessor.SuspendAmbient();
                using var guardScope = SandboxExecutionGuard.Enter();

                stagedAbilityManager = stagedProvider.GetRequiredService<AbilityManager>();
                stagedBuffManager = stagedProvider.GetRequiredService<BuffManager>();
                stagedAuraManager = ActivatorUtilities.CreateInstance<AuraManager>(stagedProvider);
                stagedBuffService = (BuffService)stagedProvider.GetRequiredService<IBuffService>();

                stagedGameState = BuildStagedGameState(
                    gameState,
                    liveCardsById,
                    stagedProvider,
                    stagedAbilityManager,
                    stagedBuffManager,
                    stagedAuraManager,
                    liveAbilityManager,
                    liveBuffManager,
                    liveAuraManager,
                    out stagedCardsById);

                using var stagedContext = GameContext.CreateForSandbox(
                    stagedProvider,
                    context.Mapper,
                    accessor,
                    stagedGameState,
                    stagedBuffService,
                    new ReadOnlyStagedEventHistoryService(context.EventHistoryService),
                    (winnerPlayer ?? gameState.Player1).PlayerId,
                    context.CancellationToken);

                stagedBuffService.RefreshDerivedStateCache(stagedContext, "mirage_combat_commit_staged_pre");

                // 1. Apply permanent growth buffs on staged state via canonical BuffAppliedEvent.Commit
                //    (which handles PreserveCurrentHealthOnApply, AbilityBuffEffect creation, tribe markers,
                //    AND AbilitySuppressionBuffEffect aura unregistration).
                foreach (var (targetCardId, buff) in preBuiltPermanentBuffs)
                {
                    if (!stagedCardsById.TryGetValue(targetCardId, out var stagedTargetCard)
                        || stagedTargetCard.CurrentZone == CardZone.Retired)
                    {
                        continue;
                    }

                    var buffApplied = new BuffAppliedEvent(buff, stagedTargetCard, stagedTargetCard);
                    if (!TryDrainStagedBuffEvents(new IGameEvent[] { buffApplied }, stagedContext, out var buffError))
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = buffError ?? $"Failed to apply permanent growth Buff {buff.Id} on card {targetCardId}."
                        };
                    }
                }

                // 2. Apply leader damage on staged state
                var stagedWinner = winnerPlayer != null ? stagedGameState.GetPlayerState(winnerPlayer.PlayerId) : null;
                var stagedLoser = loserPlayer != null ? stagedGameState.GetPlayerState(loserPlayer.PlayerId) : null;

                if (Outcome.LeaderDamage > 0 && stagedLoser?.Leader != null)
                {
                    stagedLoser.Leader.Damage += Outcome.LeaderDamage;
                    stagedBuffService.InvalidateDerivedState(stagedGameState, stagedLoser.Leader.InstanceId);
                }

                var remainingHealth = stagedLoser?.Leader != null
                    ? stagedBuffService.GetEffectiveMaxHealth(stagedLoser.Leader, stagedContext) - stagedLoser.Leader.Damage
                    : 1;

                // 3. Branch on lethal vs next round advance on staged state
                var shopService = context.ServiceProvider.GetService<IMirageShopService>();
                if (remainingHealth <= 0 && stagedWinner != null && stagedLoser != null)
                {
                    stagedIsGameOver = true;
                    stagedWinnerId = stagedWinner.PlayerId;
                    stagedLoserId = stagedLoser.PlayerId;
                    stagedEndReason = $"幻境战斗结算：主战者受到 {Outcome.LeaderDamage} 点伤害，生命值归零";

                    shopService?.ReturnEliminatedPlayerCardsToPool(stagedGameState, stagedLoser.PlayerId);

                    stagedGameState.Status = GameStatus.Finished;
                    stagedGameState.Phase = GamePhase.GameOver;
                    stagedGameState.WinnerId = stagedWinnerId;
                    stagedGameState.GameMessage = stagedEndReason;
                    stagedGameState.TurnStartedAtUtc = null;
                    stagedGameState.TurnEndsAtUtc = null;
                }
                else
                {
                    if (stagedGameState.Player1?.Mirage == null || stagedGameState.Player2?.Mirage == null)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = "Both players must have initialized MiragePlayerState to advance round."
                        };
                    }

                    if (shopService == null)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = "IMirageShopService is unavailable to pre-build next round shop offers."
                        };
                    }

                    var nextRound = RoundNumber + 1;
                    stagedAdvancedToRound = nextRound;

                    stagedGameState.Mirage!.RoundNumber = nextRound;
                    stagedGameState.Mirage.ClosingRecruitRoundNumber = null;
                    stagedGameState.Phase = GamePhase.Recruit;
                    stagedGameState.Mirage.ReadyPlayerIds.Clear();

                    var now = DateTime.UtcNow;
                    var timeoutSeconds = stagedGameState.TurnTimeoutSeconds > 0
                        ? stagedGameState.TurnTimeoutSeconds
                        : stagedGameState.Mirage.Rules.RecruitTimeoutSeconds;
                    stagedGameState.TurnTimeoutSeconds = timeoutSeconds;
                    stagedGameState.TurnStartedAtUtc = now;
                    stagedGameState.TurnEndsAtUtc = now.AddSeconds(timeoutSeconds);

                    shopService.RefreshShopForNewRound(stagedGameState.Player1, stagedGameState.Mirage);
                    shopService.RefreshShopForNewRound(stagedGameState.Player2, stagedGameState.Mirage);

                    var roundGold = stagedGameState.Mirage.Rules.GetRoundBaseGold(nextRound);
                    var p1Reset = stagedGameState.Player1.ResetForMirageRoundStart(stagedContext, roundGold, decrementUpgradeCost: true);
                    if (!p1Reset.Success)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = p1Reset.ErrorMessage ?? "Player1 round start reset failed."
                        };
                    }
                    if (!TryDrainStagedBuffEvents(p1Reset.GeneratedEvents, stagedContext, out var p1DrainError))
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = p1DrainError ?? "Player1 round start buff expiry failed."
                        };
                    }

                    var p2Reset = stagedGameState.Player2.ResetForMirageRoundStart(stagedContext, roundGold, decrementUpgradeCost: true);
                    if (!p2Reset.Success)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = p2Reset.ErrorMessage ?? "Player2 round start reset failed."
                        };
                    }
                    if (!TryDrainStagedBuffEvents(p2Reset.GeneratedEvents, stagedContext, out var p2DrainError))
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = p2DrainError ?? "Player2 round start buff expiry failed."
                        };
                    }

                    var auraRefresh = MirageCardRetirement.RefreshAllBoardAuras(stagedContext, "mirage_combat_result_committed_auras");
                    if (!auraRefresh.Success)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = auraRefresh.ErrorMessage ?? "Mirage board aura refresh failed."
                        };
                    }
                    if (!TryDrainStagedBuffEvents(auraRefresh.GeneratedEvents, stagedContext, out var auraDrainError))
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = auraDrainError ?? "Mirage board aura buff application failed."
                        };
                    }
                }

                // 4. Finalize derived-state cache on staged state inside Phase A
                stagedBuffService.RefreshDerivedStateCache(stagedContext, "mirage_combat_result_committed_final");
            }
            catch (Exception ex)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Staged Mirage combat commit pre-evaluation failed: {ex.Message}"
                };
            }

            // =========================================================================
            // PHASE B: Infallible In-Memory Commit (Only mounts pre-computed state)
            // =========================================================================
            Source = TargetInfo.ForPlayer(winnerPlayer ?? gameState.Player1);
            IsGameOverResult = stagedIsGameOver;
            WinnerPlayerId = stagedWinnerId;
            LoserPlayerId = stagedLoserId;
            EndReason = stagedEndReason;
            AdvancedToRoundNumber = stagedAdvancedToRound;

            // 1. Mount any newly created card instances and sync all existing card runtime states in-place
            foreach (var (cardId, stagedCard) in stagedCardsById)
            {
                if (!liveCardsById.TryGetValue(cardId, out var liveCard))
                {
                    liveCard = new CardInstance(stagedCard.InstanceId, stagedCard.BaseDefinitionId, stagedCard.OwnerPlayerId);
                    liveCardsById[cardId] = liveCard;
                }

                var snap = stagedCard.CaptureRuntimeStateSnapshot();
                var remappedFused = snap.FusedCards
                    .Where(c => c != null && liveCardsById.ContainsKey(c.InstanceId))
                    .Select(c => liveCardsById[c.InstanceId])
                    .ToList();
                liveCard.RestoreResumeState(
                    snap with { FusedCards = remappedFused },
                    stagedCard.HealthFloorFromBuffRemoval,
                    stagedCard.CombatContinuityVersion);
                liveCard.CurrentZone = stagedCard.CurrentZone;
                liveCard.ZoneIndex = stagedCard.ZoneIndex;
                liveCard.SetLastMoveReason(stagedCard.LastMoveReason);

                if (stagedGameState.FindCardInstance(cardId) != null)
                {
                    gameState.RegisterCardInstance(liveCard);
                }
            }

            // 2. Sync Player1 and Player2 states in-place
            ApplyStagedPlayerStateInMemory(gameState.Player1, stagedGameState.Player1, liveCardsById);
            ApplyStagedPlayerStateInMemory(gameState.Player2, stagedGameState.Player2, liveCardsById);

            // 3. Replace manager states and pre-computed derived state cache in-memory
            liveAbilityManager.ReplaceStateFrom(stagedAbilityManager, liveCardsById);
            liveBuffManager.ReplaceStateFrom(stagedBuffManager);
            liveAuraManager.ReplaceStateFrom(stagedAuraManager);
            gameState.InGameIds.ReplaceStateFrom(stagedGameState.InGameIds);
            gameState.RestoreRevisions(
                stagedGameState.StateRevision,
                stagedGameState.BuffRevision,
                stagedGameState.RefreshCycleId);
            liveBuffService.ReplaceDerivedStateCacheFrom(gameState.GameId, stagedBuffService);

            // 4. Mount GameState scalar fields
            gameState.Status = stagedGameState.Status;
            gameState.Phase = stagedGameState.Phase;
            gameState.WinnerId = stagedGameState.WinnerId;
            gameState.GameMessage = stagedGameState.GameMessage;
            gameState.TurnTimeoutSeconds = stagedGameState.TurnTimeoutSeconds;
            gameState.TurnStartedAtUtc = stagedGameState.TurnStartedAtUtc;
            gameState.TurnEndsAtUtc = stagedGameState.TurnEndsAtUtc;

            // 5. Final atomic commit registration (must be the last operation in Phase B)
            lock (mirage.RoundSyncLock)
            {
                if (stagedAdvancedToRound.HasValue)
                {
                    mirage.RoundNumber = stagedAdvancedToRound.Value;
                    mirage.ClosingRecruitRoundNumber = null;
                    mirage.ReadyPlayerIds.Clear();
                }

                if (stagedGameState.Mirage != null)
                {
                    mirage.SharedCardPool = new Dictionary<int, int>(stagedGameState.Mirage.SharedCardPool);
                    mirage.InitialCardPoolCaps = new Dictionary<int, int>(stagedGameState.Mirage.InitialCardPoolCaps);
                    mirage.ReturnedEliminatedPlayerIds = new HashSet<Guid>(stagedGameState.Mirage.ReturnedEliminatedPlayerIds);
                }

                mirage.LastCombatId = CombatId;
                mirage.LastCombatRoundNumber = RoundNumber;
                mirage.LastCombatWinnerPlayerId = Outcome.WinnerPlayerId;
                mirage.LastCombatLoserPlayerId = Outcome.LoserPlayerId;
                mirage.LastCombatLeaderDamage = Outcome.LeaderDamage;
                mirage.LastCombatStepCount = StepCount;
                mirage.CommittedCombatIds.Add(CombatId);
                mirage.ActiveCombatId = null;
            }

            return new DomainOperationResult { Success = true };
        }

        internal static Dictionary<Guid, CardInstance> CollectAllCards(GameState gameState)
        {
            var cards = new Dictionary<Guid, CardInstance>();
            void Visit(CardInstance? card)
            {
                if (card == null || card.InstanceId == Guid.Empty || !cards.TryAdd(card.InstanceId, card))
                {
                    return;
                }

                foreach (var fused in card.FusedCards)
                {
                    Visit(fused);
                }
            }

            foreach (var card in gameState.GetRegisteredCardInstances()
                         .Concat(gameState.AllCards)
                         .Concat(gameState.Player1.InitialDeck)
                         .Concat(gameState.Player2.InitialDeck))
            {
                Visit(card);
            }

            return cards;
        }

        internal static GameState BuildStagedGameState(
            GameState liveGame,
            IReadOnlyDictionary<Guid, CardInstance> liveCardsById,
            IServiceProvider stagedServices,
            AbilityManager stagedAbilityManager,
            BuffManager stagedBuffManager,
            AuraManager stagedAuraManager,
            AbilityManager liveAbilityManager,
            BuffManager liveBuffManager,
            AuraManager liveAuraManager,
            out Dictionary<Guid, CardInstance> stagedCardsById)
        {
            stagedCardsById = new Dictionary<Guid, CardInstance>(liveCardsById.Count);
            foreach (var (cardId, liveCard) in liveCardsById)
            {
                var stagedCard = new CardInstance(liveCard.InstanceId, liveCard.BaseDefinitionId, liveCard.OwnerPlayerId)
                {
                    CurrentZone = liveCard.CurrentZone,
                    ZoneIndex = liveCard.ZoneIndex
                };
                var rawSnap = liveCard.CaptureRuntimeStateSnapshot() with { FusedCards = [] };
                stagedCard.RestoreResumeState(rawSnap, liveCard.HealthFloorFromBuffRemoval, liveCard.CombatContinuityVersion);
                stagedCard.SetLastMoveReason(liveCard.LastMoveReason);
                stagedCardsById[cardId] = stagedCard;
            }

            foreach (var (cardId, liveCard) in liveCardsById)
            {
                var stagedCard = stagedCardsById[cardId];
                foreach (var fused in liveCard.FusedCards)
                {
                    if (fused != null && stagedCardsById.TryGetValue(fused.InstanceId, out var stagedFused))
                    {
                        stagedCard.AddFusedCard(stagedFused);
                    }
                }
            }

            var stagedP1 = CloneStagedPlayerState(liveGame.Player1, stagedCardsById);
            var stagedP2 = CloneStagedPlayerState(liveGame.Player2, stagedCardsById);

            var stagedGame = new GameState(
                liveGame.GameId,
                stagedP1,
                stagedP2,
                liveGame.FirstPlayerId ?? stagedP1.PlayerId)
            {
                Status = liveGame.Status,
                Phase = liveGame.Phase,
                Mode = liveGame.Mode,
                IsCombatSandbox = false,
                CurrentPlayerId = liveGame.CurrentPlayerId,
                WinnerId = liveGame.WinnerId,
                GameMessage = liveGame.GameMessage,
                StartedAtUtc = liveGame.StartedAtUtc,
                TurnTimeoutSeconds = liveGame.TurnTimeoutSeconds,
                TurnStartedAtUtc = liveGame.TurnStartedAtUtc,
                TurnEndsAtUtc = liveGame.TurnEndsAtUtc,
                Mirage = CloneStagedMirageMatchState(liveGame.Mirage!),
                AbilityManager = stagedAbilityManager,
                BuffManager = stagedBuffManager,
                AuraManager = stagedAuraManager
            };

            stagedGame.InGameIds.ReplaceStateFrom(liveGame.InGameIds);
            stagedGame.RestoreRevisions(liveGame.StateRevision, liveGame.BuffRevision, liveGame.RefreshCycleId);

            foreach (var registeredId in liveGame.GetRegisteredCardInstanceIds())
            {
                if (stagedCardsById.TryGetValue(registeredId, out var stagedCard))
                {
                    stagedGame.RegisterCardInstance(stagedCard);
                }
            }

            liveAbilityManager.CopyToStaged(stagedAbilityManager, stagedCardsById, stagedServices);

            foreach (var liveBuff in liveBuffManager.GetAllRegisteredBuffs())
            {
                var buffElement = JsonSerializer.SerializeToElement(liveBuff, ReplayExecutionState.Json);
                var clonedBuff = ReplayExecutionState.RestoreBuff(buffElement);
                stagedBuffManager.RestoreBuffInstanceWithoutCreatingAbilities(clonedBuff, stagedGame);
            }

            stagedAuraManager.ReplaceStateFrom(liveAuraManager);
            stagedGame.RestoreRevisions(liveGame.StateRevision, liveGame.BuffRevision, liveGame.RefreshCycleId);

            return stagedGame;
        }

        internal static PlayerState CloneStagedPlayerState(
            PlayerState livePlayer,
            IReadOnlyDictionary<Guid, CardInstance> stagedCardsById)
        {
            var staged = new PlayerState(livePlayer.PlayerId)
            {
                DisplayName = livePlayer.DisplayName,
                AvatarUrl = livePlayer.AvatarUrl,
                Class = livePlayer.Class,
                SecondaryClass = livePlayer.SecondaryClass,
                Environment = livePlayer.Environment,
                Rule = livePlayer.Rule,
                IsTakeTwo = livePlayer.IsTakeTwo,
                IsMirageOpponentProxy = livePlayer.IsMirageOpponentProxy,
                UserDeckId = livePlayer.UserDeckId,
                IsAI = livePlayer.IsAI,
                IsFirst = livePlayer.IsFirst,
                HasCompletedMulligan = livePlayer.HasCompletedMulligan,
                TurnNumber = livePlayer.TurnNumber,
                HasTakenFirstTurn = livePlayer.HasTakenFirstTurn,
                MaxPlayPoints = livePlayer.MaxPlayPoints,
                CurrentPlayPoints = livePlayer.CurrentPlayPoints,
                PpBoostUsesAvailable = livePlayer.PpBoostUsesAvailable,
                PpBoostResetApplied = livePlayer.PpBoostResetApplied,
                IsPpBoostArmed = livePlayer.IsPpBoostArmed,
                MaxEvolutionPoints = livePlayer.MaxEvolutionPoints,
                CurrentEvolutionPoints = livePlayer.CurrentEvolutionPoints,
                MaxSuperEvolutionPoints = livePlayer.MaxSuperEvolutionPoints,
                CurrentSuperEvolutionPoints = livePlayer.CurrentSuperEvolutionPoints,
                IsEvolutionUnlocked = livePlayer.IsEvolutionUnlocked,
                IsSuperEvolutionUnlocked = livePlayer.IsSuperEvolutionUnlocked,
                IsIntromezzoActive = livePlayer.IsIntromezzoActive,
                Leader = stagedCardsById[livePlayer.Leader.InstanceId],
                Field = livePlayer.Field.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                Hand = livePlayer.Hand.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                Deck = livePlayer.Deck.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                Graveyard = livePlayer.Graveyard.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                Extra = livePlayer.Extra.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                Emblem = livePlayer.Emblem.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                InitialDeck = livePlayer.InitialDeck.Where(c => c != null && stagedCardsById.ContainsKey(c.InstanceId)).Select(c => stagedCardsById[c.InstanceId]).ToList(),
                InitialDeckDefinitionIds = new List<int>(livePlayer.InitialDeckDefinitionIds),
                Mirage = livePlayer.Mirage == null
                    ? null
                    : new MiragePlayerState
                    {
                        TavernTier = livePlayer.Mirage.TavernTier,
                        UpgradeCost = livePlayer.Mirage.UpgradeCost,
                        HardGoldCap = livePlayer.Mirage.HardGoldCap,
                        IsShopFrozen = livePlayer.Mirage.IsShopFrozen,
                        HasSelectedLeader = livePlayer.Mirage.HasSelectedLeader,
                        LeaderCandidateCardIds = new List<int>(livePlayer.Mirage.LeaderCandidateCardIds),
                        ShopOffers = livePlayer.Mirage.ShopOffers
                            .Where(o => o != null)
                            .Select(o => new MirageShopOffer
                            {
                                OfferId = o.OfferId,
                                CardDefinitionId = o.CardDefinitionId,
                                TavernTier = o.TavernTier,
                                BuyCost = o.BuyCost
                            })
                            .ToList(),
                        PendingTickets = livePlayer.Mirage.PendingTickets
                            .Where(t => t != null)
                            .Select(t => new MirageDiscoverTicket
                            {
                                TicketId = t.TicketId,
                                TargetTier = t.TargetTier,
                                CandidateCardDefinitionIds = new List<int>(t.CandidateCardDefinitionIds)
                            })
                            .ToList()
                    }
            };

            staged.RestoreManualEvolutionRight(livePlayer.HasUsedManualEvolutionRightThisTurn);
            foreach (var (key, val) in livePlayer.CounterMap)
            {
                staged.SetCounter(key, val);
            }

            return staged;
        }

        internal static MirageMatchState CloneStagedMirageMatchState(MirageMatchState liveMirage)
        {
            Dictionary<int, int> poolCopy;
            Dictionary<int, int> capsCopy;
            HashSet<Guid> returnedCopy;
            lock (liveMirage.SharedPoolSyncRoot)
            {
                poolCopy = new Dictionary<int, int>(liveMirage.SharedCardPool);
                capsCopy = new Dictionary<int, int>(liveMirage.InitialCardPoolCaps);
                returnedCopy = new HashSet<Guid>(liveMirage.ReturnedEliminatedPlayerIds);
            }

            return new MirageMatchState
            {
                TournamentRoomId = liveMirage.TournamentRoomId,
                TournamentSeatIndex = liveMirage.TournamentSeatIndex,
                IsNextRoundGateClosed = liveMirage.IsNextRoundGateClosed,
                RoundNumber = liveMirage.RoundNumber,
                ClosingRecruitRoundNumber = liveMirage.ClosingRecruitRoundNumber,
                ActiveCombatId = liveMirage.ActiveCombatId,
                LastCombatId = liveMirage.LastCombatId,
                LastCombatRoundNumber = liveMirage.LastCombatRoundNumber,
                LastCombatWinnerPlayerId = liveMirage.LastCombatWinnerPlayerId,
                LastCombatLoserPlayerId = liveMirage.LastCombatLoserPlayerId,
                LastCombatLeaderDamage = liveMirage.LastCombatLeaderDamage,
                LastCombatStepCount = liveMirage.LastCombatStepCount,
                ReadyPlayerIds = new HashSet<Guid>(liveMirage.ReadyPlayerIds),
                CommittedCombatIds = new HashSet<Guid>(liveMirage.CommittedCombatIds),
                SharedCardPool = poolCopy,
                InitialCardPoolCaps = capsCopy,
                ReturnedEliminatedPlayerIds = returnedCopy,
                Rules = new MirageRuleConfig
                {
                    HardGoldCap = liveMirage.Rules.HardGoldCap,
                    RecruitTimeoutSeconds = liveMirage.Rules.RecruitTimeoutSeconds,
                    BuyCost = liveMirage.Rules.BuyCost,
                    RefreshCost = liveMirage.Rules.RefreshCost,
                    SellRefund = liveMirage.Rules.SellRefund,
                    MaxTavernTier = liveMirage.Rules.MaxTavernTier,
                    MaxCombatSteps = liveMirage.Rules.MaxCombatSteps,
                    BaseUpgradeCostByTargetTier = new Dictionary<int, int>(liveMirage.Rules.BaseUpgradeCostByTargetTier),
                    InitialCopiesByTier = new Dictionary<int, int>(liveMirage.Rules.InitialCopiesByTier)
                }
            };
        }

        internal static void ApplyStagedPlayerStateInMemory(
            PlayerState livePlayer,
            PlayerState stagedPlayer,
            IReadOnlyDictionary<Guid, CardInstance> liveCardsById)
        {
            livePlayer.TurnNumber = stagedPlayer.TurnNumber;
            livePlayer.HasTakenFirstTurn = stagedPlayer.HasTakenFirstTurn;
            livePlayer.MaxPlayPoints = stagedPlayer.MaxPlayPoints;
            livePlayer.CurrentPlayPoints = stagedPlayer.CurrentPlayPoints;
            livePlayer.PpBoostUsesAvailable = stagedPlayer.PpBoostUsesAvailable;
            livePlayer.PpBoostResetApplied = stagedPlayer.PpBoostResetApplied;
            livePlayer.IsPpBoostArmed = stagedPlayer.IsPpBoostArmed;
            livePlayer.IsIntromezzoActive = stagedPlayer.IsIntromezzoActive;
            livePlayer.RestoreManualEvolutionRight(stagedPlayer.HasUsedManualEvolutionRightThisTurn);

            foreach (var existingKey in livePlayer.CounterMap.Keys.ToList())
            {
                if (!stagedPlayer.CounterMap.ContainsKey(existingKey))
                {
                    livePlayer.SetCounter(existingKey, 0);
                }
            }
            foreach (var (key, val) in stagedPlayer.CounterMap)
            {
                livePlayer.SetCounter(key, val);
            }

            if (stagedPlayer.Leader != null && liveCardsById.TryGetValue(stagedPlayer.Leader.InstanceId, out var liveLeader))
            {
                livePlayer.Leader = liveLeader;
            }

            livePlayer.Field = stagedPlayer.Field.Select(c => liveCardsById[c.InstanceId]).ToList();
            livePlayer.Hand = stagedPlayer.Hand.Select(c => liveCardsById[c.InstanceId]).ToList();
            livePlayer.Deck = stagedPlayer.Deck.Select(c => liveCardsById[c.InstanceId]).ToList();
            livePlayer.Graveyard = stagedPlayer.Graveyard.Select(c => liveCardsById[c.InstanceId]).ToList();
            livePlayer.Extra = stagedPlayer.Extra.Select(c => liveCardsById[c.InstanceId]).ToList();
            livePlayer.Emblem = stagedPlayer.Emblem.Select(c => liveCardsById[c.InstanceId]).ToList();

            if (livePlayer.Mirage != null && stagedPlayer.Mirage != null)
            {
                livePlayer.Mirage.TavernTier = stagedPlayer.Mirage.TavernTier;
                livePlayer.Mirage.UpgradeCost = stagedPlayer.Mirage.UpgradeCost;
                livePlayer.Mirage.HardGoldCap = stagedPlayer.Mirage.HardGoldCap;
                livePlayer.Mirage.IsShopFrozen = stagedPlayer.Mirage.IsShopFrozen;
                livePlayer.Mirage.HasSelectedLeader = stagedPlayer.Mirage.HasSelectedLeader;
                livePlayer.Mirage.LeaderCandidateCardIds = new List<int>(stagedPlayer.Mirage.LeaderCandidateCardIds);
                livePlayer.Mirage.ShopOffers = stagedPlayer.Mirage.ShopOffers;
                livePlayer.Mirage.PendingTickets = stagedPlayer.Mirage.PendingTickets;
            }
        }

        internal static bool TryDrainStagedBuffEvents(
            IEnumerable<IGameEvent>? initialEvents,
            GameContext stagedContext,
            out string? errorMessage)
        {
            errorMessage = null;
            if (initialEvents == null)
            {
                return true;
            }

            var queue = new Queue<IGameEvent>(initialEvents);
            int iterations = 0;
            while (queue.Count > 0)
            {
                if (++iterations > MaxStagedBuffEventDrainIterations)
                {
                    errorMessage = $"Staged Buff event drain exceeded safety limit ({MaxStagedBuffEventDrainIterations}).";
                    return false;
                }

                var ev = queue.Dequeue();
                if (ev is (BuffAppliedEvent or BuffRemovedEvent) && ev is ICommittableEvent committable)
                {
                    var subResult = committable.Commit(stagedContext);
                    if (!subResult.Success)
                    {
                        errorMessage = subResult.ErrorMessage ?? $"Staged {ev.EventType} commit failed.";
                        return false;
                    }

                    foreach (var child in subResult.GeneratedEvents)
                    {
                        if (child is BuffAppliedEvent or BuffRemovedEvent)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            return true;
        }

        public string GetAnnouncement(GameState gameState)
        {
            if (WasNoOpIdempotent)
            {
                return string.Empty;
            }

            if (Outcome.IsTie)
            {
                return $"第 {RoundNumber} 轮幻境战斗结束：双方战平。";
            }

            var winner = Outcome.WinnerPlayerId.HasValue ? gameState.GetPlayerState(Outcome.WinnerPlayerId.Value) : null;
            var loser = Outcome.LoserPlayerId.HasValue ? gameState.GetPlayerState(Outcome.LoserPlayerId.Value) : null;
            var winnerToken = winner != null ? AnnouncementTokens.Player(winner) : "胜方";
            var loserToken = loser != null ? AnnouncementTokens.Player(loser) : "败方";

            if (IsGameOverResult)
            {
                return $"第 {RoundNumber} 轮幻境战斗结束：{winnerToken}对{loserToken}造成 {Outcome.LeaderDamage} 点致命伤害，赢得对局！";
            }

            return $"第 {RoundNumber} 轮幻境战斗结束：{winnerToken}获胜，对{loserToken}主战者造成 {Outcome.LeaderDamage} 点伤害。进入第 {AdvancedToRoundNumber ?? (RoundNumber + 1)} 轮招募阶段。";
        }

        internal sealed class ReadOnlyStagedEventHistoryService : IEventHistoryService
        {
            private readonly IEventHistoryService? _inner;

            public ReadOnlyStagedEventHistoryService(IEventHistoryService? inner)
            {
                _inner = inner;
            }

            public IReadOnlyList<IGameEvent> GetEvents(string gameId)
            {
                return _inner?.GetEvents(gameId) ?? Array.Empty<IGameEvent>();
            }

            public Task<IGameEvent?> GetEventByIdAsync(string gameId, Guid eventId)
            {
                return _inner != null
                    ? _inner.GetEventByIdAsync(gameId, eventId)
                    : Task.FromResult<IGameEvent?>(null);
            }

            public Task StoreEventAsync(string gameId, IGameEvent gameEvent)
            {
                return Task.CompletedTask;
            }

            public Task ClearHistoryForGameAsync(string gameId)
            {
                return Task.CompletedTask;
            }
        }
    }
}
