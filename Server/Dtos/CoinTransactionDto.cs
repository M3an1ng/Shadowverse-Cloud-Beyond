using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 金币交易记录数据传输对象
/// </summary>
public class CoinTransactionDto
{
    /// <summary>
    /// 交易ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 金币数量 (正数=收入，负数=支出)
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// 交易类型
    /// </summary>
    public CoinTransactionType Type { get; set; }

    /// <summary>
    /// 交易类型显示名称
    /// </summary>
    public string TypeDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 交易描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 关联的实体ID
    /// </summary>
    public string? RelatedEntityId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 是否为收入
    /// </summary>
    public bool IsIncome { get; set; }

    /// <summary>
    /// 从实体转换为DTO
    /// </summary>
    /// <param name="transaction">金币交易实体</param>
    /// <returns>金币交易DTO</returns>
    public static CoinTransactionDto FromEntity(CoinTransaction transaction)
    {
        return new CoinTransactionDto
        {
            Id = transaction.Id,
            UserId = transaction.UserId,
            Amount = transaction.Amount,
            Type = transaction.Type,
            TypeDisplayName = transaction.GetTypeDisplayName(),
            Description = transaction.Description,
            RelatedEntityId = transaction.RelatedEntityId,
            CreatedAt = transaction.CreatedAt,
            IsIncome = transaction.IsIncome()
        };
    }
}
