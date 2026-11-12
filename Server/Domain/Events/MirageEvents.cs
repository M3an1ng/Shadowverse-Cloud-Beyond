using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public static class MirageCardRetirement
    {
        /// <summary>
        /// Shared non-selling retirement step (Contract 3.1):
        /// 1. Removes card from Field/Hand and moves to CardZone.Retired (reindexing remaining zone cards, no Graveyard/Shadows/Destroy);
        /// 2. Calls BuffService.RemoveAllBuffs and collects generated events;
        /// 3. Calls AuraManager.UnregisterBySourceCard and collects generated events;
        /// 4. Calls AbilityManager.UnregisterCardAbilities without recreating definition abilities.
        /// </summary>
        public static DomainOperationResult RetireCardFromPlay(
            CardInstance card,
            PlayerState owner,
            GameContext context)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var result = new DomainOperationResult { Success = true };

            var removeBuffsResult = context.BuffService.RemoveAllBuffs(card.InstanceId, context)
                ?? new DomainOperationResult { Success = true };
            if (!removeBuffsResult.Success)
            {
                return removeBuffsResult;
            }
            result.GeneratedEvents.AddRange(removeBuffsResult.GeneratedEvents);

            var auraResult = context.AuraManager.UnregisterBySourceCard(card.InstanceId, context);
            if (!auraResult.Success)
            {
                return auraResult;
            }
            result.GeneratedEvents.AddRange(auraResult.GeneratedEvents);

            context.AbilityManager.UnregisterCardAbilities(card.InstanceId, "mirage_retired");

            var sourceZone = card.CurrentZone;
            if (!owner.ApplyCardZoneChange(card, sourceZone, CardZone.Retired, null))
            {
                owner.Field.Remove(card);
                owner.Hand.Remove(card);
                card.CurrentZone = CardZone.Retired;
                card.ZoneIndex = null;
            }

            context.BuffService.InvalidateDerivedState(context.CurrentGameState, card.InstanceId);
            return result;
        }

        /// <summary>
        /// Re-evaluates all active continuous aura abilities across the board after lineup or position changes,
        /// collecting any generated BuffApplied/BuffRemoved events and refreshing the derived-state cache.
        /// </summary>
        public static DomainOperationResult RefreshAllBoardAuras(GameContext context, string cacheReason = "mirage_board_auras_refreshed")
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var result = new DomainOperationResult { Success = true };
            context.BuffService.RefreshDerivedStateCache(context, $"{cacheReason}_pre");

            var freshContext = context with { TriggeringEvent = null };
            var activeAbilities = (context.AbilityManager as ShadowverseBeyond.Server.Domain.Abilities.AbilityManager)?.GetActiveAbilities();
            if (activeAbilities == null)
            {
                return result;
            }

            foreach (var ability in activeAbilities)
            {
                if (ability is not ShadowverseBeyond.Server.Domain.Abilities.Ability concreteAbility
                    || concreteAbility.Type != ShadowverseBeyond.Server.Domain.Abilities.Common.AbilityType.Continuous
                    || concreteAbility.Config.Auras.All(aura => aura == null || !aura.IsEnabled))
                {
                    continue;
                }

                if (context.AbilityManager.IsAbilitySuppressed(concreteAbility, freshContext))
                {
                    continue;
                }

                if (concreteAbility.SourceCard != null)
                {
                    if (concreteAbility.SourceCard.IsSilenced)
                    {
                        continue;
                    }

                    var trigger = concreteAbility.Config.Trigger;
                    if (trigger != null && trigger.IsEnabled && !trigger.IsValidZone(concreteAbility.SourceCard.CurrentZone))
                    {
                        continue;
                    }
                }

                var auraResult = context.AbilityManager.RefreshAuraForAbility(ability, freshContext);
                if (!auraResult.Success)
                {
                    return auraResult;
                }
                result.GeneratedEvents.AddRange(auraResult.GeneratedEvents);
            }

            context.BuffService.RefreshDerivedStateCache(context, cacheReason);
            return result;
        }

        public static DomainOperationResult TryReplacePlayerMirageLeader(
            GameContext context,
            GameState gameState,
            PlayerState player,
            int leaderCardDefinitionId,
            out CardInstance? createdLeader)
        {
            createdLeader = null;
            var chosenDef = GameDataProvider.Instance?.GetCardDefinition(leaderCardDefinitionId);
            if (chosenDef == null
                || !chosenDef.IsMirageCard
                || chosenDef.Type != CardType.Leader
                || chosenDef.Id <= 0
                || !chosenDef.IsCollectible)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"卡牌定义 {leaderCardDefinitionId} 不是合法的可选幻境主战者。"
                };
            }

            var cardFactory = context.ServiceProvider.GetService<ICardFactory>();
            if (cardFactory == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ICardFactory is not available." };
            }

            var result = new DomainOperationResult { Success = true };
            var oldLeader = player.Leader;
            var oldDamage = Math.Max(0, oldLeader?.Damage ?? 0);
            var oldBaseHealthOverride = oldLeader?.BaseHealthOverride;

            if (oldLeader != null)
            {
                var removeBuffsResult = context.BuffService.RemoveAllBuffs(oldLeader.InstanceId, context)
                    ?? new DomainOperationResult { Success = true };
                if (!removeBuffsResult.Success)
                {
                    return removeBuffsResult;
                }
                result.GeneratedEvents.AddRange(removeBuffsResult.GeneratedEvents);

                var auraUnregisterResult = context.AuraManager.UnregisterBySourceCard(oldLeader.InstanceId, context);
                if (!auraUnregisterResult.Success)
                {
                    return auraUnregisterResult;
                }
                result.GeneratedEvents.AddRange(auraUnregisterResult.GeneratedEvents);

                context.AbilityManager.UnregisterCardAbilities(oldLeader.InstanceId, "mirage_leader_replaced");
                gameState.UnregisterCardInstance(oldLeader.InstanceId);
            }

            var newLeader = cardFactory.CreateAndRegisterNewCard(gameState, leaderCardDefinitionId, player.PlayerId, CardZone.Field);
            if (oldBaseHealthOverride.HasValue)
            {
                var targetMaxHealth = oldBaseHealthOverride.Value;
                newLeader.BaseHealthOverride = targetMaxHealth;
                newLeader.Damage = Math.Clamp(oldDamage, 0, Math.Max(0, targetMaxHealth - 1));
            }
            else
            {
                var targetMaxHealth = chosenDef.Health ?? ShadowverseBeyond.Server.Domain.Constants.GameConstants.Health.PlayerMaxHealth;
                newLeader.BaseHealthOverride = targetMaxHealth;
                newLeader.Damage = 0;
            }

            player.Leader = newLeader;
            player.Class = chosenDef.Class;
            if (player.Mirage != null)
            {
                player.Mirage.HasSelectedLeader = true;
                player.Mirage.LeaderCandidateCardIds.Clear();
            }

            createdLeader = newLeader;
            var auraRefreshResult = RefreshAllBoardAuras(context, "mirage_leader_selected");
            if (!auraRefreshResult.Success)
            {
                return auraRefreshResult;
            }
            result.GeneratedEvents.AddRange(auraRefreshResult.GeneratedEvents);
            return result;
        }
    }

    internal static class MirageRecruitValidation
    {
        public static bool TryValidateRecruitAction(
            GameContext context,
            Guid playerId,
            out GameState gameState,
            out MirageMatchState mirage,
            out PlayerState player,
            out string? errorMessage)
        {
            gameState = context.CurrentGameState;
            mirage = gameState.Mirage!;
            player = null!;
            errorMessage = null;

            if (gameState.Mode != GameMode.Mirage || gameState.Mirage == null)
            {
                errorMessage = "Action is only valid in Mirage mode.";
                return false;
            }

            lock (gameState.Mirage.RoundSyncLock)
            {
                if (gameState.Phase != GamePhase.Recruit)
                {
                    errorMessage = $"Action is only allowed during Recruit phase (Current: {gameState.Phase}).";
                    return false;
                }

                if (gameState.Mirage.IsNextRoundGateClosed)
                {
                    errorMessage = "正在等待本轮全房结算同步完成，暂不可操作。";
                    return false;
                }

                if (gameState.Mirage.ReadyPlayerIds.Contains(playerId)
                    || gameState.Mirage.ClosingRecruitRoundNumber == gameState.Mirage.RoundNumber)
                {
                    errorMessage = "Player has already locked lineup or recruit time has expired for this round.";
                    return false;
                }
            }

            var resolvedPlayer = gameState.GetPlayerState(playerId);
            if (resolvedPlayer == null || resolvedPlayer.Mirage == null)
            {
                errorMessage = $"Mirage player state not found for Player {playerId}.";
                return false;
            }

            player = resolvedPlayer;
            return true;
        }

        public static bool TryValidateDerivedTripleAction(
            GameContext context,
            Guid playerId,
            int? expectedRoundNumber,
            out GameState gameState,
            out MirageMatchState mirage,
            out PlayerState player,
            out string? errorMessage)
        {
            gameState = context.CurrentGameState;
            mirage = gameState.Mirage!;
            player = null!;
            errorMessage = null;

            if (gameState.Mode != GameMode.Mirage || gameState.Mirage == null)
            {
                errorMessage = "Triple synthesis is only valid in Mirage mode.";
                return false;
            }

            if (gameState.IsGameOver || gameState.Phase != GamePhase.Recruit)
            {
                errorMessage = $"Triple synthesis is only allowed during Recruit phase (Current: {gameState.Phase}).";
                return false;
            }

            if (expectedRoundNumber.HasValue && gameState.Mirage.RoundNumber != expectedRoundNumber.Value)
            {
                errorMessage = $"Stale triple synthesis event for round {expectedRoundNumber.Value} (Current: {gameState.Mirage.RoundNumber}).";
                return false;
            }

            var resolvedPlayer = gameState.GetPlayerState(playerId);
            if (resolvedPlayer == null || resolvedPlayer.Mirage == null)
            {
                errorMessage = $"Mirage player state not found for Player {playerId}.";
                return false;
            }

            player = resolvedPlayer;
            return true;
        }
    }

    [GameEventMetadata(
        "幻境轮次开始",
        "当幻境模式进入新的招募轮次时触发。",
        EventType.MirageRoundStarted,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageRoundStartedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("轮次编号", "当前进入的幻境招募轮次。")]
        public int RoundNumber { get; }

        public MirageRoundStartedEvent(PlayerState hostPlayer, int roundNumber, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.MirageRoundStarted, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(hostPlayer);
            RoundNumber = Math.Max(1, roundNumber);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var gameState = context.CurrentGameState;
            if (gameState.Mode != GameMode.Mirage || gameState.Mirage == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "MirageRoundStartedEvent is only valid in Mirage mode."
                };
            }

            var result = new DomainOperationResult { Success = true };
            var mirage = gameState.Mirage;

            lock (mirage.RoundSyncLock)
            {
                // 1. Set Phase = Recruit, clear ReadyPlayerIds, ClosingRecruitRoundNumber and ActiveCombatId
                mirage.RoundNumber = RoundNumber;
                mirage.ClosingRecruitRoundNumber = null;
                gameState.Phase = GamePhase.Recruit;
                mirage.ReadyPlayerIds.Clear();
                mirage.ActiveCombatId = null;

                // Update recruit timer timestamps atomically with round transition
                var now = DateTime.UtcNow;
                var timeoutSeconds = gameState.TurnTimeoutSeconds > 0
                    ? gameState.TurnTimeoutSeconds
                    : mirage.Rules.RecruitTimeoutSeconds;
                gameState.TurnTimeoutSeconds = timeoutSeconds;
                gameState.TurnStartedAtUtc = now;
                gameState.TurnEndsAtUtc = now.AddSeconds(timeoutSeconds);
            }

            // 2. Compute base gold for this round: min(HardGoldCap, 2 + RoundNumber)
            var roundGold = mirage.Rules.GetRoundBaseGold(RoundNumber);
            var decrementUpgrade = RoundNumber > 1;

            // 3, 5, 6. Reset gold, decrement upgrade cost, reset turn counters & combo, expire temporary buffs
            var p1Reset = gameState.Player1.ResetForMirageRoundStart(context, roundGold, decrementUpgrade);
            result.GeneratedEvents.AddRange(p1Reset.GeneratedEvents);

            var p2Reset = gameState.Player2.ResetForMirageRoundStart(context, roundGold, decrementUpgrade);
            result.GeneratedEvents.AddRange(p2Reset.GeneratedEvents);

            // 4. Refresh shop offers (if frozen, keep unpurchased offers, refill missing slots, and unfreeze)
            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            if (shopService != null)
            {
                shopService.RefreshShopForNewRound(gameState.Player1, mirage);
                shopService.RefreshShopForNewRound(gameState.Player2, mirage);
            }
            else
            {
                foreach (var player in new[] { gameState.Player1, gameState.Player2 })
                {
                    if (player.Mirage != null && player.Mirage.IsShopFrozen)
                    {
                        player.Mirage.IsShopFrozen = false;
                    }
                }
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            return $"幻境第 {RoundNumber} 轮招募阶段开始。";
        }
    }

    [GameEventMetadata(
        "幻境准备就绪请求",
        "当玩家在幻境招募阶段锁定阵容或招募超时时触发。",
        EventType.MirageSetReadyRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageSetReadyRequestedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起准备就绪的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("是否超时强制开战", "是否由招募倒计时超时触发。")]
        public bool IsForceTimeout { get; }

        [GameEventParameter("期望轮次", "超时触发时绑定的轮次编号。")]
        public int? ExpectedRoundNumber { get; }

        public bool TriggeredRoundAdvance { get; private set; }
        public Guid? ActivatedCombatId { get; private set; }
        public int ActivatedRoundNumber { get; private set; }
        public bool SeatLockedInTournament { get; private set; }
        public int LockedRoundNumber { get; private set; }

        public MirageSetReadyRequestedEvent(
            Guid playerId,
            bool isForceTimeout = false,
            int? expectedRoundNumber = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.MirageSetReadyRequested, eventId, timestamp)
        {
            PlayerId = playerId;
            IsForceTimeout = isForceTimeout;
            ExpectedRoundNumber = expectedRoundNumber;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var gameState = context.CurrentGameState;
            if (gameState.Mode != GameMode.Mirage || gameState.Mirage == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "MirageSetReadyRequestedEvent is only valid in Mirage mode."
                };
            }

            if (gameState.Phase != GamePhase.Recruit)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot lock lineup outside Recruit phase (Current: {gameState.Phase})."
                };
            }

            if (ExpectedRoundNumber.HasValue && gameState.Mirage.RoundNumber != ExpectedRoundNumber.Value)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Stale recruit timeout for round {ExpectedRoundNumber.Value} (Current: {gameState.Mirage.RoundNumber})."
                };
            }

            var player = gameState.GetPlayerState(PlayerId);
            if (player == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Player {PlayerId} not found in game."
                };
            }

            Source = TargetInfo.ForPlayer(player);

            if (gameState.IsMirageTournamentSeat)
            {
                var tournamentResult = new DomainOperationResult { Success = true };
                lock (gameState.Mirage.RoundSyncLock)
                {
                    if (gameState.Mirage.IsNextRoundGateClosed)
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = "正在等待本轮全房结算同步完成，暂不可操作。"
                        };
                    }

                    if (gameState.Mirage.RoundNumber == 1
                        && player.Mirage is { HasSelectedLeader: false }
                        && player.Mirage.LeaderCandidateCardIds.Count > 0)
                    {
                        var autoSelectResult = MirageCardRetirement.TryReplacePlayerMirageLeader(
                            context,
                            gameState,
                            player,
                            player.Mirage.LeaderCandidateCardIds[0],
                            out _);
                        if (!autoSelectResult.Success)
                        {
                            return autoSelectResult;
                        }
                        tournamentResult.GeneratedEvents.AddRange(autoSelectResult.GeneratedEvents);
                    }
                    else if (player.Mirage != null)
                    {
                        player.Mirage.HasSelectedLeader = true;
                        player.Mirage.LeaderCandidateCardIds.Clear();
                    }

                    gameState.Mirage.ReadyPlayerIds.Add(gameState.Player1.PlayerId);
                    SeatLockedInTournament = true;
                    LockedRoundNumber = gameState.Mirage.RoundNumber;
                }

                return tournamentResult;
            }

            bool bothReady;
            lock (gameState.Mirage.RoundSyncLock)
            {
                static void FinalizePendingLeaderSelection(PlayerState? p)
                {
                    if (p?.Mirage == null) return;
                    p.Mirage.HasSelectedLeader = true;
                    p.Mirage.LeaderCandidateCardIds.Clear();
                }

                if (IsForceTimeout)
                {
                    gameState.Mirage.ReadyPlayerIds.Add(gameState.Player1.PlayerId);
                    gameState.Mirage.ReadyPlayerIds.Add(gameState.Player2.PlayerId);
                    FinalizePendingLeaderSelection(gameState.Player1);
                    FinalizePendingLeaderSelection(gameState.Player2);
                }
                else
                {
                    gameState.Mirage.ReadyPlayerIds.Add(PlayerId);
                    FinalizePendingLeaderSelection(player);
                    var opponent = gameState.GetOpponentPlayerState(PlayerId);
                    if (opponent != null && opponent.IsAI)
                    {
                        gameState.Mirage.ReadyPlayerIds.Add(opponent.PlayerId);
                        FinalizePendingLeaderSelection(opponent);
                    }
                }

                bothReady = gameState.Mirage.ReadyPlayerIds.Contains(gameState.Player1.PlayerId)
                    && gameState.Mirage.ReadyPlayerIds.Contains(gameState.Player2.PlayerId);

                if (bothReady)
                {
                    gameState.Phase = GamePhase.AutoCombat;
                    var combatId = Guid.NewGuid();
                    gameState.Mirage.ActiveCombatId = combatId;
                    ActivatedCombatId = combatId;
                    ActivatedRoundNumber = gameState.Mirage.RoundNumber;
                    TriggeredRoundAdvance = true;
                }
            }

            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            if (IsForceTimeout)
            {
                return $"第 {gameState.Mirage?.RoundNumber ?? 1} 轮招募时间结束，自动锁定双方阵容。";
            }

            var player = gameState.GetPlayerState(PlayerId);
            return player != null
                ? $"{AnnouncementTokens.Player(player)}已锁定阵容。"
                : "玩家已锁定阵容。";
        }
    }

    [GameEventMetadata(
        "幻境刷新商店请求",
        "当玩家在幻境招募阶段手动刷新商店报价时触发。",
        EventType.MirageRefreshShopRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageRefreshShopRequestedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起刷新商店的玩家ID。")]
        public Guid PlayerId { get; }

        public MirageRefreshShopRequestedEvent(Guid playerId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.MirageRefreshShopRequested, eventId, timestamp)
        {
            PlayerId = playerId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out _, out var mirage, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var refreshCost = Math.Max(0, mirage.Rules.RefreshCost);
            if (player.CurrentPlayPoints < refreshCost)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Not enough gold to refresh shop (need {refreshCost}, have {player.CurrentPlayPoints})."
                };
            }

            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            if (shopService == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "IMirageShopService is not available." };
            }

            var pool = shopService.GetPoolUpToTier(player.Mirage!.TavernTier, mirage.Rules.MaxTavernTier);
            if (pool.Count == 0)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Mirage card pool is empty; cannot refresh shop."
                };
            }

            player.ApplyPlayPointsCurrentChange(player.CurrentPlayPoints - refreshCost);
            shopService.ForceRefreshShop(player, mirage);

            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            return player != null ? $"{AnnouncementTokens.Player(player)}刷新了商店。" : "刷新了商店。";
        }
    }

    [GameEventMetadata(
        "幻境升星请求",
        "当玩家在幻境招募阶段提升酒馆星级时触发。",
        EventType.MirageUpgradeTierRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageUpgradeTierRequestedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起升星的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("新星级", "升星后的酒馆星级。")]
        public int NewTier { get; private set; }

        public MirageUpgradeTierRequestedEvent(Guid playerId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.MirageUpgradeTierRequested, eventId, timestamp)
        {
            PlayerId = playerId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out _, out var mirage, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var playerMirage = player.Mirage!;
            if (playerMirage.TavernTier >= mirage.Rules.MaxTavernTier)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Already at maximum tavern tier ({mirage.Rules.MaxTavernTier})."
                };
            }

            var upgradeCost = Math.Max(0, playerMirage.UpgradeCost);
            if (player.CurrentPlayPoints < upgradeCost)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Not enough gold to upgrade tavern tier (need {upgradeCost}, have {player.CurrentPlayPoints})."
                };
            }

            player.ApplyPlayPointsCurrentChange(player.CurrentPlayPoints - upgradeCost);
            playerMirage.TavernTier += 1;
            NewTier = playerMirage.TavernTier;

            if (playerMirage.TavernTier >= mirage.Rules.MaxTavernTier)
            {
                playerMirage.UpgradeCost = 0;
            }
            else
            {
                playerMirage.UpgradeCost = mirage.Rules.GetBaseUpgradeCostForNextTier(playerMirage.TavernTier);
            }

            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            return player != null
                ? $"{AnnouncementTokens.Player(player)}将酒馆升级至 {NewTier} 星。"
                : $"酒馆升级至 {NewTier} 星。";
        }
    }

    [GameEventMetadata(
        "幻境冻结切换请求",
        "当玩家在幻境招募阶段切换商店全店冻结状态时触发。",
        EventType.MirageToggleFreezeRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageToggleFreezeRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起冻结切换的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("冻结状态", "切换后的全店冻结状态。")]
        public bool IsShopFrozen { get; private set; }

        public MirageToggleFreezeRequestedEvent(Guid playerId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.MirageToggleFreezeRequested, eventId, timestamp)
        {
            PlayerId = playerId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out _, out _, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            player.Mirage!.IsShopFrozen = !player.Mirage.IsShopFrozen;
            IsShopFrozen = player.Mirage.IsShopFrozen;

            return new DomainOperationResult { Success = true };
        }
    }

    [GameEventMetadata(
        "幻境购买报价请求",
        "当玩家在幻境招募阶段购买商店报价时触发。",
        EventType.MirageBuyOfferRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageBuyOfferRequestedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起购买的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("报价ID", "所购买的商店报价ID。")]
        public Guid OfferId { get; }

        [GameEventParameter("购入卡牌", "购买后实例化进手牌的卡牌。")]
        public CardInstance? PurchasedCard { get; private set; }

        public MirageBuyOfferRequestedEvent(Guid playerId, Guid offerId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.MirageBuyOfferRequested, eventId, timestamp)
        {
            PlayerId = playerId;
            OfferId = offerId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out var gameState, out _, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var offerIndex = player.Mirage!.ShopOffers.FindIndex(o => o.OfferId == OfferId);
            if (offerIndex < 0)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Shop offer {OfferId} not found."
                };
            }

            var offer = player.Mirage.ShopOffers[offerIndex];
            if (player.Hand.Count >= ShadowverseBeyond.Server.Domain.Constants.GameConstants.Hand.MaxHandSize)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Hand is full."
                };
            }

            var buyCost = Math.Max(0, offer.BuyCost);
            if (player.CurrentPlayPoints < buyCost)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Not enough gold to buy offer (need {buyCost}, have {player.CurrentPlayPoints})."
                };
            }

            var cardFactory = context.ServiceProvider.GetService<ICardFactory>();
            if (cardFactory == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ICardFactory is not available." };
            }

            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            var offerDef = GameDataProvider.Instance?.GetCardDefinition(offer.CardDefinitionId);
            if (offerDef == null
                || !offerDef.IsMirageCard
                || (shopService != null && shopService.GetCatalogEntry(offer.CardDefinitionId) == null))
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Card definition {offer.CardDefinitionId} does not belong to the Mirage card pool."
                };
            }

            player.Mirage.ShopOffers.RemoveAt(offerIndex);
            player.ApplyPlayPointsCurrentChange(player.CurrentPlayPoints - buyCost);

            var purchasedCard = cardFactory.CreateAndRegisterNewCard(gameState, offer.CardDefinitionId, player.PlayerId, CardZone.Hand);
            purchasedCard.IsMirageRecruited = true;
            purchasedCard.AddMiragePoolHolding(offer.CardDefinitionId, 1);
            purchasedCard.CostOverride = 0;
            PurchasedCard = purchasedCard;
            Target.Add(TargetInfo.ForCard(purchasedCard));

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new HandUpdatedEvent(player, player.Hand.ToList()));

            var tripleService = context.ServiceProvider.GetService<IMirageTripleService>();
            if (tripleService != null)
            {
                result.GeneratedEvents.AddRange(tripleService.CreatePendingTripleEvents(player, gameState.Mirage?.RoundNumber));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            if (player == null || PurchasedCard == null) return string.Empty;
            return $"{AnnouncementTokens.Player(player)}招募了{AnnouncementTokens.Card(PurchasedCard)}。";
        }
    }

    [GameEventMetadata(
        "随从出售完成",
        "当幻境随从完成出售并退场返还金币后触发（供词条监听）。",
        EventType.CardSoldCompleted,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel)]
    public class CardSoldCompletedEvent : GameEventBase
    {
        [GameEventParameter("出售玩家", "执行出售的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? SellingPlayer => Source?.Player;

        [GameEventParameter("被售随从快照", "被出售随从离场前的完整快照。")]
        public CardInstanceSnapshot SoldCardSnapshot { get; }

        public CardSoldCompletedEvent(PlayerState sellingPlayer, CardInstanceSnapshot soldCardSnapshot, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CardSoldCompleted, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(sellingPlayer);
            SoldCardSnapshot = soldCardSnapshot ?? throw new ArgumentNullException(nameof(soldCardSnapshot));
        }
    }

    [GameEventMetadata(
        "随从出售",
        "当玩家在幻境招募阶段出售场上随从时触发。",
        EventType.CardSold,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel)]
    public class CardSoldEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起出售的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("随从ID", "被出售的场上随从实例ID。")]
        public Guid CardInstanceId { get; }

        [GameEventParameter("被售随从快照", "被出售随从离场前的完整快照。")]
        public CardInstanceSnapshot? SoldCardSnapshot { get; private set; }

        public CardSoldEvent(Guid playerId, Guid cardInstanceId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CardSold, eventId, timestamp)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out _, out var mirage, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var cardToSell = player.Field.FirstOrDefault(
                c => c != null && c.IsFollower && c.CurrentZone == CardZone.Field && c.InstanceId == CardInstanceId);
            if (cardToSell == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Follower {CardInstanceId} not found on player {PlayerId}'s field."
                };
            }

            Target.Add(TargetInfo.ForCard(cardToSell));
            SoldCardSnapshot = CardSnapshotCapture.Capture(cardToSell);

            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            shopService?.ReleaseCardInstancePoolHoldings(context.CurrentGameState, cardToSell);

            var retireResult = MirageCardRetirement.RetireCardFromPlay(cardToSell, player, context);
            if (!retireResult.Success)
            {
                return retireResult;
            }

            var refund = Math.Max(0, mirage.Rules.SellRefund);
            player.ApplyPlayPointsCurrentChange(player.CurrentPlayPoints + refund);

            var auraRefreshResult = MirageCardRetirement.RefreshAllBoardAuras(context, "mirage_card_sold");
            if (!auraRefreshResult.Success)
            {
                return auraRefreshResult;
            }

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.AddRange(retireResult.GeneratedEvents);
            result.GeneratedEvents.AddRange(auraRefreshResult.GeneratedEvents);
            result.GeneratedEvents.Add(new CardSoldCompletedEvent(player, SoldCardSnapshot));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            if (player == null || SoldCardSnapshot == null) return string.Empty;
            return $"{AnnouncementTokens.Player(player)}出售了《{SoldCardSnapshot.Name}》，获得 1 金币。";
        }
    }

    [GameEventMetadata(
        "战场站位重排",
        "当玩家在幻境招募阶段调整场上随从左右站位顺序时触发。",
        EventType.FieldReordered,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class FieldReorderedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起重排的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("新站位顺序", "重排后的场上随从实例ID列表。")]
        public IReadOnlyList<Guid> OrderedCardInstanceIds { get; }

        public FieldReorderedEvent(Guid playerId, IReadOnlyList<Guid> orderedCardInstanceIds, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.FieldReordered, eventId, timestamp)
        {
            PlayerId = playerId;
            OrderedCardInstanceIds = orderedCardInstanceIds ?? Array.Empty<Guid>();
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out _, out _, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var fieldUnits = player.Field.Where(c => c != null && !c.IsLeader).ToList();
            if (OrderedCardInstanceIds.Count != fieldUnits.Count)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Reorder list count ({OrderedCardInstanceIds.Count}) does not match field unit count ({fieldUnits.Count})."
                };
            }

            var unitById = fieldUnits.ToDictionary(c => c.InstanceId);
            var distinctOrdered = OrderedCardInstanceIds.Distinct().ToList();
            if (distinctOrdered.Count != fieldUnits.Count || distinctOrdered.Any(id => !unitById.ContainsKey(id)))
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Reorder list must contain the exact set of current field unit IDs without duplicates."
                };
            }

            var reordered = distinctOrdered.Select(id => unitById[id]).ToList();
            player.Field.Clear();
            for (int i = 0; i < reordered.Count; i++)
            {
                reordered[i].ZoneIndex = i;
                player.Field.Add(reordered[i]);
            }

            var auraRefreshResult = MirageCardRetirement.RefreshAllBoardAuras(context, "mirage_field_reordered");
            if (!auraRefreshResult.Success)
            {
                return auraRefreshResult;
            }

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.AddRange(auraRefreshResult.GeneratedEvents);
            return result;
        }
    }

    [GameEventMetadata(
        "幻境三连合成",
        "当玩家在幻境招募阶段集齐3张同组非三连随从并合成为1张双倍基础身材三连随从时触发。",
        EventType.MirageTripleSynthesized,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageTripleSynthesizedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "完成三连合成的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("三连归组ID", "本次三连合成对应的卡牌归组ID。")]
        public string TripleGroupId { get; }

        [GameEventParameter("材料卡牌ID列表", "参与三连合成的3张材料随从实例ID。")]
        public IReadOnlyList<Guid> MaterialCardInstanceIds { get; }

        [GameEventParameter("所属轮次", "生成该三连合成事件时的幻境轮次编号。")]
        public int? ExpectedRoundNumber { get; }

        [GameEventParameter("合成三连卡", "合成后放入手牌的三连卡牌实例。")]
        public CardInstance? SynthesizedCard { get; private set; }

        public MirageDiscoverTicket? GrantedTicket { get; private set; }

        public MirageTripleSynthesizedEvent(
            Guid playerId,
            string tripleGroupId,
            IReadOnlyList<Guid>? materialCardInstanceIds = null,
            int? expectedRoundNumber = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.MirageTripleSynthesized, eventId, timestamp)
        {
            PlayerId = playerId;
            TripleGroupId = tripleGroupId ?? string.Empty;
            MaterialCardInstanceIds = materialCardInstanceIds ?? Array.Empty<Guid>();
            ExpectedRoundNumber = expectedRoundNumber;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateDerivedTripleAction(context, PlayerId, ExpectedRoundNumber, out _, out _, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var tripleService = context.ServiceProvider.GetService<IMirageTripleService>();
            if (tripleService == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "IMirageTripleService is not available."
                };
            }

            var result = tripleService.ExecuteTripleSynthesis(
                player,
                TripleGroupId,
                MaterialCardInstanceIds,
                context,
                out var synthesizedCard,
                out var grantedTicket);
            if (!result.Success)
            {
                return result;
            }

            SynthesizedCard = synthesizedCard;
            GrantedTicket = grantedTicket;
            if (synthesizedCard != null)
            {
                Target.Add(TargetInfo.ForCard(synthesizedCard));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            if (player == null || SynthesizedCard == null) return string.Empty;
            var tierText = GrantedTicket != null ? $"，并获得 1 张 {GrantedTicket.TargetTier} 星发现凭据" : string.Empty;
            return $"{AnnouncementTokens.Player(player)}完成了三连合成：{AnnouncementTokens.Card(SynthesizedCard)}{tierText}！";
        }
    }

    [GameEventMetadata(
        "幻境兑现凭据请求",
        "当玩家在幻境招募阶段使用三连奖励的发现凭据选择1张高星随从加入手牌时触发。",
        EventType.MirageRedeemTicketRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageRedeemTicketRequestedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起兑现凭据的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("凭据ID", "被兑现的发现凭据ID。")]
        public Guid TicketId { get; }

        [GameEventParameter("所选卡牌定义ID", "玩家从候选中选择的卡牌定义ID。")]
        public int ChosenCardDefinitionId { get; }

        [GameEventParameter("获得卡牌", "兑现凭据后放入手牌的卡牌实例。")]
        public CardInstance? DiscoveredCard { get; private set; }

        public MirageRedeemTicketRequestedEvent(
            Guid playerId,
            Guid ticketId,
            int chosenCardDefinitionId,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.MirageRedeemTicketRequested, eventId, timestamp)
        {
            PlayerId = playerId;
            TicketId = ticketId;
            ChosenCardDefinitionId = chosenCardDefinitionId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out var gameState, out _, out var player, out var error))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = error };
            }

            Source = TargetInfo.ForPlayer(player);
            var ticketIndex = player.Mirage!.PendingTickets.FindIndex(t => t != null && t.TicketId == TicketId);
            if (ticketIndex < 0)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Mirage discover ticket {TicketId} not found."
                };
            }

            var ticket = player.Mirage.PendingTickets[ticketIndex];
            if (!ticket.CandidateCardDefinitionIds.Contains(ChosenCardDefinitionId))
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Card definition {ChosenCardDefinitionId} is not a valid candidate for ticket {TicketId}."
                };
            }

            if (player.Hand.Count >= ShadowverseBeyond.Server.Domain.Constants.GameConstants.Hand.MaxHandSize)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Hand is full."
                };
            }

            var cardFactory = context.ServiceProvider.GetService<ICardFactory>();
            if (cardFactory == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ICardFactory is not available." };
            }

            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            var chosenDef = GameDataProvider.Instance?.GetCardDefinition(ChosenCardDefinitionId);
            if (chosenDef == null
                || !chosenDef.IsMirageCard
                || (shopService != null && shopService.GetCatalogEntry(ChosenCardDefinitionId) == null))
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Card definition {ChosenCardDefinitionId} does not belong to the Mirage card pool."
                };
            }

            player.Mirage.PendingTickets.RemoveAt(ticketIndex);

            bool skippedChosen = false;
            foreach (var candidateId in ticket.CandidateCardDefinitionIds)
            {
                if (!skippedChosen && candidateId == ChosenCardDefinitionId)
                {
                    skippedChosen = true;
                    continue;
                }

                shopService?.ReturnCopiesToPool(gameState.Mirage, candidateId, 1);
            }

            var discoveredCard = cardFactory.CreateAndRegisterNewCard(gameState, ChosenCardDefinitionId, player.PlayerId, CardZone.Hand);
            discoveredCard.IsMirageRecruited = true;
            discoveredCard.AddMiragePoolHolding(ChosenCardDefinitionId, 1);
            discoveredCard.CostOverride = 0;
            DiscoveredCard = discoveredCard;
            Target.Add(TargetInfo.ForCard(discoveredCard));

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new HandUpdatedEvent(player, player.Hand.ToList()));

            var tripleService = context.ServiceProvider.GetService<IMirageTripleService>();
            if (tripleService != null)
            {
                result.GeneratedEvents.AddRange(tripleService.CreatePendingTripleEvents(player, gameState.Mirage?.RoundNumber));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            if (player == null || DiscoveredCard == null) return string.Empty;
            return $"{AnnouncementTokens.Player(player)}兑现发现凭据，获得了{AnnouncementTokens.Card(DiscoveredCard)}。";
        }
    }

    [GameEventMetadata(
        "幻境选择主战者请求",
        "当玩家在幻境开局第1轮招募阶段从3张候选中选择主战者时触发。",
        EventType.MirageSelectLeaderRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MirageSelectLeaderRequestedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("玩家ID", "发起选择主战者的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("所选主战者卡牌定义ID", "玩家选择的幻境主战者卡牌定义ID。")]
        public int LeaderCardDefinitionId { get; }

        [GameEventParameter("新主战者实例", "替换后挂载在玩家身上的新主战者卡牌实例。")]
        public CardInstance? SelectedLeader { get; private set; }

        public MirageSelectLeaderRequestedEvent(
            Guid playerId,
            int leaderCardDefinitionId,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.MirageSelectLeaderRequested, eventId, timestamp)
        {
            PlayerId = playerId;
            LeaderCardDefinitionId = leaderCardDefinitionId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var currentGameState = context.CurrentGameState;
            if (currentGameState.Mode != GameMode.Mirage || currentGameState.Mirage == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Action is only valid in Mirage mode." };
            }

            lock (currentGameState.Mirage.RoundSyncLock)
            {
                if (!MirageRecruitValidation.TryValidateRecruitAction(context, PlayerId, out var gameState, out var mirage, out var player, out var error))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = error };
                }

                if (mirage.RoundNumber != 1)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "幻境主战者仅可在第 1 轮招募阶段选择。"
                    };
                }

                if (player.Mirage == null)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "玩家幻境状态不存在。"
                    };
                }

                if (player.Mirage.HasSelectedLeader)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "本局已选择过幻境主战者，不可重复选择。"
                    };
                }

                if (!player.Mirage.LeaderCandidateCardIds.Contains(LeaderCardDefinitionId))
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"卡牌定义 {LeaderCardDefinitionId} 不在当前可选主战者候选列表中。"
                    };
                }

                Source = TargetInfo.ForPlayer(player);
                var replaceResult = MirageCardRetirement.TryReplacePlayerMirageLeader(
                    context,
                    gameState,
                    player,
                    LeaderCardDefinitionId,
                    out var createdLeader);
                if (!replaceResult.Success || createdLeader == null)
                {
                    return replaceResult;
                }

                SelectedLeader = createdLeader;
                Target.Add(TargetInfo.ForCard(createdLeader));
                return replaceResult;
            }
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = gameState.GetPlayerState(PlayerId);
            if (player == null || SelectedLeader == null) return string.Empty;
            return $"{AnnouncementTokens.Player(player)}选择了幻境主战者：{AnnouncementTokens.Card(SelectedLeader)}。";
        }
    }
}
