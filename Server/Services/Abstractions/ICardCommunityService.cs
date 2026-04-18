using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface ICardCommunityService
{
    Task<CardCommunityController.CardCommentDto> CreateCommentAsync(
        int cardId,
        User user,
        string content,
        CancellationToken cancellationToken = default);

    Task<CardCommunityController.CardVoteSummaryDto> VoteAsync(
        int cardId,
        User user,
        CardVoteType voteType,
        CancellationToken cancellationToken = default);
}
