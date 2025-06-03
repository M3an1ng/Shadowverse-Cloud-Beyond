using ShadowverseBeyond.Server.Domain.Events;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Fanfare ability - triggers when a card is played/summoned.
    /// This is a configurable ability that can have different effects.
    /// </summary>
    public class FanfareAbility : CardAbilityBase
    {
        private readonly string _effectType;
        private readonly Dictionary<string, object> _parameters;

        public FanfareAbility(string effectType, Dictionary<string, object> parameters)
            : base("Fanfare", new List<string> { "play_card", "summon" })
        {
            _effectType = effectType;
            _parameters = parameters;
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Fanfare triggers when this specific card is played or summoned
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // First, broadcast that a Fanfare ability was triggered
            events.Add(CreateAbilityTriggeredEvent(triggerEvent, _parameters));

            // Then execute the specific effect based on the effect type
            switch (_effectType.ToLowerInvariant())
            {
                case "damage":
                    events.AddRange(await ExecuteDamageEffectAsync(context));
                    break;
                case "draw":
                    events.AddRange(await ExecuteDrawEffectAsync(context));
                    break;
                case "summon":
                    events.AddRange(await ExecuteSummonEffectAsync(context));
                    break;
                case "heal":
                    events.AddRange(await ExecuteHealEffectAsync(context));
                    break;
                // Add more effect types as needed
            }

            return events;
        }

        private async Task<List<IGameEvent>> ExecuteDamageEffectAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            if (_parameters.TryGetValue("damage", out var damageObj) && damageObj is int damage &&
                _parameters.TryGetValue("target", out var targetObj) && targetObj is string targetType)
            {
                var targets = await SelectTargetsAsync(targetType, context);
                foreach (var target in targets)
                {
                    events.Add(new DamageEvent(target, damage, SourceCard));
                }
            }

            return events;
        }

        private async Task<List<IGameEvent>> ExecuteDrawEffectAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            if (_parameters.TryGetValue("count", out var countObj) && countObj is int count)
            {
                var controller = context.GetPlayer(SourceCard.OwnerPlayerId);
                if (controller != null)
                {
                    events.Add(new DrawEvent(controller, count));
                }
            }

            return await Task.FromResult(events);
        }

        private async Task<List<IGameEvent>> ExecuteSummonEffectAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            if (_parameters.TryGetValue("cardId", out var cardIdObj) && cardIdObj is int cardId)
            {
                var controller = context.GetPlayer(SourceCard.OwnerPlayerId);
                if (controller != null)
                {
                    // Create a new card instance for summoning
                    var summonCard = new CardInstance($"summon_{cardId}_{System.Guid.NewGuid()}", cardId, controller.PlayerId);
                    events.Add(new SummonEvent(controller, summonCard));
                }
            }

            return await Task.FromResult(events);
        }

        private async Task<List<IGameEvent>> ExecuteHealEffectAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            if (_parameters.TryGetValue("heal", out var healObj) && healObj is int healAmount &&
                _parameters.TryGetValue("target", out var targetObj) && targetObj is string targetType)
            {
                var targets = await SelectTargetsAsync(targetType, context);
                foreach (var target in targets)
                {
                    events.Add(new HealEvent(target, healAmount, SourceCard));
                }
            }

            return events;
        }

        private async Task<List<CardInstance>> SelectTargetsAsync(string targetType, GameContext context)
        {
            var targets = new List<CardInstance>();

            switch (targetType.ToLowerInvariant())
            {
                case "enemy_leader":
                    var enemyPlayer = context.GetOpponent(SourceCard.OwnerPlayerId);
                    if (enemyPlayer != null)
                    {
                        var enemyLeader = context.FindCardInstance(enemyPlayer.PlayerId + "_leader");
                        if (enemyLeader != null) targets.Add(enemyLeader);
                    }
                    break;

                case "all_enemies":
                    var opponent = context.GetOpponent(SourceCard.OwnerPlayerId);
                    if (opponent != null)
                    {
                        foreach (var unit in opponent.Field)
                        {
                            var cardInstance = context.FindCardInstance(unit.InstanceId);
                            if (cardInstance != null) targets.Add(cardInstance);
                        }
                    }
                    break;

                case "random_enemy":
                    var opponentForRandom = context.GetOpponent(SourceCard.OwnerPlayerId);
                    if (opponentForRandom != null && opponentForRandom.Field.Count > 0)
                    {
                        var randomIndex = new System.Random().Next(opponentForRandom.Field.Count);
                        var randomUnit = opponentForRandom.Field[randomIndex];
                        var cardInstance = context.FindCardInstance(randomUnit.InstanceId);
                        if (cardInstance != null) targets.Add(cardInstance);
                    }
                    break;

                // Add more target selection types as needed
            }

            return await Task.FromResult(targets);
        }
    }

    /// <summary>
    /// Last Words ability - triggers when a card is destroyed.
    /// </summary>
    public class LastWordsAbility : CardAbilityBase
    {
        private readonly string _effectType;
        private readonly Dictionary<string, object> _parameters;

        public LastWordsAbility(string effectType, Dictionary<string, object> parameters)
            : base("LastWords", new List<string> { "card_destroyed" })
        {
            _effectType = effectType;
            _parameters = parameters;
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Last Words triggers when this specific card is destroyed
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // First, broadcast that a Last Words ability was triggered
            events.Add(CreateAbilityTriggeredEvent(triggerEvent, _parameters));

            // Then execute the specific effect (similar to Fanfare)
            switch (_effectType.ToLowerInvariant())
            {
                case "damage":
                    events.AddRange(await ExecuteDamageEffectAsync(context));
                    break;
                case "draw":
                    events.AddRange(await ExecuteDrawEffectAsync(context));
                    break;
                case "summon":
                    events.AddRange(await ExecuteSummonEffectAsync(context));
                    break;
                // Add more effect types as needed
            }

            return events;
        }

        // Similar helper methods as FanfareAbility (could be refactored to a base class)
        private async Task<List<IGameEvent>> ExecuteDamageEffectAsync(GameContext context)
        {
            // Implementation similar to FanfareAbility
            return await Task.FromResult(new List<IGameEvent>());
        }

        private async Task<List<IGameEvent>> ExecuteDrawEffectAsync(GameContext context)
        {
            // Implementation similar to FanfareAbility
            return await Task.FromResult(new List<IGameEvent>());
        }

        private async Task<List<IGameEvent>> ExecuteSummonEffectAsync(GameContext context)
        {
            // Implementation similar to FanfareAbility
            return await Task.FromResult(new List<IGameEvent>());
        }
    }

    /// <summary>
    /// Evolve Effect ability - triggers when a card evolves.
    /// </summary>
    public class EvolveEffectAbility : CardAbilityBase
    {
        private readonly string _effectType;
        private readonly Dictionary<string, object> _parameters;

        public EvolveEffectAbility(string effectType, Dictionary<string, object> parameters)
            : base("EvolveEffect", new List<string> { "evolve" })
        {
            _effectType = effectType;
            _parameters = parameters;
        }

        public override async Task<bool> ShouldTriggerAsync(IGameEvent triggerEvent, GameContext context)
        {
            // Evolve Effect triggers when this specific card evolves
            return triggerEvent.SourceCard?.InstanceId == SourceCard.InstanceId;
        }

        public override async Task<List<IGameEvent>> OnTriggeredAsync(IGameEvent triggerEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            // First, broadcast that an Evolve Effect ability was triggered
            events.Add(CreateAbilityTriggeredEvent(triggerEvent, _parameters));

            // Then execute the specific effect (similar to Fanfare)
            switch (_effectType.ToLowerInvariant())
            {
                case "damage":
                    events.AddRange(await ExecuteDamageEffectAsync(context));
                    break;
                case "draw":
                    events.AddRange(await ExecuteDrawEffectAsync(context));
                    break;
                // Add more effect types as needed
            }

            return events;
        }

        // Similar helper methods as FanfareAbility
        private async Task<List<IGameEvent>> ExecuteDamageEffectAsync(GameContext context)
        {
            // Implementation similar to FanfareAbility
            return await Task.FromResult(new List<IGameEvent>());
        }

        private async Task<List<IGameEvent>> ExecuteDrawEffectAsync(GameContext context)
        {
            // Implementation similar to FanfareAbility
            return await Task.FromResult(new List<IGameEvent>());
        }
    }
}
