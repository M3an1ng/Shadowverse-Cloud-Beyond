using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata
{
    public static class DslEnvironmentCardPoolNames
    {
        public const string CurrentEnvironmentCardsPropertyName = "currentEnvironmentCards";

        private static readonly IReadOnlyList<(DeckEnvironment? Environment, string PropertyName)> Entries =
            Enum.GetValues<DeckEnvironment>()
                .Select(environment => ((DeckEnvironment?)environment, GetPropertyName(environment)))
                .Concat(new[]
                {
                    ((DeckEnvironment?)null, CurrentEnvironmentCardsPropertyName)
                })
                .ToList();

        public static IReadOnlyList<(DeckEnvironment? Environment, string PropertyName)> GetAll()
        {
            return Entries;
        }

        public static string GetPropertyName(DeckEnvironment environment)
        {
            var rawName = Enum.GetName(environment) ?? environment.ToString();
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "cards";
            }

            var camel = char.ToLowerInvariant(rawName[0]) + rawName[1..];
            return $"{camel}Cards";
        }

        public static bool TryGetEnvironment(string propertyName, out DeckEnvironment environment)
        {
            foreach (var candidate in Enum.GetValues<DeckEnvironment>())
            {
                if (string.Equals(GetPropertyName(candidate), propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    environment = candidate;
                    return true;
                }
            }

            environment = default;
            return false;
        }

        public static bool IsCurrentEnvironmentProperty(string propertyName)
        {
            return string.Equals(
                propertyName,
                CurrentEnvironmentCardsPropertyName,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
