using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For SelectionMethod, SortDirection
using ShadowverseBeyond.Server.Domain.Entities; // Assuming CardInstance is here

namespace ShadowverseBeyond.Server.Domain.Query.Operations
{
    /// <summary>
    /// Query operation to order a collection of cards based on specified criteria.
    /// </summary>
    public class OrderingOperation : IQueryOperation
    {
        /// <summary>
        /// The property to sort by (e.g., "Attack", "Cost", "PlayOrder").
        /// Can be null if using a predefined SelectionMethod like Random.
        /// </summary>
        public string? SortProperty { get; }

        /// <summary>
        /// The direction of the sort (Ascending or Descending).
        /// </summary>
        public SortDirection Direction { get; }

        /// <summary>
        /// Predefined selection method (e.g., Random, First, Last).
        /// If set to anything other than 'All', it might override SortProperty/Direction.
        /// </summary>
        public SelectionMethod Selection { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderingOperation"/> class using a predefined selection method.
        /// </summary>
        public OrderingOperation(SelectionMethod selection)
        {
            if (selection == SelectionMethod.All)
                throw new ArgumentException("SelectionMethod.All is not a valid ordering operation. Use specific methods like First, Last, Random, or provide SortProperty.", nameof(selection));

            Selection = selection;
            // SortProperty and Direction might be irrelevant for methods like Random, First, Last
            SortProperty = null;
            Direction = SortDirection.Ascending; // Default, might not be used
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderingOperation"/> class using a property and direction.
        /// </summary>
        public OrderingOperation(string sortProperty, SortDirection direction = SortDirection.Ascending)
        {
            if (string.IsNullOrWhiteSpace(sortProperty))
                throw new ArgumentException("Sort property cannot be null or whitespace.", nameof(sortProperty));

            Selection = SelectionMethod.All; // Indicates custom sorting
            SortProperty = sortProperty;
            Direction = direction;
        }


        public Task<IEnumerable<CardInstance>> ApplyAsync(IEnumerable<CardInstance> cards, QueryContext context)
        {
            IEnumerable<CardInstance> orderedCards;

            switch (Selection)
            {
                case SelectionMethod.Random:
                    // TODO: Use a seeded random based on context if needed for determinism
                    var random = new Random();
                    orderedCards = cards.OrderBy(c => random.Next());
                    break;
                case SelectionMethod.First:
                    // Assumes a default order (e.g., play order) if no prior sort applied.
                    // If a specific 'first' is needed (e.g., first played), prior sorting is required.
                    orderedCards = cards.Take(1); // Or should it preserve order and just mark the first? Depends on intent. Let's assume Take(1).
                    break;
                case SelectionMethod.Last:
                    // Similar assumption as First.
                    orderedCards = cards.Reverse().Take(1); // Simple reverse and take. Might need adjustment based on desired 'last' definition.
                    break;
                // Add cases for Lowest/Highest Attack/Health/Cost if SelectionMethod enum is expanded
                // case SelectionMethod.LowestAttack:
                //    orderedCards = cards.OrderBy(c => c.Attack); // Assuming CardInstance has Attack
                //    break;
                // case SelectionMethod.HighestAttack:
                //    orderedCards = cards.OrderByDescending(c => c.Attack);
                //    break;
                // ... etc.

                case SelectionMethod.All: // Apply custom sorting
                default: // Includes PlayerChoice which doesn't reorder here
                    if (!string.IsNullOrWhiteSpace(SortProperty))
                    {
                        // TODO: Implement dynamic property access and comparison
                        // This is complex and might require reflection or a dedicated property accessor system.
                        Console.WriteLine($"Warning: Ordering by property '{SortProperty}' not fully implemented.");
                        // Placeholder: Sort by InstanceId as a fallback
                        orderedCards = Direction == SortDirection.Ascending
                            ? cards.OrderBy(c => c.InstanceId)
                            : cards.OrderByDescending(c => c.InstanceId);
                    }
                    else
                    {
                        orderedCards = cards; // No specific ordering applied
                    }
                    break;
            }

            return Task.FromResult(orderedCards);
        }
    }
}