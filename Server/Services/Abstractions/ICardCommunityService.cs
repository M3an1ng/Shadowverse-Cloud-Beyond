using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface ICardCommunityService
{
    Task<CardVoteSummaryDto> GetVoteSummaryAsync(
        int cardId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CardCommentDto> CreateCommentAsync(
        int cardId,
        User user,
        string content,
        CancellationToken cancellationToken = default);

    Task<CardVoteSummaryDto> VoteAsync(
        int cardId,
        User user,
        CardVoteType voteType,
        CancellationToken cancellationToken = default);
}
