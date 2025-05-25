using Server.Domain.Entities; // Assuming CardDefinition is here
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Application.Interfaces
{
    /// <summary>
    /// Interface for application logic related to cards.
    /// </summary>
    public interface ICardService
    {
        /// <summary>
        /// Retrieves all card definitions asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of all card definitions.</returns>
        /// <remarks>
        /// In a more complex scenario, this might return a DTO instead of the domain entity.
        /// </remarks>
        Task<IEnumerable<CardDefinition>> GetAllCardsAsync();

        // Other card-related application logic methods can be added here.
    }
}