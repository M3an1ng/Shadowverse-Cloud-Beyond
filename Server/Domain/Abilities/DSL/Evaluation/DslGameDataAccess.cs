using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public static class DslGameDataAccess
    {
        public static IReadOnlyCollection<CardDefinition> ResolveEnvironmentPool(
            IGameDataProvider? provider,
            GameContext? gameContext,
            string? memberName)
        {
            if (provider == null || string.IsNullOrWhiteSpace(memberName))
            {
                return Array.Empty<CardDefinition>();
            }

            if (!TryResolveEnvironment(memberName, gameContext, out var environment))
            {
                return Array.Empty<CardDefinition>();
            }

            if (provider is IEnvironmentScopedGameDataProvider scopedProvider)
            {
                return scopedProvider.GetCardDefinitionsForEnvironment(environment);
            }

            return FilterCards(provider.Cards, environment);
        }

        private static IReadOnlyCollection<CardDefinition> FilterCards(
            IReadOnlyCollection<CardDefinition>? cards,
            DeckEnvironment environment)
        {
            if (cards == null || cards.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            return cards
                .Where(card => (card.CardSetDefinition?.Environments ?? new List<DeckEnvironment>()).Contains(environment))
                .ToList();
        }

        private static bool TryResolveEnvironment(
            string memberName,
            GameContext? gameContext,
            out DeckEnvironment environment)
        {
            if (DslEnvironmentCardPoolNames.TryGetEnvironment(memberName, out environment))
            {
                return true;
            }

            if (!DslEnvironmentCardPoolNames.IsCurrentEnvironmentProperty(memberName))
            {
                environment = default;
                return false;
            }

            if (gameContext?.CurrentGameState == null)
            {
                throw new InvalidOperationException(
                    $"DSL pool '{memberName}' requires a game context with a unique environment.");
            }

            environment = gameContext.CurrentGameState.GetRequiredUniqueEnvironment();
            return true;
        }
    }
}
