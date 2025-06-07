namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// 事务处理服务接口
/// 遵循依赖倒置原则，提供统一的事务处理抽象
/// 遵循接口隔离原则，专门负责事务管理
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// 在事务中执行操作
    /// 提供统一的事务处理模式，确保数据一致性
    /// </summary>
    /// <typeparam name="TResult">操作返回类型</typeparam>
    /// <param name="operation">要执行的操作</param>
    /// <returns>操作结果</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation);

    /// <summary>
    /// 在事务中执行无返回值的操作
    /// </summary>
    /// <param name="operation">要执行的操作</param>
    Task ExecuteInTransactionAsync(Func<Task> operation);

    /// <summary>
    /// 检查当前是否在事务中
    /// </summary>
    /// <returns>是否在事务中</returns>
    bool IsInTransaction();
}
