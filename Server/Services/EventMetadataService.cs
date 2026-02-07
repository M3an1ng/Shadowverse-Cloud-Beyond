using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public class EventMetadataService : IEventMetadataService
    {
        private readonly ILogger<EventMetadataService> _logger;
        private readonly List<EventMetadataDto> _cachedMetadata;
        private static readonly IReadOnlyDictionary<EventType, string> EventCategoryMap = new Dictionary<EventType, string>
        {
            { EventType.Attack, AbilityEditorCategories.BaseActionId },
            { EventType.Clash, AbilityEditorCategories.BaseActionId },
            { EventType.Damage, AbilityEditorCategories.BaseActionId },
            { EventType.Heal, AbilityEditorCategories.BaseActionId },
            { EventType.Evolve, AbilityEditorCategories.BaseActionId },
            { EventType.SuperEvolve, AbilityEditorCategories.BaseActionId },
            { EventType.PlayCard, AbilityEditorCategories.BaseActionId },
            { EventType.Act, AbilityEditorCategories.BaseActionId },
            { EventType.Fusion, AbilityEditorCategories.BaseActionId },
            { EventType.Enhance, AbilityEditorCategories.BaseActionId },

            { EventType.Summon, AbilityEditorCategories.PositionChangeId },
            { EventType.CreateCard, AbilityEditorCategories.PositionChangeId },
            { EventType.CopyCard, AbilityEditorCategories.PositionChangeId },
            { EventType.CardMoved, AbilityEditorCategories.PositionChangeId },
            { EventType.CardRevealed, AbilityEditorCategories.PositionChangeId },
            { EventType.AddCardToHand, AbilityEditorCategories.PositionChangeId },
            { EventType.Draw, AbilityEditorCategories.PositionChangeId },
            { EventType.DeckEmptyOnDraw, AbilityEditorCategories.PositionChangeId },
            { EventType.Discard, AbilityEditorCategories.PositionChangeId },
            { EventType.Return, AbilityEditorCategories.PositionChangeId },
            { EventType.Vanish, AbilityEditorCategories.PositionChangeId },
            { EventType.Destroy, AbilityEditorCategories.PositionChangeId },
            { EventType.Transform, AbilityEditorCategories.PositionChangeId },
            { EventType.AddDeck, AbilityEditorCategories.PositionChangeId },
            { EventType.Tuck, AbilityEditorCategories.PositionChangeId },
            { EventType.Shuffle, AbilityEditorCategories.PositionChangeId },
            { EventType.CardBurned, AbilityEditorCategories.PositionChangeId },
            { EventType.CardPacked, AbilityEditorCategories.PositionChangeId },

            { EventType.BuffApplied, AbilityEditorCategories.TempStateId },
            { EventType.BuffRemoved, AbilityEditorCategories.TempStateId },
            { EventType.KeywordApplied, AbilityEditorCategories.TempStateId },
            { EventType.KeywordRemoved, AbilityEditorCategories.TempStateId },
            { EventType.AbilityGranted, AbilityEditorCategories.TempStateId },
            { EventType.SilenceApplied, AbilityEditorCategories.TempStateId },
            { EventType.SilenceRemoved, AbilityEditorCategories.TempStateId },
            { EventType.SpellBoost, AbilityEditorCategories.TempStateId },

            { EventType.PpUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.PpMaxUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.PpBoostStateUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.EpUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.EpMaxUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.SepUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.SepMaxUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.StatChanged, AbilityEditorCategories.ResourceValueId },
            { EventType.HealthChanged, AbilityEditorCategories.ResourceValueId },
            { EventType.HandUpdated, AbilityEditorCategories.ResourceValueId },
            { EventType.CounterChanged, AbilityEditorCategories.ResourceValueId },

            { EventType.TurnStart, AbilityEditorCategories.TurnFlowId },
            { EventType.TurnEnd, AbilityEditorCategories.TurnFlowId },
            { EventType.CountdownTick, AbilityEditorCategories.TurnFlowId },
            { EventType.CountdownFinished, AbilityEditorCategories.TurnFlowId },
            { EventType.MulliganCompleted, AbilityEditorCategories.TurnFlowId },
            { EventType.GameEnd, AbilityEditorCategories.TurnFlowId },

            { EventType.AbilityTriggered, AbilityEditorCategories.SpecialId },

            { EventType.AttackRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.PlayCardRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.EvolveRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.SuperEvolveRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.ActRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.FusionRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.MulliganSubmitted, AbilityEditorCategories.SystemRequestId },
            { EventType.EndTurnRequested, AbilityEditorCategories.SystemRequestId },
            { EventType.PlayerChoiceSubmitted, AbilityEditorCategories.SystemRequestId },
            { EventType.PpBoostRequested, AbilityEditorCategories.SystemRequestId }
        };

        public EventMetadataService(ILogger<EventMetadataService> logger)
        {
            _logger = logger;
            _cachedMetadata = new List<EventMetadataDto>();
            Initialize();
        }

        private void Initialize()
        {
            _logger.LogInformation("Initializing EventMetadataService by scanning assemblies...");

            var eventInterfaceType = typeof(IGameEvent);

            static IEnumerable<Type> SafeGetTypes(Assembly assembly)
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null)!;
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            }

            var eventTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(SafeGetTypes)
                .Where(t => t.IsClass && !t.IsAbstract && eventInterfaceType.IsAssignableFrom(t));

            foreach (var eventType in eventTypes)
            {
                var attribute = eventType.GetCustomAttribute<GameEventMetadataAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                var normalizedCategoryId = AbilityEditorCategories.NormalizeId(attribute.CategoryId);
                if (normalizedCategoryId == AbilityEditorCategories.UncategorizedId &&
                    EventCategoryMap.TryGetValue(attribute.EventType, out var mappedCategoryId))
                {
                    normalizedCategoryId = mappedCategoryId;
                }

                var eventDto = new EventMetadataDto
                {
                    DisplayName = attribute.DisplayName,
                    Description = attribute.Description,
                    CategoryId = normalizedCategoryId,
                    CategoryLabel = AbilityEditorCategories.GetLabelOrDefault(normalizedCategoryId, attribute.CategoryLabel),
                    Visibility = attribute.Visibility,
                    EventType = attribute.EventType,
                    Parameters = new List<EventParameterMetadataDto>()
                };

                var descriptor = EventParameterCache.GetDescriptor(eventType);
                foreach (var parameter in descriptor.Parameters)
                {
                    eventDto.Parameters.Add(new EventParameterMetadataDto
                    {
                        PropertyName = parameter.PropertyName,
                        DisplayName = parameter.DisplayName,
                        Description = parameter.Description,
                        TypeName = TypeNameHelper.GetCleanTypeName(parameter.PropertyType),
                        Scope = parameter.Scope,
                        DslDisplayTypeName = parameter.DslDisplayTypeName
                    });
                }

                _cachedMetadata.Add(eventDto);
                _logger.LogDebug("Registered event metadata for: {EventName}", eventType.Name);
            }

            _logger.LogInformation("EventMetadataService initialization complete. Found {Count} event metadata entries.", _cachedMetadata.Count);
        }



        public IEnumerable<EventMetadataDto> GetAllEventMetadata()
        {
            return _cachedMetadata;
        }
    }
}
