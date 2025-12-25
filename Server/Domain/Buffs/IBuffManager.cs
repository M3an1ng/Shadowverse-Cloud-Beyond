using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Defines the low-level contract for managing Buff instances' state.
    /// This interface is responsible for direct, non-business-logic data operations on buffs.
    /// </summary>
    public interface IBuffManager
    {
        /// <summary>
        /// Retrieves all active Buff instances currently applied to a specific card instance.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the target card instance.</param>
        /// <returns>An ordered list of active Buffs applied to the card.</returns>
        List<Buff> GetBuffsForCard(Guid cardInstanceId);

        /// <summary>
        /// Finds a buff instance by its unique ID without removing it.
        /// </summary>
        /// <param name="buffId">The ID of the buff to find.</param>
        /// <returns>The found buff instance, or null if not found.</returns>
        Buff? FindBuffInstance(Guid buffId);

        /// <summary>
        /// Core method: Registers a buff instance to the internal dictionary.
        /// </summary>
        /// <param name="buff">The buff instance to add.</param>
        void RegisterBuffInstance(Buff buff, GameState gameState);

        /// <summary>
        /// Core method: Removes a buff instance from the internal dictionary.
        /// </summary>
        /// <param name="buffId">The ID of the buff to remove.</param>
        /// <param name="context">The current game context, required for handling ability removal.</param>
        /// <returns>The removed buff instance, or null if not found.</returns>
        Buff? UnregisterBuffInstance(Guid buffId);
    }
}