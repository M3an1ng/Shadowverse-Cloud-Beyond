//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 数据库设计: 文档/04-数据库设计/01-实体关系图.md
// - 错误处理与日志系统: 文档/05-游戏系统/08-错误处理与日志系统.md
//
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// 事务处理服务实现
/// 遵循单一职责原则，专门负责数据库事务管理
/// 提供统一的事务处理模式，减少代码重复
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(ApplicationDbContext context, ILogger<TransactionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 在事务中执行操作
    /// 提供统一的错误处理和日志记录
    /// </summary>
    /// <typeparam name="TResult">操作返回类型</typeparam>
    /// <param name="operation">要执行的操作</param>
    /// <returns>操作结果</returns>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        // 如果已经在事务中，直接执行操作
        if (IsInTransaction())
        {
            _logger.LogDebug("Already in transaction, executing operation directly");
            return await operation();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogDebug("Starting database transaction");

            var result = await operation();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogDebug("Transaction committed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed, rolling back. Exception type: {ExceptionType}", ex.GetType().Name);

            try
            {
                await transaction.RollbackAsync();
                _logger.LogDebug("Transaction rollback completed successfully");
            }
            catch (Exception rollbackException)
            {
                _logger.LogCritical(rollbackException, "Critical error: Failed to rollback transaction. Data integrity may be compromised");
            }

            throw;
        }
    }

    /// <summary>
    /// 在事务中执行无返回值的操作
    /// </summary>
    /// <param name="operation">要执行的操作</param>
    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await ExecuteInTransactionAsync(async () =>
        {
            await operation();
            return true; // 返回一个占位值
        });
    }

    /// <summary>
    /// 检查当前是否在事务中
    /// </summary>
    /// <returns>是否在事务中</returns>
    public bool IsInTransaction()
    {
        return _context.Database.CurrentTransaction != null;
    }
}
