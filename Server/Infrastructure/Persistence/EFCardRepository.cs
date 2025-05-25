using Microsoft.EntityFrameworkCore;
using Server.Domain.Entities; // Assuming CardDefinition entity is here
using Server.Domain.Interfaces; // The interface we just created
using Server.Infrastructure.Data; // Assuming ApplicationDbContext is here
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Infrastructure.Persistence
{
    /// <summary>
    /// Entity Framework Core implementation of the card repository.
    /// </summary>
    public class EFCardRepository : ICardRepository
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="EFCardRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public EFCardRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CardDefinition>> GetAllAsync()
        {
            // Use AsNoTracking for read-only queries to improve performance.
            return await _context.CardDefinitions
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        /// <inheritdoc /> // Corrected line 33
        public async Task<CardDefinition?> GetCardByIdAsync(string id) // Corrected line 34
        {
            // FindAsync is suitable for querying by primary key and returns null if not found.
            return await _context.CardDefinitions.FindAsync(id);
        }
    }
}