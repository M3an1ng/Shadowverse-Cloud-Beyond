using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Interfaces; // Added for ICardRepository
using ShadowverseBeyond.Server.Exceptions; // Added for GameLogicException
using ShadowverseBeyond.Server.Domain.Abilities; // For AbilityManager and AbilityConfig
using ShadowverseBeyond.Server.Services; // For GameDataProvider
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of transforming a card instance into another card definition.
    /// </summary>
    [EffectHandler(EffectType.Transform)]
    public class TransformEffectHandler : EffectHandlerBase<TransformEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        public TransformEffectHandler(
            ILogger<TransformEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, TransformEffectParams parameters)
        {
            const string effectName = "TransformEffect";
            Logger.LogDebug("{EffectName}: Starting with CardSourceMode={CardSourceMode}, TargetCardDefinitionId={TargetDefId}.",
                           effectName, parameters.CardSourceMode, parameters.TargetCardDefinitionId);

            if (parameters.CardSourceMode == Common.CardSourceMode.Instance)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "Transform does not support instance mode."
                };
            }

            if (parameters.CardSourceMode == Common.CardSourceMode.Definition)
            {
                var targetDefinition = GameDataProvider.Instance.GetCardDefinition(parameters.TargetCardDefinitionId);
                if (targetDefinition == null)
                {
                    Logger.LogError("{EffectName}: Target CardDefinition with ID {TargetDefId} not found.",
                                   effectName, parameters.TargetCardDefinitionId);
                    return new GameStateActionResult { Success = false, ErrorMessage = $"Target CardDefinition ID {parameters.TargetCardDefinitionId} not found." };
                }
            }

            CardInstance? copySourceCard = null;
            IReadOnlyList<Buff> copySourceBuffs = Array.Empty<Buff>();
            if (parameters.CardSourceMode == Common.CardSourceMode.Copy)
            {
                if (!TryResolveCopySourceCards(parameters.CopySourceTargets, context, out var resolvedCopySources, out var errorMessage))
                {
                    Logger.LogError("{EffectName}: Failed to resolve copy source targets. {ErrorMessage}", effectName, errorMessage);
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage ?? "Transform copy mode failed to resolve copy source."
                    };
                }

                if (resolvedCopySources.Count != 1)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = $"Transform copy mode requires exactly one copy source card, but resolved {resolvedCopySources.Count}."
                    };
                }

                copySourceCard = resolvedCopySources[0];
                copySourceBuffs = (context.BuffManager.GetBuffsForCard(copySourceCard.InstanceId) ?? new List<Buff>()).ToList();
            }

            Func<TargetInfo, GameContext, DomainOperationResult> transformAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Transform effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;

                    // 通过唯一的 TransformEvent 表达意图，提交阶段执行实体变更
                    var evt = parameters.CardSourceMode == Common.CardSourceMode.Copy
                        ? new TransformEvent(
                            card,
                            copySourceCard!,
                            copySourceBuffs)
                        : new TransformEvent(card, parameters.TargetCardDefinitionId, context.SourceCard);
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(evt);
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                transformAction,
                effectName);
        }

        private static bool TryResolveCopySourceCards(object? value, GameContext context, out List<CardInstance> cards, out string? errorMessage)
        {
            var resolvedCards = new List<CardInstance>();
            errorMessage = null;

            if (value == null)
            {
                errorMessage = "Transform copy mode requires CopySourceTargets.";
                cards = resolvedCards;
                return false;
            }

            if (value is ValueConfig valueConfig)
            {
                var resolver = context.ServiceProvider.GetRequiredService<IValueSourceResolver>();
                value = valueConfig.Resolve(context, resolver);
            }

            var hadUnsupported = false;

            void AddCard(CardInstance? card)
            {
                if (card != null)
                {
                    resolvedCards.Add(card);
                }
            }

            void HandleObject(object? obj)
            {
                switch (obj)
                {
                    case null:
                        return;
                    case CardInstance card:
                        AddCard(card);
                        return;
                    case TargetInfo target when target.Card != null:
                        AddCard(target.Card);
                        return;
                    case Guid guid:
                        AddCard(context.CurrentGameState.FindCardInstance(guid));
                        return;
                    case string str when Guid.TryParse(str, out var parsedGuid):
                        AddCard(context.CurrentGameState.FindCardInstance(parsedGuid));
                        return;
                    case JsonElement jsonElement:
                        if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in jsonElement.EnumerateArray())
                            {
                                HandleObject(item);
                            }
                            return;
                        }
                        if (jsonElement.ValueKind == JsonValueKind.String && Guid.TryParse(jsonElement.GetString(), out var jsonGuid))
                        {
                            AddCard(context.CurrentGameState.FindCardInstance(jsonGuid));
                            return;
                        }
                        hadUnsupported = true;
                        return;
                    case IEnumerable enumerable when obj is not string:
                        foreach (var item in enumerable)
                        {
                            HandleObject(item);
                        }
                        return;
                    default:
                        hadUnsupported = true;
                        return;
                }
            }

            HandleObject(value);

            if (resolvedCards.Count == 0 && hadUnsupported)
            {
                errorMessage = "CopySourceTargets contains unsupported values.";
                cards = resolvedCards;
                return false;
            }

            cards = resolvedCards;
            return true;
        }
    }
}
