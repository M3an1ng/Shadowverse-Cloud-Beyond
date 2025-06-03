using ShadowverseBeyond.Server.Domain.Events;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Rush keyword ability - allows a follower to attack other followers immediately when summoned.
    /// </summary>
    public class RushAbility : CardAbilityBase
    {
        public RushAbility() : base("Rush", new List<string> { "summon" })
        {
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Rush triggers when this card is summoned
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // Add ability triggered event for broadcasting
            events.Add(CreateAbilityTriggeredEvent(triggerEvent));

            // Rush effect: Allow the card to attack other followers immediately
            // The actual game logic (setting CanAttack = true) would be handled by GameService
            // This is just the ability trigger notification

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Storm keyword ability - allows a follower to attack any target immediately when summoned.
    /// </summary>
    public class StormAbility : CardAbilityBase
    {
        public StormAbility() : base("Storm", new List<string> { "summon" })
        {
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Storm triggers when this card is summoned
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // Add ability triggered event for broadcasting
            events.Add(CreateAbilityTriggeredEvent(triggerEvent));

            // Storm effect: Allow the card to attack any target immediately
            // The actual game logic would be handled by GameService

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Ward keyword ability - forces opponents to attack this follower first.
    /// </summary>
    public class WardAbility : CardAbilityBase
    {
        public WardAbility() : base("Ward", new List<string> { "summon" })
        {
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Ward triggers when this card is summoned
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // Add ability triggered event for broadcasting
            events.Add(CreateAbilityTriggeredEvent(triggerEvent));

            // Ward effect: This follower must be attacked first
            // The actual game logic would be handled by GameService

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Drain keyword ability - heals the controller when this follower deals damage.
    /// </summary>
    public class DrainAbility : CardAbilityBase
    {
        public DrainAbility() : base("Drain", new List<string> { "damage" })
        {
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Drain triggers when this card deals damage
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // Add ability triggered event for broadcasting
            events.Add(CreateAbilityTriggeredEvent(triggerEvent));

            // Drain effect: Heal the controller for the amount of damage dealt
            var damage = triggerEvent.Parameters.GetValueOrDefault("damage", 0);
            if (damage is int damageAmount && damageAmount > 0)
            {
                var controller = context.GetPlayer(SourceCard.OwnerPlayerId);
                if (controller != null)
                {
                    // Create a heal event for the controller's leader
                    var leaderCard = context.FindCardInstance(controller.PlayerId + "_leader");
                    if (leaderCard != null)
                    {
                        events.Add(new HealEvent(leaderCard, damageAmount, SourceCard));
                    }
                }
            }

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Bane keyword ability - destroys any follower this card damages.
    /// </summary>
    public class BaneAbility : CardAbilityBase
    {
        public BaneAbility() : base("Bane", new List<string> { "damage" })
        {
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Bane triggers when this card deals damage to a follower
            if (triggerEvent.SourceCard?.InstanceId != SourceCard.InstanceId)
                return false;

            var target = triggerEvent.Parameters.GetValueOrDefault("target");
            return target is CardInstance targetCard && targetCard.CardType.ToLowerInvariant() == "follower";
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // Add ability triggered event for broadcasting
            events.Add(CreateAbilityTriggeredEvent(triggerEvent));

            // Bane effect: Destroy the target follower
            var target = triggerEvent.Parameters.GetValueOrDefault("target");
            if (target is CardInstance targetCard)
            {
                var targetPlayer = context.GetPlayer(targetCard.OwnerPlayerId);
                if (targetPlayer != null)
                {
                    events.Add(new CardDestroyedEvent(targetPlayer, targetCard));
                }
            }

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Heal event for restoring health to a target.
    /// </summary>
    public class HealEvent : GameEventBase
    {
        public HealEvent(CardInstance target, int healAmount, CardInstance? source = null)
            : base("heal")
        {
            SourceCard = source;
            Parameters["target"] = target;
            Parameters["heal_amount"] = healAmount;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            // The actual healing logic would be handled by GameService
            return await Task.FromResult(events);
        }
    }
}
