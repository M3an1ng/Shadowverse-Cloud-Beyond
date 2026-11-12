using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A trigger event that represents a successful Act activation for a specific card instance.
    /// Abilities can listen to <see cref="EventType.Act"/> and use relation filters (e.g. SourceSelf) to scope to the card.
    /// </summary>
    [GameEventMetadata(
        "启动",
        "当一张卡牌成功发动启动能力时触发的事件。",
        EventType.Act,
        AbilityEditorCategories.BaseActionId,
        AbilityEditorCategories.BaseActionLabel)]
    public sealed class ActInvokedEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("启动玩家", "发动启动能力的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? ActPlayer => Source?.Player;

        [GameEventParameter("启动卡牌", "发动启动能力的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? ActCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("支付费用", "本次启动实际消耗的费用。")]
        public int PaidCost { get; }

        [GameEventParameter("触发能力ID", "本次启动所对应的能力运行时ID。")]
        public Guid? TriggeredAbilityRuntimeId { get; }

        public ActInvokedEvent(PlayerState requestingPlayer, CardInstance sourceCard, int paidCost, Guid? triggeredAbilityRuntimeId = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Act, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(requestingPlayer);
            Target.Add(TargetInfo.ForCard(sourceCard));
            PaidCost = paidCost;
            TriggeredAbilityRuntimeId = triggeredAbilityRuntimeId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var card = ActCard;
            var player = ActPlayer;
            if (card == null || player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: player or card is null." };
            }

            if (card.OwnerPlayerId != player.PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: card owner mismatch." };
            }

            if (context.CurrentGameState.IsCombatSandbox)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: Act cannot be used during auto-combat." };
            }

            DomainOperationResult ExecuteCoreActCommit()
            {
                if (card.CurrentZone != Dtos.Enums.CardZone.Field)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: card is no longer on the field." };
                }

                if (card.HasUsedActThisTurn())
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: card has already used Act this turn." };
                }

                var actCost = PaidCost;

                var spendablePp = player.GetSpendablePlayPoints();
                if (actCost > spendablePp)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Not enough PP to use Act. Required: {actCost}, Available: {spendablePp}"
                    };
                }

                if (actCost > 0)
                {
                    var ppResult = player.SpendPlayPoints(actCost);
                    if (!ppResult.Success)
                    {
                        return ppResult;
                    }
                    result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
                }

                card.MarkActUsed();
                result.Merge(global::ShadowverseBeyond.Server.Domain.Buffs.OneUseModifiers.Consume(card, AbilityKeyword.Act, context));

                result.Success = true;
                result.GeneratedEvents.Add(new ActStateUpdatedEvent(card, card.ActsThisTurn, card.MaxActsPerTurn));
                return result;
            }

            if (context.CurrentGameState.Mode == Dtos.Enums.GameMode.Mirage)
            {
                var mirage = context.CurrentGameState.Mirage;
                if (mirage == null)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: Mirage state is missing." };
                }

                lock (mirage.RoundSyncLock)
                {
                    if (context.CurrentGameState.Phase != Dtos.Enums.GamePhase.Recruit
                        || mirage.ReadyPlayerIds.Contains(player.PlayerId)
                        || mirage.ClosingRecruitRoundNumber == mirage.RoundNumber)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: Mirage Recruit phase is already locked or closing." };
                    }

                    var isActiveLeader = card.IsLeader && player.Leader?.InstanceId == card.InstanceId;
                    var isFieldUnit = !card.IsLeader && player.Field.Any(c => c.InstanceId == card.InstanceId);
                    if (!isActiveLeader && !isFieldUnit)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: Only your active Mirage Leader or field cards can use Act during Recruit phase." };
                    }

                    return ExecuteCoreActCommit();
                }
            }

            return ExecuteCoreActCommit();
        }

        public string GetAnnouncement(GameState gameState)
        {
            var card = ActCard;
            var player = ActPlayer;
            if (card == null)
            {
                return string.Empty;
            }

            return player != null
                ? $"{AnnouncementTokens.Player(player)}启动{AnnouncementTokens.Card(card)}。"
                : $"{AnnouncementTokens.Card(card)}启动了。";
        }
    }
}
