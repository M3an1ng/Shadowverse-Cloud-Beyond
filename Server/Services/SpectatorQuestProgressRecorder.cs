using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed class SpectatorQuestProgressRecorder : ISpectatorQuestProgressRecorder
{
    private readonly IQuestProgressService _questProgressService;

    public SpectatorQuestProgressRecorder(IQuestProgressService questProgressService)
    {
        _questProgressService = questProgressService;
    }

    public async Task RecordSpectateAsync(
        string? spectatorUserId,
        string activeGameId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(spectatorUserId, out var userId) ||
            string.IsNullOrWhiteSpace(activeGameId))
        {
            return;
        }

        await _questProgressService.RecordActivityAsync(
            userId,
            QuestActivityType.SpectateMatch,
            "Match",
            activeGameId.Trim(),
            cancellationToken: cancellationToken);
    }
}
