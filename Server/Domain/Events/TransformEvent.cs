using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions; // IGameDataProvider
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 变形事件（唯一）：既可作为通知型事件（变形完成后广播），也可作为可提交事件（驱动实际变形）。
    /// 语义：变形是效果的结果，不是玩家直接动作。
    /// </summary>
    [GameEventMetadata("变形", "当一张卡牌变形为另一张卡牌时触发的事件。", EventType.Transform)]
    public class TransformEvent : GameEventBase, ICommittableEvent
    {
        /// <summary>
        /// 目标卡牌定义ID（提交场景下：要变形成的定义ID；通知场景下：已变形后的定义ID）。
        /// </summary>
        [GameEventParameter("新卡牌定义ID", "变形后的卡牌定义ID。")]
        public int NewCardDefinitionId { get; }

        [GameEventParameter("变形的卡牌", "发生变形的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? TransformingCard => Source?.Card;

        /// <summary>
        /// 通知用构造：在卡面已变更后，用当前卡面的定义ID作为 NewCardDefinitionId。
        /// 供 CardInstance.Transform 内部在完成变形后派发。
        /// </summary>
        public TransformEvent(CardInstance transformedCard, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Transform, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(transformedCard);
            NewCardDefinitionId = transformedCard.BaseDefinitionId;
        }

        /// <summary>
        /// 提交用构造：携带目标定义ID，由 Commit 执行实体实际变形。
        /// </summary>
        public TransformEvent(CardInstance targetCard, int newDefinitionId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Transform, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(targetCard);
            NewCardDefinitionId = newDefinitionId;
        }

        /// <summary>
        /// 执行变形：解析目标定义并调用 CardInstance.Transform。
        /// CardInstance.Transform 将负责注销/重建能力，并派发通知型 TransformEvent。
        /// </summary>
        public DomainOperationResult Commit(GameContext context)
        {
            var card = TransformingCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TransformEvent.Commit: Source card is null." };
            }

            // Notification events are emitted after a transform has already been applied.
            // If the card already matches the requested definition, skip re-transforming to avoid recursion.
            if (card.BaseDefinitionId == NewCardDefinitionId)
            {
                return new DomainOperationResult { Success = true };
            }

            var data = context.ServiceProvider.GetRequiredService<IGameDataProvider>();
            var def = data.GetCardDefinition(NewCardDefinitionId);
            if (def == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"TransformEvent.Commit: CardDefinition {NewCardDefinitionId} not found." };
            }

            var transformResult = card.Transform(def, context);
            if (!transformResult.Success)
            {
                return transformResult;
            }

            return transformResult;
        }
    }
}
