// File: Server/Services/Abstractions/IBuffService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Service responsible for calculating card stats and keywords considering all active buffs.
    /// This centralizes the logic that was previously in CardInstance.
    /// </summary>
    public interface IBuffService
    {
        /// <summary>
        /// Calculates the effective attack of a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective attack value.</returns>
        Task<int> GetEffectiveAttackAsync(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Calculates the effective maximum health of a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective maximum health value.</returns>
        Task<int> GetEffectiveMaxHealthAsync(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Calculates the effective cost of a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective cost value.</returns>
        Task<int> GetEffectiveCostAsync(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Gets the list of effective keywords for a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>A list of all effective keywords.</returns>
        Task<List<CardKeyword>> GetEffectiveKeywordsAsync(CardInstance card, GameContext gameContext);
    }
}