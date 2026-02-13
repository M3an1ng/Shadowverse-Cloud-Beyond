using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("战斗破坏结算", "当战斗伤害处理完毕后统一结算战斗相关破坏。", EventType.CombatDestroyResolution, visibility: EventMetadataVisibility.System)]
    public class CombatDestroyResolutionEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("攻击者", "发起攻击的单位。", DslDisplayTypeName = "事件源")]
        public CardInstance? Attacker => Source?.Card;

        [GameEventParameter("防御者", "被攻击的单位。", DslDisplayTypeName = "事件目标")]
        public CardInstance? Defender => Target.FirstOrDefault()?.Card;

        [GameEventParameter("攻击者具备必杀", "战斗伤害生成时攻击者是否拥有必杀。", Scope = GameEventParameterScope.Input)]
        public bool AttackerHadBane { get; }

        [GameEventParameter("防御者具备必杀", "战斗伤害生成时防御者是否拥有必杀。", Scope = GameEventParameterScope.Input)]
        public bool DefenderHadBane { get; }

        [GameEventParameter("是否存在反击伤害", "本次战斗是否存在防御者对攻击者的反击伤害。", Scope = GameEventParameterScope.Input)]
        public bool HasCounterAttack { get; }

        public CombatDestroyResolutionEvent(
            CardInstance attacker,
            CardInstance defender,
            bool attackerHadBane,
            bool defenderHadBane,
            bool hasCounterAttack,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.CombatDestroyResolution, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(attacker);
            Target.Add(TargetInfo.ForCard(defender));
            AttackerHadBane = attackerHadBane;
            DefenderHadBane = defenderHadBane;
            HasCounterAttack = hasCounterAttack;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (Attacker == null || Defender == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CombatDestroyResolutionEvent.Commit: attacker or defender is null." };
            }

            var result = new DomainOperationResult { Success = true };
            ResolveDefenderDestroy(result);

            if (HasCounterAttack)
            {
                ResolveAttackerDestroy(result);
            }

            return result;
        }

        private void ResolveDefenderDestroy(DomainOperationResult result)
        {
            var attacker = Attacker;
            var defender = Defender;
            if (attacker == null || defender == null)
            {
                return;
            }

            if (!IsFollowerOnField(defender))
            {
                return;
            }

            if (defender.CurrentHealth <= 0)
            {
                result.GeneratedEvents.Add(new DestroyEvent(defender, attacker, EventReasonSource.System));
                return;
            }

            if (AttackerHadBane)
            {
                result.GeneratedEvents.Add(new DestroyEvent(defender, attacker, EventReasonSource.Ability));
            }
        }

        private void ResolveAttackerDestroy(DomainOperationResult result)
        {
            var attacker = Attacker;
            var defender = Defender;
            if (attacker == null || defender == null)
            {
                return;
            }

            if (!IsFollowerOnField(attacker))
            {
                return;
            }

            if (attacker.CurrentHealth <= 0)
            {
                result.GeneratedEvents.Add(new DestroyEvent(attacker, defender, EventReasonSource.System));
                return;
            }

            if (DefenderHadBane)
            {
                result.GeneratedEvents.Add(new DestroyEvent(attacker, defender, EventReasonSource.Ability));
            }
        }

        private static bool IsFollowerOnField(CardInstance card)
        {
            return card.CardType == CardType.Follower && card.CurrentZone == CardZone.Field;
        }
    }
}
