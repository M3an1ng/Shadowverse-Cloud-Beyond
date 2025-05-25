using Server.Application.Interfaces; // The service interface
using Server.Domain.Entities;      // Domain entity
using Server.Domain.Interfaces;    // Repository interface
using Microsoft.Extensions.Logging;       // For logging (optional but good practice)
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Application.Services
{
    /// <summary>
    /// Implements application logic related to cards.
    /// </summary>
    public class CardService : ICardService
    {
        private readonly ICardRepository _cardRepository;
        private readonly ILogger<CardService> _logger; // Optional logger

        /// <summary>
        /// Initializes a new instance of the <see cref="CardService"/> class.
        /// </summary>
        /// <param name="cardRepository">The card repository.</param>
        /// <param name="logger">The logger.</param>
        public CardService(ICardRepository cardRepository, ILogger<CardService> logger)
        {
            _cardRepository = cardRepository;
            _logger = logger; // Store logger
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CardDefinition>> GetAllCardsAsync()
        {
            try
            {
                _logger?.LogInformation("Fetching all cards from repository."); // Example logging
                var cards = await _cardRepository.GetAllAsync();
                _logger?.LogInformation("Successfully fetched {CardCount} cards.", cards?.Count() ?? 0);
                return cards ?? System.Linq.Enumerable.Empty<CardDefinition>(); // Return empty list if cards is null
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "An error occurred while fetching all cards.");
                // Depending on requirements, could re-throw, return empty list, or handle differently
                throw; // Re-throw for now to let higher layers handle it
            }
        }
    }
}