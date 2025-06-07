using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities; // Assuming CardInstance is here

namespace ShadowverseBeyond.Server.Domain.Query.Operations
{
    /// <summary>
    /// Represents a single operation (like filtering, sorting, limiting)
    /// to be applied to a collection of cards during a query.
    /// </summary>
    public interface IQueryOperation
    {
        /// <summary>
        /// Asynchronously applies the operation to the given card collection.
        /// </summary>
        /// <param name="cards">The current collection of cards.</param>
        /// <param name="context">The query context (e.g., game state, source card).</param>
        /// <returns>A new collection of cards after applying the operation.</returns>
        Task<IEnumerable<CardInstance>> ApplyAsync(IEnumerable<CardInstance> cards, QueryContext context);

        // Consider adding properties for validation or description if needed
        // string Description { get; }
        // bool IsValid();
    }

    /// <summary>
    /// Provides context for query operations.
    /// This might include game state, the player performing the query, the source card, etc.
    /// Needs to be defined based on actual requirements.
    /// </summary>
    public class QueryContext
    {
        public GameState GameState { get; } // Example property
        public PlayerState QueryingPlayer { get; } // Example property
        public CardInstance? SourceCard { get; } // Example property

        public QueryContext(GameState gameState, PlayerState queryingPlayer, CardInstance? sourceCard = null)
        {
            GameState = gameState;
            QueryingPlayer = queryingPlayer;
            SourceCard = sourceCard;
        }

        // Add methods to resolve dynamic values or access game data if needed
    }
}