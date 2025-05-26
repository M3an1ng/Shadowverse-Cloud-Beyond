using ShadowverseBeyond.Server.Domain.Entities; // Added ShadowverseBeyond prefix
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Interfaces // Added ShadowverseBeyond prefix
{
    /// <summary>
    /// Interface for accessing card data.
    /// </summary>
    public interface ICardRepository
    {
        /// <summary>
        /// Retrieves all card definitions asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of all card definitions.</returns>
        Task<IEnumerable<CardDefinition>> GetAllAsync();

        /// <summary>
        /// Retrieves a specific card definition by its ID asynchronously.
        /// </summary>
        /// <param name="id">The ID of the card definition to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the card definition, or null if not found.</returns>
        Task<CardDefinition?> GetCardByIdAsync(string id);
        // Future methods like GetByIdAsync, AddAsync, etc., can be added here.
    }
}