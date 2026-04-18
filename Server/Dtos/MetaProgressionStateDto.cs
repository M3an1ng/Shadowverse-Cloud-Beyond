namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 元进度状态聚合视图（MVP）
/// </summary>
public sealed class MetaProgressionStateDto
{
    public Guid UserId { get; init; }
    public UserRankDto? Rank { get; init; }
    public List<UserQuestDto> Quests { get; init; } = new();
    public List<UserClassWinStatsDto> ClassStats { get; init; } = new();
}
