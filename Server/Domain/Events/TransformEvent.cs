using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Services.Abstractions; // IGameDataProvider

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

        [GameEventParameter("变形来源", "导致变形的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? TransformSource => Source?.Card;

        [GameEventParameter("变形的卡牌", "发生变形的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TransformingCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("变形来源快照", "生成变形事件时记录的变形来源快照。", Scope = GameEventParameterScope.Output)]
        public CardInstanceSnapshot? TransformSourceSnapshot { get; }

        [GameEventParameter("变形目标快照", "生成变形事件时记录的变形目标快照。", Scope = GameEventParameterScope.Output)]
        public CardInstanceSnapshot? TransformingCardSnapshot { get; }

        internal bool CopySourceState { get; }
        internal CardInstance.CardRuntimeStateSnapshot? CopySourceStateSnapshot { get; }
        internal IReadOnlyList<Buff> CopySourceBuffs { get; } = Array.Empty<Buff>();

        /// <summary>
        /// 通知用构造：在卡面已变更后，用当前卡面的定义ID作为 NewCardDefinitionId。
        /// 供 CardInstance.Transform 内部在完成变形后派发。
        /// </summary>
        public TransformEvent(CardInstance transformedCard, CardInstance? sourceCard = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Transform, eventId, timestamp)
        {
            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
                TransformSourceSnapshot = CardSnapshotCapture.Capture(sourceCard);
            }

            Target.Add(TargetInfo.ForCard(transformedCard));
            TransformingCardSnapshot = CardSnapshotCapture.Capture(transformedCard);
            NewCardDefinitionId = transformedCard.BaseDefinitionId;
        }

        /// <summary>
        /// 提交用构造：携带目标定义ID，由 Commit 执行实体实际变形。
        /// </summary>
        public TransformEvent(CardInstance targetCard, int newDefinitionId, CardInstance? sourceCard = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Transform, eventId, timestamp)
        {
            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
                TransformSourceSnapshot = CardSnapshotCapture.Capture(sourceCard);
            }

            Target.Add(TargetInfo.ForCard(targetCard));
            TransformingCardSnapshot = CardSnapshotCapture.Capture(targetCard);
            NewCardDefinitionId = newDefinitionId;
        }

        /// <summary>
        /// 提交用构造：将目标卡牌变形为复制来源卡牌的精确复制，保留目标实例本身。
        /// </summary>
        public TransformEvent(CardInstance targetCard, CardInstance copySourceCard, IReadOnlyList<Buff>? copySourceBuffs = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Transform, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(copySourceCard);
            TransformSourceSnapshot = CardSnapshotCapture.Capture(copySourceCard);

            Target.Add(TargetInfo.ForCard(targetCard));
            TransformingCardSnapshot = CardSnapshotCapture.Capture(targetCard);
            NewCardDefinitionId = copySourceCard.CurrentDefinitionId;
            CopySourceState = true;
            CopySourceStateSnapshot = copySourceCard.CaptureRuntimeStateSnapshot();
            CopySourceBuffs = copySourceBuffs != null
                ? copySourceBuffs.ToList()
                : Array.Empty<Buff>();
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
                return new DomainOperationResult { Success = false, ErrorMessage = "TransformEvent.Commit: target card is null." };
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

            if (CopySourceState)
            {
                if (CopySourceStateSnapshot == null)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "TransformEvent.Commit: copy source snapshot is missing." };
                }

                card.CopyRuntimeStateFrom(CopySourceStateSnapshot);

                if (CopySourceBuffs.Count > 0)
                {
                    var buffFactory = context.ServiceProvider.GetRequiredService<IBuffFactory>();
                    foreach (var buff in CopySourceBuffs)
                    {
                        var buffCopy = buffFactory.CreateBuffCopy(buff, card);
                        context.BuffManager.RegisterBuffInstance(buffCopy, context.CurrentGameState);
                    }
                }

                context.BuffService.InvalidateDerivedState(context.CurrentGameState, card.InstanceId);
            }

            return transformResult;
        }
    }
}
