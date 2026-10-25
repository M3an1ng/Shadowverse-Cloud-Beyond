namespace ShadowverseBeyond.Server.Dtos;

public class CardVoteSummaryDto
{
    public int CardDefinitionId { get; set; }
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public string? UserVote { get; set; }
    public bool RewardGranted { get; set; }
    public int RewardCoins { get; set; }
    public int? CurrentCoins { get; set; }
    public QuestProgressDto? QuestProgress { get; set; }
}

public class CardCommunityResponse : CardVoteSummaryDto
{
    public int TotalComments { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public List<CardCommentDto> Comments { get; set; } = new();
    public List<CardVoteActivityDto> VoteActivities { get; set; } = new();
}

public class CardVoteActivityDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string VoteType { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class CardCommentDto
{
    public Guid Id { get; set; }
    public int CardDefinitionId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool CanDelete { get; set; }
    public bool RewardGranted { get; set; }
    public int RewardCoins { get; set; }
    public int? CurrentCoins { get; set; }
    public QuestProgressDto? QuestProgress { get; set; }
}

public class QuestProgressDto
{
    public List<Guid> UpdatedQuestIds { get; set; } = new();
    public List<Guid> CompletedQuestIds { get; set; } = new();
    public int ClaimableCount { get; set; }
    public bool IgnoredAsDuplicate { get; set; }
}
