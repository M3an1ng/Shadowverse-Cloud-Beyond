namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface ISpectatorQuestProgressRecorder
{
    Task RecordSpectateAsync(
        string? spectatorUserId,
        string activeGameId,
        CancellationToken cancellationToken = default);
}
