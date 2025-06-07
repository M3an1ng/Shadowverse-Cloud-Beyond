using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities; // Assuming CardInstance is here

namespace ShadowverseBeyond.Server.Domain.Query.Operations
{
    /// <summary>
    /// Query operation to limit the number of cards in the result set.
    /// </summary>
    public class LimitOperation : IQueryOperation
    {
        public int Count { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitOperation"/> class.
        /// </summary>
        /// <param name="count">The maximum number of cards to return. Must be non-negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if count is negative.</exception>
        public LimitOperation(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Limit count cannot be negative.");
            }
            Count = count;
        }

        public Task<IEnumerable<CardInstance>> ApplyAsync(IEnumerable<CardInstance> cards, QueryContext context)
        {
            // Take applies the limit. If the collection has fewer items than Count, it takes all of them.
            return Task.FromResult(cards.Take(Count));
        }
    }
}