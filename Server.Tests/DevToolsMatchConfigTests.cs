using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Constants;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.Devtools;

public class DevToolsMatchConfigTests
{
    [Fact]
    public void TryBuildConfig_AllowsStartingTurnWithinRange()
    {
        var request = new DevToolsMatchesController.DevMatchConfigRequest
        {
            StartingTurn = 6
        };

        var success = DevToolsMatchesController.TryBuildConfig(request, out var config, out var error);

        Assert.True(success);
        Assert.NotNull(config);
        Assert.Equal(6, config!.StartingTurn);
        Assert.Null(error);
    }

    [Fact]
    public void TryBuildConfig_RejectsStartingTurnBelowRange()
    {
        var request = new DevToolsMatchesController.DevMatchConfigRequest
        {
            StartingTurn = 0
        };

        var success = DevToolsMatchesController.TryBuildConfig(request, out var config, out var error);

        Assert.False(success);
        Assert.Null(config);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryBuildConfig_RejectsStartingTurnAboveRange()
    {
        var request = new DevToolsMatchesController.DevMatchConfigRequest
        {
            StartingTurn = GameConstants.Timing.MaxTurnsBeforeDraw + 1
        };

        var success = DevToolsMatchesController.TryBuildConfig(request, out var config, out var error);

        Assert.False(success);
        Assert.Null(config);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
