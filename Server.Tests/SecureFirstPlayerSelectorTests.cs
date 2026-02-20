using System;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class SecureFirstPlayerSelectorTests
{
    [Fact]
    public void IsPlayer1First_ReturnsTrue_WhenRollIsZero()
    {
        var sut = new SecureFirstPlayerSelector((_, _) => 0);

        var result = sut.IsPlayer1First();

        Assert.True(result);
    }

    [Fact]
    public void IsPlayer1First_ReturnsFalse_WhenRollIsOne()
    {
        var sut = new SecureFirstPlayerSelector((_, _) => 1);

        var result = sut.IsPlayer1First();

        Assert.False(result);
    }

    [Fact]
    public void IsPlayer1First_Throws_WhenRollIsOutOfRange()
    {
        var sut = new SecureFirstPlayerSelector((_, _) => 2);

        Assert.Throws<InvalidOperationException>(() => sut.IsPlayer1First());
    }
}
