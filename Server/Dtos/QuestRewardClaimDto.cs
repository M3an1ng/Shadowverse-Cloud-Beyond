namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 任务奖励领取结果
/// </summary>
public sealed class QuestRewardClaimDto
{
    public Guid UserId { get; init; }
    public Guid UserQuestId { get; init; }
    public int QuestDefinitionId { get; init; }
    public int CoinReward { get; init; }
    public int CurrentCoins { get; init; }
    public DateTime ClaimedAtUtc { get; init; }
}
