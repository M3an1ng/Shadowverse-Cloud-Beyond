// File: Server/Domain/Events/GameContextExtensions.cs
using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Provides extension methods for the immutable GameContext record.
    /// This keeps the GameContext as a pure data carrier, while providing convenient accessors for derived data.
    /// </summary>
    public static class GameContextExtensions
    {
        /// <summary>
        /// Gets the PlayerState for the player who initiated the current action chain.
        /// </summary>
        /// <param name="context">The game context.</param>
        /// <returns>The requesting player's state.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the player is not found in the game state.</exception>
        public static PlayerState GetRequestingPlayer(this GameContext context)
        {
            return context.CurrentGameState.GetPlayerState(context.RequestingPlayerId)
                ?? throw new InvalidOperationException($"Requesting player with ID {context.RequestingPlayerId} not found in game state.");
        }

        /// <summary>
        /// Gets the PlayerState for the opponent of the player who initiated the current action chain.
        /// </summary>
        /// <param name="context">The game context.</param>
        /// <returns>The opponent player's state.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the opponent is not found in the game state.</exception>
        public static PlayerState GetOpponentPlayer(this GameContext context)
        {
            return context.CurrentGameState.GetOpponentPlayerState(context.RequestingPlayerId)
                ?? throw new InvalidOperationException($"Opponent for player with ID {context.RequestingPlayerId} not found in game state.");
        }

        /// <summary>
        /// Gets the ability instance that is the source of the current effect being processed.
        /// </summary>
        /// <param name="context">The game context.</param>
        /// <returns>The source IAbility instance, or null if the effect has no source ability or cannot be found.</returns>
        public static IAbility? GetCurrentAbility(this GameContext context)
        {
            if (context.CurrentEffect?.sourceAbilityId == null)
            {
                return null;
            }

            var abilityManager = context.ServiceProvider.GetRequiredService<IAbilityManager>();
            return abilityManager.GetAbilityByRuntimeId(context.CurrentEffect.sourceAbilityId.Value);
        }

        public static PlayerState GetSourceCardOwner(this GameContext context)
        {
            return context.CurrentGameState.GetPlayerState(context.SourceCard!.OwnerPlayerId)
                ?? throw new InvalidOperationException($"Source card owner with ID {context.SourceCard.OwnerPlayerId} not found in game state.");
        }
    }
    public static class DomainOperationResultExtensions
    {
        /// <summary>
        /// Merges the contents of another DomainOperationResult into this one.
        /// </summary>
        /// <param name="source">The source result to merge into.</param>
        /// <param name="other">The result to merge from.</param>
        public static void Merge(this DomainOperationResult source, DomainOperationResult other)
        {
            if (!other.Success)
            {
                source.Success = false;
                if (!string.IsNullOrEmpty(other.ErrorMessage))
                {
                    source.ErrorMessage = string.IsNullOrEmpty(source.ErrorMessage)
                        ? other.ErrorMessage
                        : $"{source.ErrorMessage}; {other.ErrorMessage}";
                }
            }
            source.GeneratedEvents.AddRange(other.GeneratedEvents);
        }
    }
}