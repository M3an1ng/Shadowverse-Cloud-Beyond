using Moq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class SpectatorQuestProgressRecorderTests
{
    [Fact]
    public async Task RecordSpectateAsync_WithPersistedUserId_RecordsSpectateMatchActivity()
    {
        var userId = Guid.NewGuid();
        var progress = new Mock<IQuestProgressService>();
        progress
            .Setup(service => service.RecordActivityAsync(
                userId,
                QuestActivityType.SpectateMatch,
                "Match",
                "game-1",
                1,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuestProgressResult
            {
                UserId = userId,
                ActivityType = QuestActivityType.SpectateMatch
            });
        var recorder = new SpectatorQuestProgressRecorder(progress.Object);

        await recorder.RecordSpectateAsync(userId.ToString(), "game-1");

        progress.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anonymous")]
    public async Task RecordSpectateAsync_WithAnonymousOrInvalidUserId_DoesNotRecordProgress(string? spectatorUserId)
    {
        var progress = new Mock<IQuestProgressService>(MockBehavior.Strict);
        var recorder = new SpectatorQuestProgressRecorder(progress.Object);

        await recorder.RecordSpectateAsync(spectatorUserId, "game-1");

        progress.VerifyNoOtherCalls();
    }
}
