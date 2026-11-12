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
    public enum MirageTournamentSeatOutcomeKind
    {
        AdvanceRound = 0,
        Eliminated = 1,
        Champion = 2
    }

    public sealed record MirageStageAEvaluationResult(
        bool Success,
        string? ErrorMessage,
        int PreCombatHp,
        int PostCombatHp,
        int PostCombatMaxHp,
        int TavernTier,
        int LeaderCardDefinitionId);

    [GameEventMetadata(
        "幻境八人赛席位轮次结算",
        "当8人幻境赛事协调器向单席位提交本轮战斗伤害、永久成长、淘汰名次或下一轮推进时触发。",
        EventType.MirageTournamentRoundSettled,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public sealed class MirageTournamentRoundSettledEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        public Guid CombatId { get; }
        public int RoundNumber { get; }
        public int StepCount { get; }
        public int SelfDamageTaken { get; }
        public int OpponentDamageTaken { get; }
        public string OpponentDisplayName { get; }
        public bool IsCombatTie { get; }
        public bool SelfWonCombat { get; }
        public IReadOnlyList<MiragePermanentGrowthEntry> SelfPermanentGrowthLog { get; }
        public MirageTournamentSeatOutcomeKind OutcomeKind { get; }
        public int? FinalPlacement { get; }
        public string? EndReason { get; }
        public List<MirageShopOffer>? PreRolledNextShopOffers { get; }
        public DateTime? NextRoundStartedAtUtc { get; }
        public DateTime? NextRoundEndsAtUtc { get; }
        public int? NextRoundTimeoutSeconds { get; }

        public bool WasNoOpIdempotent { get; private set; }
        public bool IsGameOverResult { get; private set; }
        public int? AdvancedToRoundNumber { get; private set; }

        public MirageTournamentRoundSettledEvent(
            Guid combatId,
            int roundNumber,
            int stepCount,
            int selfDamageTaken,
            int opponentDamageTaken,
            string opponentDisplayName,
            bool isCombatTie,
            bool selfWonCombat,
            IReadOnlyList<MiragePermanentGrowthEntry>? selfPermanentGrowthLog,
            MirageTournamentSeatOutcomeKind outcomeKind,
            int? finalPlacement,
            string? endReason,
            List<MirageShopOffer>? preRolledNextShopOffers,
            DateTime? nextRoundStartedAtUtc,
            DateTime? nextRoundEndsAtUtc,
            int? nextRoundTimeoutSeconds,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.MirageTournamentRoundSettled, eventId, timestamp)
        {
            CombatId = combatId;
            RoundNumber = roundNumber;
            StepCount = Math.Max(0, stepCount);
            SelfDamageTaken = Math.Max(0, selfDamageTaken);
            OpponentDamageTaken = Math.Max(0, opponentDamageTaken);
            OpponentDisplayName = opponentDisplayName ?? "对手";
            IsCombatTie = isCombatTie;
            SelfWonCombat = selfWonCombat;
            SelfPermanentGrowthLog = selfPermanentGrowthLog ?? Array.Empty<MiragePermanentGrowthEntry>();
            OutcomeKind = outcomeKind;
            FinalPlacement = finalPlacement;
            EndReason = endReason;
            PreRolledNextShopOffers = preRolledNextShopOffers;
            NextRoundStartedAtUtc = nextRoundStartedAtUtc;
            NextRoundEndsAtUtc = nextRoundEndsAtUtc;
            NextRoundTimeoutSeconds = nextRoundTimeoutSeconds;
        }

        public static MirageStageAEvaluationResult EvaluateStageA(
            GameState liveGameState,
            IServiceProvider rootServiceProvider,
            AutoMapper.IMapper mapper,
            IEventHistoryService? eventHistoryService,
            int roundNumber,
            int selfDamageTaken,
            IReadOnlyList<MiragePermanentGrowthEntry>? selfGrowthLog,
            CancellationToken cancellationToken)
        {
            if (liveGameState == null || liveGameState.Mode != GameMode.Mirage || liveGameState.Mirage == null)
            {
                return new MirageStageAEvaluationResult(false, "Seat GameState is not in Mirage mode.", 0, 0, 20, 1, 0);
            }

            var selfPlayerId = liveGameState.Player1.PlayerId;
            if (liveGameState.AbilityManager is not AbilityManager liveAbilityManager
                || liveGameState.BuffManager is not BuffManager liveBuffManager
                || liveGameState.AuraManager is not AuraManager liveAuraManager)
            {
                return new MirageStageAEvaluationResult(false, "Seat GameState managers are invalid.", 0, 0, 20, 1, 0);
            }

            if (!TryPreBuildPermanentBuffs(liveGameState, selfPlayerId, selfGrowthLog, out var preBuiltBuffs, out var buffError))
            {
                return new MirageStageAEvaluationResult(false, buffError, 0, 0, 20, 1, 0);
            }

            var scopeFactory = rootServiceProvider.GetRequiredService<IServiceScopeFactory>();
            using var stagedScope = scopeFactory.CreateScope();
            var stagedProvider = stagedScope.ServiceProvider;

            try
            {
                var accessor = stagedProvider.GetRequiredService<IGameContextAccessor>();
                using var suspendScope = accessor.SuspendAmbient();
                using var guardScope = SandboxExecutionGuard.Enter();

                var stagedAbilityManager = stagedProvider.GetRequiredService<AbilityManager>();
                var stagedBuffManager = stagedProvider.GetRequiredService<BuffManager>();
                var stagedAuraManager = ActivatorUtilities.CreateInstance<AuraManager>(stagedProvider);
                var stagedBuffService = (BuffService)stagedProvider.GetRequiredService<IBuffService>();

                var liveCardsById = MirageCombatResultCommittedEvent.CollectAllCards(liveGameState);
                var stagedGameState = MirageCombatResultCommittedEvent.BuildStagedGameState(
                    liveGameState,
                    liveCardsById,
                    stagedProvider,
                    stagedAbilityManager,
                    stagedBuffManager,
                    stagedAuraManager,
                    liveAbilityManager,
                    liveBuffManager,
                    liveAuraManager,
                    out var stagedCardsById);

                using var stagedContext = GameContext.CreateForSandbox(
                    stagedProvider,
                    mapper,
                    accessor,
                    stagedGameState,
                    stagedBuffService,
                    new MirageCombatResultCommittedEvent.ReadOnlyStagedEventHistoryService(eventHistoryService),
                    selfPlayerId,
                    cancellationToken);

                stagedBuffService.RefreshDerivedStateCache(stagedContext, "mirage_8p_stage_a_pre");
                var stagedSelf = stagedGameState.Player1;
                var preMaxHp = stagedSelf.Leader != null
                    ? stagedBuffService.GetEffectiveMaxHealth(stagedSelf.Leader, stagedContext)
                    : 20;
                var preHp = preMaxHp - (stagedSelf.Leader?.Damage ?? 0);

                foreach (var (targetCardId, buff) in preBuiltBuffs)
                {
                    if (!stagedCardsById.TryGetValue(targetCardId, out var stagedTargetCard)
                        || stagedTargetCard.CurrentZone == CardZone.Retired)
                    {
                        continue;
                    }

                    var buffApplied = new BuffAppliedEvent(buff, stagedTargetCard, stagedTargetCard);
                    if (!MirageCombatResultCommittedEvent.TryDrainStagedBuffEvents(new IGameEvent[] { buffApplied }, stagedContext, out var drainErr))
                    {
                        return new MirageStageAEvaluationResult(false, drainErr, preHp, preHp, preMaxHp, stagedSelf.Mirage?.TavernTier ?? 1, stagedSelf.Leader?.BaseDefinitionId ?? 0);
                    }
                }

                if (selfDamageTaken > 0 && stagedSelf.Leader != null)
                {
                    stagedSelf.Leader.Damage += selfDamageTaken;
                    stagedBuffService.InvalidateDerivedState(stagedGameState, stagedSelf.Leader.InstanceId);
                }

                stagedBuffService.RefreshDerivedStateCache(stagedContext, "mirage_8p_stage_a_post");
                var postMaxHp = stagedSelf.Leader != null
                    ? stagedBuffService.GetEffectiveMaxHealth(stagedSelf.Leader, stagedContext)
                    : 20;
                var postHp = postMaxHp - (stagedSelf.Leader?.Damage ?? 0);

                return new MirageStageAEvaluationResult(
                    true,
                    null,
                    preHp,
                    postHp,
                    postMaxHp,
                    stagedSelf.Mirage?.TavernTier ?? 1,
                    stagedSelf.Leader?.BaseDefinitionId ?? 0);
            }
            catch (Exception ex)
            {
                return new MirageStageAEvaluationResult(false, $"Stage A evaluation failed: {ex.Message}", 0, 0, 20, 1, 0);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var gameState = context?.CurrentGameState;
            if (gameState == null || gameState.Mode != GameMode.Mirage || gameState.Mirage == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "MirageTournamentRoundSettledEvent is only valid in Mirage mode."
                };
            }

            if (CombatId == Guid.Empty)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CombatId cannot be empty." };
            }

            var mirage = gameState.Mirage;
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
                        ErrorMessage = "Seat game is already over."
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

            var selfPlayerId = gameState.Player1.PlayerId;
            if (!TryPreBuildPermanentBuffs(gameState, selfPlayerId, SelfPermanentGrowthLog, out var preBuiltBuffs, out var buffBuildErr))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = buffBuildErr };
            }

            if (context!.AbilityManager is not AbilityManager liveAbilityManager
                || context.BuffManager is not BuffManager liveBuffManager
                || context.AuraManager is not AuraManager liveAuraManager
                || context.BuffService is not BuffService liveBuffService)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Mirage tournament round settle requires concrete managers."
                };
            }

            var scopeFactory = context.ServiceProvider.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "IServiceScopeFactory is unavailable." };
            }

            var liveCardsById = MirageCombatResultCommittedEvent.CollectAllCards(gameState);
            using var stagedScope = scopeFactory.CreateScope();
            var stagedProvider = stagedScope.ServiceProvider;

            GameState stagedGameState;
            Dictionary<Guid, CardInstance> stagedCardsById;
            AbilityManager stagedAbilityManager;
            BuffManager stagedBuffManager;
            AuraManager stagedAuraManager;
            BuffService stagedBuffService;
            bool stagedIsGameOver = false;
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

                stagedGameState = MirageCombatResultCommittedEvent.BuildStagedGameState(
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
                    new MirageCombatResultCommittedEvent.ReadOnlyStagedEventHistoryService(context.EventHistoryService),
                    selfPlayerId,
                    context.CancellationToken);

                stagedBuffService.RefreshDerivedStateCache(stagedContext, "mirage_8p_commit_pre");

                foreach (var (targetCardId, buff) in preBuiltBuffs)
                {
                    if (!stagedCardsById.TryGetValue(targetCardId, out var stagedTargetCard)
                        || stagedTargetCard.CurrentZone == CardZone.Retired)
                    {
                        continue;
                    }

                    var buffApplied = new BuffAppliedEvent(buff, stagedTargetCard, stagedTargetCard);
                    if (!MirageCombatResultCommittedEvent.TryDrainStagedBuffEvents(new IGameEvent[] { buffApplied }, stagedContext, out var buffError))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = buffError };
                    }
                }

                var stagedSelf = stagedGameState.Player1;
                if (SelfDamageTaken > 0 && stagedSelf.Leader != null)
                {
                    stagedSelf.Leader.Damage += SelfDamageTaken;
                    stagedBuffService.InvalidateDerivedState(stagedGameState, stagedSelf.Leader.InstanceId);
                }

                if (OutcomeKind is MirageTournamentSeatOutcomeKind.Eliminated or MirageTournamentSeatOutcomeKind.Champion)
                {
                    stagedIsGameOver = true;
                    stagedGameState.Status = GameStatus.Finished;
                    stagedGameState.Phase = GamePhase.GameOver;
                    stagedGameState.WinnerId = OutcomeKind == MirageTournamentSeatOutcomeKind.Champion ? selfPlayerId : null;
                    stagedGameState.GameMessage = EndReason ?? (OutcomeKind == MirageTournamentSeatOutcomeKind.Champion
                        ? "第 1 名 · 夺得幻境战棋冠军！"
                        : $"第 {FinalPlacement ?? 8} 名 · 幻境淘汰出局");
                    stagedGameState.TurnStartedAtUtc = null;
                    stagedGameState.TurnEndsAtUtc = null;
                    stagedGameState.Mirage!.IsNextRoundGateClosed = false;
                }
                else
                {
                    if (stagedSelf.Mirage == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Player1 MiragePlayerState is missing." };
                    }

                    var nextRound = RoundNumber + 1;
                    stagedAdvancedToRound = nextRound;
                    stagedGameState.Mirage!.RoundNumber = nextRound;
                    stagedGameState.Mirage.ClosingRecruitRoundNumber = null;
                    // Keep gate closed until coordinator finishes Stage B on ALL seats and enters Recruit uniformly
                    stagedGameState.Mirage.IsNextRoundGateClosed = true;
                    stagedGameState.Phase = GamePhase.Recruit;
                    stagedGameState.Mirage.ReadyPlayerIds.Clear();

                    var now = NextRoundStartedAtUtc ?? DateTime.UtcNow;
                    var timeoutSec = NextRoundTimeoutSeconds
                        ?? (stagedGameState.TurnTimeoutSeconds > 0 ? stagedGameState.TurnTimeoutSeconds : stagedGameState.Mirage.Rules.RecruitTimeoutSeconds);
                    stagedGameState.TurnTimeoutSeconds = timeoutSec;
                    stagedGameState.TurnStartedAtUtc = now;
                    stagedGameState.TurnEndsAtUtc = NextRoundEndsAtUtc ?? now.AddSeconds(timeoutSec);

                    if (PreRolledNextShopOffers != null)
                    {
                        stagedSelf.Mirage.ShopOffers = PreRolledNextShopOffers
                            .Select(o => new MirageShopOffer
                            {
                                OfferId = o.OfferId,
                                CardDefinitionId = o.CardDefinitionId,
                                TavernTier = o.TavernTier,
                                BuyCost = o.BuyCost
                            })
                            .ToList();
                        stagedSelf.Mirage.IsShopFrozen = false;
                    }

                    var roundGold = stagedGameState.Mirage.Rules.GetRoundBaseGold(nextRound);
                    var selfReset = stagedSelf.ResetForMirageRoundStart(stagedContext, roundGold, decrementUpgradeCost: true);
                    if (!selfReset.Success)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = selfReset.ErrorMessage ?? "Player1 round reset failed." };
                    }

                    if (!MirageCombatResultCommittedEvent.TryDrainStagedBuffEvents(selfReset.GeneratedEvents, stagedContext, out var resetErr))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = resetErr };
                    }

                    var auraRefresh = MirageCardRetirement.RefreshAllBoardAuras(stagedContext, "mirage_8p_round_settled_auras");
                    if (!auraRefresh.Success)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = auraRefresh.ErrorMessage };
                    }

                    if (!MirageCombatResultCommittedEvent.TryDrainStagedBuffEvents(auraRefresh.GeneratedEvents, stagedContext, out var auraErr))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = auraErr };
                    }
                }

                stagedBuffService.RefreshDerivedStateCache(stagedContext, "mirage_8p_commit_final");
            }
            catch (Exception ex)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Staged Mirage 8P settle failed: {ex.Message}"
                };
            }

            // Phase B: Mount pre-computed staged state onto live seat GameState without replacing SharedCardPool reference
            Source = TargetInfo.ForPlayer(gameState.Player1);
            IsGameOverResult = stagedIsGameOver;
            AdvancedToRoundNumber = stagedAdvancedToRound;

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

            MirageCombatResultCommittedEvent.ApplyStagedPlayerStateInMemory(gameState.Player1, stagedGameState.Player1, liveCardsById);
            MirageCombatResultCommittedEvent.ApplyStagedPlayerStateInMemory(gameState.Player2, stagedGameState.Player2, liveCardsById);

            liveAbilityManager.ReplaceStateFrom(stagedAbilityManager, liveCardsById);
            liveBuffManager.ReplaceStateFrom(stagedBuffManager);
            liveAuraManager.ReplaceStateFrom(stagedAuraManager);
            gameState.InGameIds.ReplaceStateFrom(stagedGameState.InGameIds);
            gameState.RestoreRevisions(
                stagedGameState.StateRevision,
                stagedGameState.BuffRevision,
                stagedGameState.RefreshCycleId);
            liveBuffService.ReplaceDerivedStateCacheFrom(gameState.GameId, stagedBuffService);

            gameState.Status = stagedGameState.Status;
            gameState.Phase = stagedGameState.Phase;
            gameState.WinnerId = stagedGameState.WinnerId;
            gameState.GameMessage = stagedGameState.GameMessage;
            gameState.TurnTimeoutSeconds = stagedGameState.TurnTimeoutSeconds;
            gameState.TurnStartedAtUtc = stagedGameState.TurnStartedAtUtc;
            gameState.TurnEndsAtUtc = stagedGameState.TurnEndsAtUtc;

            lock (mirage.RoundSyncLock)
            {
                if (stagedAdvancedToRound.HasValue)
                {
                    mirage.RoundNumber = stagedAdvancedToRound.Value;
                    mirage.ClosingRecruitRoundNumber = null;
                    mirage.ReadyPlayerIds.Clear();
                }

                mirage.IsNextRoundGateClosed = stagedGameState.Mirage?.IsNextRoundGateClosed ?? false;
                mirage.LastCombatId = CombatId;
                mirage.LastCombatRoundNumber = RoundNumber;
                mirage.LastCombatWinnerPlayerId = IsCombatTie ? null : (SelfWonCombat ? selfPlayerId : gameState.Player2.PlayerId);
                mirage.LastCombatLoserPlayerId = IsCombatTie ? null : (SelfWonCombat ? gameState.Player2.PlayerId : selfPlayerId);
                mirage.LastCombatLeaderDamage = SelfWonCombat ? OpponentDamageTaken : SelfDamageTaken;
                mirage.LastCombatStepCount = StepCount;
                mirage.CommittedCombatIds.Add(CombatId);
                mirage.ActiveCombatId = null;
            }

            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            if (WasNoOpIdempotent)
            {
                return string.Empty;
            }

            var selfToken = AnnouncementTokens.Player(gameState.Player1);
            if (OutcomeKind == MirageTournamentSeatOutcomeKind.Champion)
            {
                return $"第 {RoundNumber} 轮幻境交战结束：{selfToken}获胜并夺得本局幻境第 1 名！";
            }

            if (OutcomeKind == MirageTournamentSeatOutcomeKind.Eliminated)
            {
                return $"第 {RoundNumber} 轮幻境交战结束：{selfToken}受到 {SelfDamageTaken} 点伤害，以第 {FinalPlacement ?? 8} 名结束征程。";
            }

            if (IsCombatTie)
            {
                return $"第 {RoundNumber} 轮幻境交战（对阵 {OpponentDisplayName}）：双方战平。进入第 {AdvancedToRoundNumber ?? (RoundNumber + 1)} 轮招募阶段。";
            }

            if (SelfWonCombat)
            {
                return $"第 {RoundNumber} 轮幻境交战（对阵 {OpponentDisplayName}）：{selfToken}获胜，造成 {OpponentDamageTaken} 点伤害。进入第 {AdvancedToRoundNumber ?? (RoundNumber + 1)} 轮招募阶段。";
            }

            return $"第 {RoundNumber} 轮幻境交战（对阵 {OpponentDisplayName}）：{selfToken}受到 {SelfDamageTaken} 点伤害。进入第 {AdvancedToRoundNumber ?? (RoundNumber + 1)} 轮招募阶段。";
        }

        private static bool TryPreBuildPermanentBuffs(
            GameState gameState,
            Guid selfPlayerId,
            IReadOnlyList<MiragePermanentGrowthEntry>? growthEntries,
            out List<(Guid TargetCardInstanceId, Buff BuffInstance)> result,
            out string? errorMessage)
        {
            result = new List<(Guid, Buff)>();
            errorMessage = null;
            if (growthEntries == null || growthEntries.Count == 0)
            {
                return true;
            }

            var owner = gameState.GetPlayerState(selfPlayerId);
            if (owner == null)
            {
                errorMessage = "Self player not found in seat GameState.";
                return false;
            }

            foreach (var entry in growthEntries)
            {
                if (entry == null || entry.PlayerId != selfPlayerId || entry.OriginInstanceId == Guid.Empty)
                {
                    continue;
                }

                var targetCard = owner.Field.FirstOrDefault(c => c != null && c.InstanceId == entry.OriginInstanceId)
                    ?? owner.Hand.FirstOrDefault(c => c != null && c.InstanceId == entry.OriginInstanceId);
                if (targetCard == null || targetCard.CurrentZone == CardZone.Retired)
                {
                    continue;
                }

                try
                {
                    var buffNode = JsonNode.Parse(entry.SerializedBuff.GetRawText()) as JsonObject;
                    if (buffNode == null)
                    {
                        errorMessage = $"SerializedBuff for card {entry.OriginInstanceId} is not a JSON object.";
                        return false;
                    }

                    buffNode["id"] = Guid.NewGuid();
                    buffNode["targetCardInstanceId"] = targetCard.InstanceId;
                    buffNode["sourceCardInstanceId"] = targetCard.InstanceId;
                    buffNode["sourceAbilityId"] = Guid.Empty;
                    buffNode["summaryRootAbilityId"] = null;
                    buffNode["isActive"] = true;
                    var remappedBuffElement = JsonSerializer.SerializeToElement(buffNode, ReplayExecutionState.Json);
                    var restoredBuff = ReplayExecutionState.RestoreBuff(remappedBuffElement);
                    if (restoredBuff == null
                        || restoredBuff.Effects == null
                        || restoredBuff.Effects.Count == 0
                        || restoredBuff.DurationType != DurationType.Permanent
                        || restoredBuff.IsAuraGenerated)
                    {
                        errorMessage = $"Invalid permanent growth Buff for card {entry.OriginInstanceId}.";
                        return false;
                    }

                    restoredBuff.IsAuraGenerated = false;
                    result.Add((targetCard.InstanceId, restoredBuff));
                }
                catch (Exception ex)
                {
                    errorMessage = $"Failed to deserialize permanent growth Buff for card {entry.OriginInstanceId}: {ex.Message}";
                    return false;
                }
            }

            return true;
        }
    }
}
