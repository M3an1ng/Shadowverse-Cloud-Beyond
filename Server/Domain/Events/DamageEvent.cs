using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using System;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public enum DamageKind
    {
        Unknown = 0,
        Combat = 1,
        Ability = 2
    }

    /// <summary>
    /// Event triggered when damage is dealt to a target.
    /// </summary>
    [GameEventMetadata("伤害", "当对目标造成伤害时触发的事件。", EventType.Damage)]
    public class DamageEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("伤害值", "造成的伤害数值。")]
        public int Damage { get; }

        [GameEventParameter("实际伤害值", "实际造成的伤害数值（考虑护盾、超进化免伤等）。")]
        public int ActualDamage { get; private set; }

        [GameEventParameter("伤害来源", "造成伤害的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? DamageSource => Source?.Card;

        [GameEventParameter("伤害目标", "受到伤害的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? DamageTarget => Target.FirstOrDefault()?.Card;

        [GameEventParameter("伤害类型", "伤害来源类型（战斗/能力等）。")]
        public DamageKind Kind { get; }

        [GameEventParameter("反击伤害", "是否为反击（被动）造成的伤害。")]
        public bool IsCounterAttack { get; }

        public DamageEvent(CardInstance target, int damage, CardInstance source)
            : this(target, damage, source, DamageKind.Unknown, null, null, false)
        {
        }

        public DamageEvent(CardInstance target, int damage, CardInstance source, Guid? eventId, DateTime? timestamp)
            : this(target, damage, source, DamageKind.Unknown, eventId, timestamp, false)
        {
        }

        public DamageEvent(CardInstance target, int damage, CardInstance source, DamageKind kind, Guid? eventId = null, DateTime? timestamp = null, bool isCounterAttack = false)
            : base(EventType.Damage, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target));
            Damage = damage;
            ActualDamage = damage;
            Source = TargetInfo.ForCard(source);
            Kind = kind;
            IsCounterAttack = isCounterAttack;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (DamageTarget == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "DamageEvent target card is null." };
            }

            var result = new DomainOperationResult { Success = true };
            var card = DamageTarget;

            int oldHealth = card.CurrentHealth;
            int intendedDamage = this.Damage;
            var targetKeywords = context.BuffService.GetEffectiveKeywords(card, context);
            var hasShield = targetKeywords.Contains(CardKeyword.Shield);
            var rawDamage = hasShield ? 0 : Math.Max(0, intendedDamage);
            int newHealth = oldHealth - rawDamage;
            ActualDamage = rawDamage;

            if (hasShield)
            {
                // Consume shield on first incoming positive damage.
                result.GeneratedEvents.Add(new KeywordRemovedEvent(CardKeyword.Shield, card));
            }

            // Generate the atomic HealthChangedEvent; it will apply state in its Commit
            result.GeneratedEvents.Add(new HealthChangedEvent(card, oldHealth, newHealth, -rawDamage));

            if (Kind == DamageKind.Combat && DamageSource != null)
            {
                var sourceKeywords = context.BuffService.GetEffectiveKeywords(DamageSource, context);

                if (DamageSource.IsSuperEvolved && card.IsFollower && newHealth <= 0)
                {
                    var defenderOwner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (defenderOwner?.Leader != null)
                    {
                        result.GeneratedEvents.Add(new DamageEvent(defenderOwner.Leader, 1, DamageSource, DamageKind.Ability));
                    }
                }


                if (sourceKeywords.Contains(CardKeyword.Drain) && !IsCounterAttack)
                {
                    var owner = context.CurrentGameState.GetPlayerState(DamageSource.OwnerPlayerId);
                    if (owner?.Leader != null)
                    {
                        result.GeneratedEvents.Add(new HealEvent(owner.Leader, rawDamage, DamageSource));
                    }
                }

                if (sourceKeywords.Contains(CardKeyword.Bane) && card.IsFollower && newHealth > 0)
                {
                    result.GeneratedEvents.Add(new DestroyEvent(card, DamageSource, "DestroyedByBane"));
                }
            }

            // If a leader reaches 0 health, the game ends (leaders are not destroyed as normal cards).
            if (card.IsLeader && newHealth <= 0)
            {
                var loserId = card.OwnerPlayerId;
                var winnerId = DamageSource?.OwnerPlayerId;
                if (!winnerId.HasValue || winnerId.Value == loserId)
                {
                    var opponent = context.CurrentGameState.GetOpponentPlayerState(loserId);
                    winnerId = opponent?.PlayerId;
                }

                if (winnerId.HasValue && winnerId.Value != Guid.Empty)
                {
                    result.GeneratedEvents.Add(new GameEndEvent(winnerId.Value, loserId, "Leader health reached zero"));
                }
            }
            // If a non-leader reaches 0 health, schedule destruction after health is applied.
            else if (newHealth <= 0)
            {
                var sourceCard = DamageSource ?? card; // fallback to self if missing
                result.GeneratedEvents.Add(new DestroyEvent(card, sourceCard, "DestroyedByDamage"));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var target = DamageTarget;
            if (target == null)
            {
                return string.Empty;
            }

            var amount = Math.Max(0, ActualDamage);
            var source = DamageSource;
            return source != null
                ? $"{AnnouncementTokens.Card(source)}对{AnnouncementTokens.Card(target)}造成 {AnnouncementTokens.Number(amount)} 点伤害。"
                : $"{AnnouncementTokens.Card(target)}受到 {AnnouncementTokens.Number(amount)} 点伤害。";
        }
    }
}
