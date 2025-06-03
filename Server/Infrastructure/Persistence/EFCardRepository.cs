using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities; // Corrected prefix was already applied (Good!)
using ShadowverseBeyond.Server.Domain.Interfaces; // Corrected prefix was already applied (Good!)
using ShadowverseBeyond.Server.Infrastructure.Data; // Corrected prefix was already applied (Good!)
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence // Corrected prefix was already applied (Good!)
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
        public async Task<CardDefinition?> GetCardByIdAsync(int id) // Change parameter type to int
        {
            // FindAsync is suitable for querying by primary key and returns null if not found.
            // It works with int primary keys directly.
            return await _context.CardDefinitions.FindAsync(id);
        }
    }
}