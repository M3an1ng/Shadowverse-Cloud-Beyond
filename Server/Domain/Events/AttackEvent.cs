using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Represents the player's intent to attack with a follower.
    /// This event is the entry point for an attack action. Its Commit method
    /// orchestrates the generation of subsequent events like Clash and Damage.
    /// </summary>
    [GameEventMetadata("攻击时", "当一个单位攻击另一个单位时触发（在伤害计算前）。", EventType.Attack)]
    public class AttackEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("攻击者", "发起攻击的单位。")]
        public CardInstance? Attacker => Source?.Card;

        [GameEventParameter("目标", "被攻击的目标单位。")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        public AttackEvent(CardInstance attacker, CardInstance target)
            : base(EventType.Attack)
        {
            Source = TargetInfo.ForCard(attacker);
            Target.Add(TargetInfo.ForCard(target));
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (Attacker == null || TargetCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Attacker or target is null in AttackEvent." };
            }

            var attackerPlayer = context.CurrentGameState.GetPlayerState(Attacker.OwnerPlayerId);
            if (attackerPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Attacker's owner (ID: {Attacker.OwnerPlayerId}) not found." };
            }

            var attackConsequences = Attacker.PerformAttack(TargetCard);

            if (Attacker.HasKeyword(CardKeyword.Ambush))
            {
                attackConsequences.GeneratedEvents.Add(new KeywordRemovedEvent(CardKeyword.Ambush, Attacker));
            }

            return attackConsequences;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var attackerId = Attacker?.InstanceId;
            var targetId = TargetCard?.InstanceId;
            if (!attackerId.HasValue || !targetId.HasValue)
            {
                return "attack";
            }

            var attackerToken = gameState.InGameIds.Format(InGameIdKind.Card, attackerId.Value);
            var targetToken = gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value);
            return $"attack attacker={attackerToken} target={targetToken}";
        }
    }
}
