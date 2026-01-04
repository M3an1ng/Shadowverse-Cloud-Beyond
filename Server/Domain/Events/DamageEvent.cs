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

        [GameEventParameter("伤害来源", "造成伤害的卡牌。")]
        public CardInstance? DamageSource => Source?.Card;

        [GameEventParameter("伤害目标", "受到伤害的卡牌。")]
        public CardInstance? DamageTarget => Target.FirstOrDefault()?.Card;

        [GameEventParameter("伤害类型", "伤害来源类型（战斗/能力等）。")]
        public DamageKind Kind { get; }

        public DamageEvent(CardInstance target, int damage, CardInstance source)
            : this(target, damage, source, DamageKind.Unknown)
        {
        }

        public DamageEvent(CardInstance target, int damage, CardInstance source, DamageKind kind, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Damage, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target));
            Damage = damage;
            Source = TargetInfo.ForCard(source);
            Kind = kind;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (DamageTarget == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "DamageEvent target card is null." };
            }

            var result = new DomainOperationResult { Success = true };
            var card = DamageTarget;

            if (card.IsSuperEvolved && card.IsFollower && card.OwnerPlayerId == context.CurrentGameState.CurrentPlayerId)
            {
                return result;
            }

            int oldHealth = card.CurrentHealth;
            int intendedDamage = this.Damage;
            var targetKeywords = context.BuffService.GetEffectiveKeywords(card, context);
            var hasShield = intendedDamage >= 0 && targetKeywords.Contains(CardKeyword.Shield);
            int actualDamage = hasShield ? 0 : Math.Max(0, Math.Min(intendedDamage, oldHealth));
            int newHealth = Math.Max(0, oldHealth - actualDamage);

            if (hasShield)
            {
                // Consume shield on first incoming positive damage.
                result.GeneratedEvents.Add(new KeywordRemovedEvent(CardKeyword.Shield, card));
            }

            // Generate the atomic HealthChangedEvent; it will apply state in its Commit
            if (actualDamage > 0)
            {
                result.GeneratedEvents.Add(new HealthChangedEvent(card, oldHealth, newHealth, -actualDamage));
            }

            if (actualDamage > 0 && Kind == DamageKind.Combat && DamageSource != null)
            {
                var sourceKeywords = context.BuffService.GetEffectiveKeywords(DamageSource, context);

                if (sourceKeywords.Contains(CardKeyword.Drain))
                {
                    var owner = context.CurrentGameState.GetPlayerState(DamageSource.OwnerPlayerId);
                    if (owner?.Leader != null)
                    {
                        result.GeneratedEvents.Add(new HealEvent(owner.Leader, actualDamage, DamageSource));
                    }
                }

                if (sourceKeywords.Contains(CardKeyword.Bane) && card.IsFollower && newHealth > 0)
                {
                    result.GeneratedEvents.Add(new DestroyEvent(card, DamageSource, "DestroyedByBane"));
                }

                if (DamageSource.IsSuperEvolved && card.IsFollower && newHealth <= 0)
                {
                    var defenderOwner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (defenderOwner?.Leader != null)
                    {
                        result.GeneratedEvents.Add(new DamageEvent(defenderOwner.Leader, 1, DamageSource, DamageKind.Ability));
                    }
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
            var targetId = DamageTarget?.InstanceId;
            var sourceId = DamageSource?.InstanceId;
            if (!targetId.HasValue || Damage <= 0)
            {
                return "damage";
            }

            var targetToken = gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value);
            return sourceId.HasValue
                ? $"damage target={targetToken} amount={Damage} source={gameState.InGameIds.Format(InGameIdKind.Card, sourceId.Value)}"
                : $"damage target={targetToken} amount={Damage}";
        }
    }
}
