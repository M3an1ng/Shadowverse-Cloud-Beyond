using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using System;
using System.ComponentModel;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public enum DamageKind
    {
        [Description("未知")]
        Unknown = 0,
        [Description("战斗")]
        Combat = 1,
        [Description("能力")]
        Ability = 2
    }

    /// <summary>
    /// Event triggered when damage is dealt to a target.
    /// </summary>
    [GameEventMetadata("伤害", "当对目标造成伤害时触发的事件。", EventType.Damage)]
    public class DamageEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("伤害值", "造成的伤害数值。", Scope = GameEventParameterScope.Input)]
        public int Damage { get; }

        [GameEventParameter("实际伤害值", "实际造成的伤害数值（考虑屏障、超进化免伤等）。", Scope = GameEventParameterScope.Output)]
        public int ActualDamage { get; private set; }

        [GameEventParameter("伤害来源", "造成伤害的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? DamageSource => Source?.Card;

        [GameEventParameter("伤害目标", "受到伤害的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? DamageTarget => Target.FirstOrDefault()?.Card;

        [GameEventParameter("伤害类型", "伤害来源类型（战斗/能力等）。")]
        public DamageKind Kind { get; }

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
            if (card.CurrentZone == CardZone.Graveyard || card.CurrentZone == CardZone.Removed)
            {
                ActualDamage = 0;
                result.Success = false;
                result.ErrorMessage = $"DamageEvent target {card.InstanceId} is no longer valid in zone {card.CurrentZone}.";
                return result;
            }

            int oldHealth = card.CurrentHealth;
            int intendedDamage = this.Damage;
            var targetKeywords = context.BuffService.GetEffectiveKeywords(card, context);
            var hasShield = targetKeywords.Contains(CardKeyword.Shield);
            var isSuperEvolveDamageImmune = IsSuperEvolveDamageImmune(card, context);
            var rawDamage = hasShield || isSuperEvolveDamageImmune ? 0 : Math.Max(0, intendedDamage);
            int newHealth = oldHealth - rawDamage;
            ActualDamage = rawDamage;

            if (hasShield)
            {
                // Consume shield on first incoming positive damage.
                result.GeneratedEvents.Add(new KeywordRemovedEvent(CardKeyword.Shield, card, DamageSource));
            }

            // Generate the atomic HealthChangedEvent; it will apply state in its Commit
            result.GeneratedEvents.Add(new HealthChangedEvent(card, oldHealth, newHealth, -rawDamage));

            if (Kind == DamageKind.Combat && DamageSource != null)
            {
                var sourceKeywords = context.BuffService.GetEffectiveKeywords(DamageSource, context);
                var activePlayerId = context.CurrentGameState.CurrentPlayerId;
                if (DamageSource.IsSuperEvolved && activePlayerId.HasValue && DamageSource.OwnerPlayerId == activePlayerId.Value && card.IsFollower && newHealth <= 0)
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
                    result.GeneratedEvents.Add(new GameEndEvent(winnerId.Value, loserId, "主战者生命值归零"));
                }
            }
            // If a non-leader reaches 0 health, schedule destruction after health is applied.
            else if (newHealth <= 0 && Kind != DamageKind.Combat)
            {
                var sourceCard = DamageSource ?? card; // fallback to self if missing
                result.GeneratedEvents.Add(new DestroyEvent(card, sourceCard, EventReasonSource.System));
            }

            return result;
        }

        private static bool IsSuperEvolveDamageImmune(CardInstance target, GameContext context)
        {
            if (!target.IsSuperEvolved || !target.IsFollower)
            {
                return false;
            }

            var activePlayerId = context.CurrentGameState.CurrentPlayerId;
            return activePlayerId.HasValue && target.OwnerPlayerId == activePlayerId.Value;
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
