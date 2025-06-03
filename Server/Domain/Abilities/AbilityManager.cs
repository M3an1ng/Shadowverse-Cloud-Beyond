using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Manages all card abilities in the game, processing events and triggering appropriate abilities.
    /// This is the central coordinator for the Event-driven ability system.
    /// </summary>
    public class AbilityManager
    {
        private readonly Dictionary<string, List<ICardAbility>> _abilitiesByEvent;
        private readonly Dictionary<string, List<ICardAbility>> _abilitiesByCard;
        private readonly Queue<IGameEvent> _eventQueue;
        private bool _isProcessing;

        public AbilityManager()
        {
            _abilitiesByEvent = new Dictionary<string, List<ICardAbility>>();
            _abilitiesByCard = new Dictionary<string, List<ICardAbility>>();
            _eventQueue = new Queue<IGameEvent>();
            _isProcessing = false;
        }

        /// <summary>
        /// Registers a card ability with the manager.
        /// </summary>
        /// <param name="ability">The ability to register.</param>
        public void RegisterAbility(ICardAbility ability)
        {
            if (ability?.SourceCard == null)
                throw new ArgumentNullException(nameof(ability));

            // Index by event types
            foreach (var eventType in ability.TriggerEvents)
            {
                if (!_abilitiesByEvent.ContainsKey(eventType))
                    _abilitiesByEvent[eventType] = new List<ICardAbility>();
                
                _abilitiesByEvent[eventType].Add(ability);
            }

            // Index by card instance
            var cardId = ability.SourceCard.InstanceId;
            if (!_abilitiesByCard.ContainsKey(cardId))
                _abilitiesByCard[cardId] = new List<ICardAbility>();
            
            _abilitiesByCard[cardId].Add(ability);
        }

        /// <summary>
        /// Unregisters all abilities for a specific card (e.g., when card is destroyed).
        /// </summary>
        /// <param name="cardInstanceId">The instance ID of the card whose abilities should be removed.</param>
        public void UnregisterCardAbilities(string cardInstanceId)
        {
            if (!_abilitiesByCard.ContainsKey(cardInstanceId))
                return;

            var abilities = _abilitiesByCard[cardInstanceId];
            
            // Remove from event indexes
            foreach (var ability in abilities)
            {
                foreach (var eventType in ability.TriggerEvents)
                {
                    if (_abilitiesByEvent.ContainsKey(eventType))
                    {
                        _abilitiesByEvent[eventType].Remove(ability);
                        if (_abilitiesByEvent[eventType].Count == 0)
                            _abilitiesByEvent.Remove(eventType);
                    }
                }
            }

            // Remove from card index
            _abilitiesByCard.Remove(cardInstanceId);
        }

        /// <summary>
        /// Processes a game event, triggering any relevant abilities and handling the resulting event chain.
        /// </summary>
        /// <param name="initialEvent">The initial event to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A list of all events that were generated during processing.</returns>
        public async Task<List<IGameEvent>> ProcessEventAsync(IGameEvent initialEvent, GameContext context)
        {
            var allGeneratedEvents = new List<IGameEvent>();
            
            // Add the initial event to the queue
            _eventQueue.Enqueue(initialEvent);

            // Prevent recursive processing
            if (_isProcessing)
                return allGeneratedEvents;

            _isProcessing = true;

            try
            {
                // Process events until the queue is empty
                while (_eventQueue.Count > 0)
                {
                    var currentEvent = _eventQueue.Dequeue();
                    
                    // Execute the event itself first
                    var eventResults = await currentEvent.ExecuteAsync(context);
                    allGeneratedEvents.AddRange(eventResults);

                    // Find abilities that should trigger for this event
                    var triggeredAbilities = await FindTriggeredAbilitiesAsync(currentEvent, context);

                    // Execute triggered abilities
                    foreach (var ability in triggeredAbilities)
                    {
                        try
                        {
                            var abilityResults = await ability.OnTriggeredAsync(currentEvent, context);
                            allGeneratedEvents.AddRange(abilityResults);

                            // Add new events to the queue for further processing
                            foreach (var newEvent in abilityResults)
                            {
                                _eventQueue.Enqueue(newEvent);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log ability execution error but continue processing
                            Console.WriteLine($"Error executing ability {ability.AbilityTag} for card {ability.SourceCard.InstanceId}: {ex.Message}");
                        }
                    }

                    // Add event results to queue for further processing
                    foreach (var newEvent in eventResults)
                    {
                        _eventQueue.Enqueue(newEvent);
                    }
                }
            }
            finally
            {
                _isProcessing = false;
            }

            return allGeneratedEvents;
        }

        /// <summary>
        /// Finds all abilities that should trigger in response to the given event.
        /// </summary>
        /// <param name="gameEvent">The event to check for triggers.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A list of abilities that should trigger.</returns>
        private async Task<List<ICardAbility>> FindTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context)
        {
            var triggeredAbilities = new List<ICardAbility>();

            // Get abilities that listen to this event type
            if (_abilitiesByEvent.TryGetValue(gameEvent.EventType, out var potentialAbilities))
            {
                foreach (var ability in potentialAbilities)
                {
                    try
                    {
                        if (await ability.ShouldTriggerAsync(gameEvent, context))
                        {
                            triggeredAbilities.Add(ability);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log trigger check error but continue
                        Console.WriteLine($"Error checking trigger for ability {ability.AbilityTag}: {ex.Message}");
                    }
                }
            }

            return triggeredAbilities;
        }

        /// <summary>
        /// Creates abilities for a card based on its definition.
        /// This method parses the card's ability data and creates appropriate ability instances.
        /// </summary>
        /// <param name="card">The card instance to create abilities for.</param>
        /// <param name="abilityData">The ability data from the card definition.</param>
        /// <returns>A list of created abilities.</returns>
        public List<ICardAbility> CreateAbilitiesForCard(CardInstance card, Dictionary<string, object> abilityData)
        {
            var abilities = new List<ICardAbility>();

            foreach (var kvp in abilityData)
            {
                var abilityType = kvp.Key.ToLowerInvariant();
                var parameters = kvp.Value as Dictionary<string, object> ?? new Dictionary<string, object>();

                ICardAbility? ability = abilityType switch
                {
                    "rush" => new RushAbility(),
                    "storm" => new StormAbility(),
                    "ward" => new WardAbility(),
                    "drain" => new DrainAbility(),
                    "bane" => new BaneAbility(),
                    "fanfare" => CreateFanfareAbility(parameters),
                    "lastwords" => CreateLastWordsAbility(parameters),
                    "evolveeffect" => CreateEvolveEffectAbility(parameters),
                    _ => null
                };

                if (ability != null)
                {
                    ability.SourceCard = card;
                    abilities.Add(ability);
                }
            }

            return abilities;
        }

        private ICardAbility? CreateFanfareAbility(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("effect", out var effectObj) && effectObj is string effectType)
            {
                return new FanfareAbility(effectType, parameters);
            }
            return null;
        }

        private ICardAbility? CreateLastWordsAbility(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("effect", out var effectObj) && effectObj is string effectType)
            {
                return new LastWordsAbility(effectType, parameters);
            }
            return null;
        }

        private ICardAbility? CreateEvolveEffectAbility(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("effect", out var effectObj) && effectObj is string effectType)
            {
                return new EvolveEffectAbility(effectType, parameters);
            }
            return null;
        }

        /// <summary>
        /// Converts internal ability events to DTOs for client broadcasting.
        /// </summary>
        /// <param name="events">The list of events to convert.</param>
        /// <returns>A list of event DTOs for client communication.</returns>
        public List<AbilityTriggeredEventDto> ConvertToEventDtos(List<IGameEvent> events)
        {
            var dtos = new List<AbilityTriggeredEventDto>();

            foreach (var gameEvent in events.OfType<AbilityTriggeredEvent>())
            {
                if (gameEvent.SourceCard != null && 
                    gameEvent.Parameters.TryGetValue("ability_tag", out var abilityTagObj) &&
                    gameEvent.Parameters.TryGetValue("trigger_event", out var triggerEventObj) &&
                    abilityTagObj is string abilityTag &&
                    triggerEventObj is IGameEvent triggerEvent)
                {
                    var abilityParams = gameEvent.Parameters.GetValueOrDefault("ability_parameters") as Dictionary<string, object> ?? new();
                    
                    var dto = new AbilityTriggeredEventDto(
                        gameEvent.SourceCard.OwnerPlayerId,
                        gameEvent.SourceCard.InstanceId,
                        abilityTag,
                        triggerEvent.EventType,
                        abilityParams
                    );
                    
                    dtos.Add(dto);
                }
            }

            return dtos;
        }
    }
}
