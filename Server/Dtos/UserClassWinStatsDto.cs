namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 用户按职业聚合的胜场统计
/// </summary>
public sealed class UserClassWinStatsDto
{
    public string DeckClass { get; init; } = string.Empty;
    public int TotalGames { get; init; }
    public int TotalWins { get; init; }
}
