namespace ShadowverseBeyond.Server.Domain.Entities;

public static class CardVoteLabels
{
    public static string ToLabel(CardVoteType voteType) => voteType switch
    {
        CardVoteType.Upvote => "up",
        CardVoteType.Downvote => "down",
        _ => throw new ArgumentOutOfRangeException(nameof(voteType), voteType, "Unsupported card vote type.")
    };
}
