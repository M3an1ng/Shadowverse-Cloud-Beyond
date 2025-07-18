using ShadowverseBeyond.Server.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Defines the contract for managing Buff instances within a game session.
    /// Responsible for registering, unregistering, and querying active buffs.
    /// </summary>
    public interface IBuffManager
    {
        /// <summary>
        /// Registers a new Buff instance, applying stacking rules and storing it.
        /// This is the primary method for adding a buff to a target.
        /// </summary>
        /// <param name="buff">The Buff instance to register.</param>
        /// <returns>A DomainOperationResult containing generated events (e.g., BuffAppliedEvent, BuffRemovedEvent for replacements).</returns>
        DomainOperationResult RegisterBuff(BuffConfig buff);

        /// <summary>
        /// Unregisters a specific Buff instance, typically when it expires or is explicitly removed.
        /// </summary>
        /// <param name="buffId">The unique ID of the Buff to unregister.</param>
        /// <returns>A DomainOperationResult containing a BuffRemovedEvent if successful.</returns>
        DomainOperationResult UnregisterBuff(Guid buffId);

        /// <summary>
        /// Retrieves all active Buff instances currently applied to a specific card instance.
        /// The returned list should ideally be ordered by application time, as calculation order matters.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the target card instance.</param>
        /// <returns>An ordered list of active Buffs applied to the card.</returns>
        List<BuffConfig> GetBuffsForCard(Guid cardInstanceId);

        /// <summary>
        /// Removes all Buffs that originated from a specific source ability instance.
        /// Useful when the source card leaves play (e.g., for auras).
        /// </summary>
        /// <param name="sourceAbilityId">The unique ID of the source ability.</param>
        /// <returns>A DomainOperationResult containing all BuffRemovedEvents.</returns>
        DomainOperationResult RemoveBuffsFromSource(string sourceAbilityId);

        /// <summary>
        /// Removes all Buffs currently applied to a specific card instance based on a predicate.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the target card instance.</param>
        /// <param name="predicate">A function to determine which buffs to remove. If null, removes all.</param>
        /// <returns>A DomainOperationResult containing all BuffRemovedEvents.</returns>
        DomainOperationResult RemoveAllBuffs(Guid cardInstanceId, Func<BuffConfig, bool>? predicate = null);
    }
}