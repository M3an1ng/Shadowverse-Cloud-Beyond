using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public static class GameContextExtensions
    {

        public static object? ResolveIdentifier(this GameContext context, List<string> path)
        {
            if (path == null || !path.Any()) return null;

            if (context.TemporaryVariables.TryGetValue(path.First(), out var tempVariableValue) && path.Count == 1)
            {
                return tempVariableValue;
            }

            if (path.Count > 2 &&
                (path[0].ToLowerInvariant() is "self" or "this" or "current") &&
                path[1].ToLowerInvariant() == "state" &&
                context.CurrentItem is CardInstance sourceCard)
            {
                string variableName = path[2];
                if (sourceCard.AbilityState.TryGetValue(variableName, out var stateValue))
                {
                    if (path.Count == 3)
                    {
                        return stateValue;
                    }
                    else
                    {
                        return ResolvePathSegments(stateValue, path.Skip(3).ToList());
                    }
                }
            }

            object? rootObject = path.First().ToLowerInvariant() switch
            {
                "player" => context.RequestingPlayer,
                "opponent" => context.OpponentPlayer,
                "gamestate" => context.CurrentGameState,
                "self" or "this" or "current" => context.CurrentItem,
                _ => null
            };

            List<string> remainingPath = path.Skip(1).ToList();

            if (rootObject != null)
            {
                return ResolvePathSegments(rootObject, remainingPath);
            }
            else if (path.First().ToLowerInvariant() is "self" or "this" or "current" && context.CurrentItem != null)
            {
                if (!remainingPath.Any()) return context.CurrentItem;

                if (remainingPath.Count > 0 && remainingPath[0].ToLowerInvariant() == "state")
                {
                }
                else
                {
                    return ResolvePathSegments(context.CurrentItem, remainingPath);
                }
            }

            // Console.WriteLine($"Warning: Identifier not found or path invalid: {string.Join(".", path)}");
            return null;
        }

        private static object? ResolvePathSegments(object? target, List<string> segments)
        {
            object? currentTarget = target;
            foreach (var segment in segments)
            {
                if (currentTarget == null) return null;

                if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase) && currentTarget is System.Collections.ICollection collection)
                {
                    currentTarget = collection.Count;
                    continue;
                }
                if (segment.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase) && currentTarget is System.Collections.ICollection collectionIsEmpty)
                {
                    currentTarget = collectionIsEmpty.Count == 0;
                    continue;
                }

                var propertyInfo = currentTarget.GetType().GetProperty(segment, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                {
                    // Console.WriteLine($"Warning: Property '{segment}' not found on type '{currentTarget.GetType().Name}'");
                    return null;
                }
                currentTarget = propertyInfo.GetValue(currentTarget);
            }
            return currentTarget;
        }
    }
}