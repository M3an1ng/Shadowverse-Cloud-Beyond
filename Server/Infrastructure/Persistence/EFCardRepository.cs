//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
// - 错误处理与日志系统: 文档/05-游戏系统/08-错误处理与日志系统.md
//
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities; // Corrected prefix was already applied (Good!)
using ShadowverseBeyond.Server.Domain.Interfaces; // Corrected prefix was already applied (Good!)
using ShadowverseBeyond.Server.Infrastructure.Data; // Corrected prefix was already applied (Good!)
using System.Collections.Generic;
using System.Linq; // Added for Enumerable.Empty
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos; // Add missing using

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

        /// <inheritdoc />
        public async Task<IReadOnlyList<CardDefinition>> GetAllCardDefinitionsAsync() // Match interface return type
        {
            // Use AsNoTracking for read-only queries to improve performance
            var cardDefinitions = await _context.CardDefinitions
                .AsNoTracking()
                .ToListAsync();

            return cardDefinitions.AsReadOnly(); // Convert to IReadOnlyList
        }

        /// <inheritdoc />
        public async Task<CardDefinition?> GetCardDefinitionByIdAsync(int id) // Match interface return type (parameter name differs, but type matches)
        {
            // Use FindAsync for querying by primary key - it's optimized for single entity retrieval
            return await _context.CardDefinitions.FindAsync(id);
        }
    }
}