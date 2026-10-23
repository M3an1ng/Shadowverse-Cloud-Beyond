using System;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class PlayerEvolutionRightTests
    {
        [Fact]
        public void SepUpdate_DoesNotRestoreManualEvolutionRight_AfterItWasUsed()
        {
            var player = new PlayerState(Guid.NewGuid())
            {
                IsEvolutionUnlocked = true,
                IsSuperEvolutionUnlocked = true,
                MaxSuperEvolutionPoints = 2,
                CurrentSuperEvolutionPoints = 0,
                CanEvolve = false
            };

            var result = new SepUpdateEvent(player, 1).Commit(null!);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, player.CurrentSuperEvolutionPoints);
            Assert.False(player.CanEvolve);
            Assert.False(player.CanSuperEvolve);
        }

        [Fact]
        public void SepUpdate_DoesNotConsumeManualEvolutionRight_WhenPlayerHasNotEvolvedThisTurn()
        {
            var player = new PlayerState(Guid.NewGuid())
            {
                IsEvolutionUnlocked = true,
                IsSuperEvolutionUnlocked = true,
                MaxSuperEvolutionPoints = 2,
                CurrentSuperEvolutionPoints = 0,
                CanEvolve = true
            };

            var result = new SepUpdateEvent(player, 1).Commit(null!);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, player.CurrentSuperEvolutionPoints);
            Assert.True(player.CanEvolve);
            Assert.True(player.CanSuperEvolve);
        }

        [Fact]
        public void CanEvolve_IsFalse_WhenEvolutionIsNotUnlocked()
        {
            var player = new PlayerState(Guid.NewGuid())
            {
                IsEvolutionUnlocked = false,
                IsSuperEvolutionUnlocked = false
            };

            Assert.False(player.CanEvolve);
            Assert.False(player.CanSuperEvolve);
        }

        [Fact]
        public void CanEvolve_AndCanSuperEvolve_RespectTheirOwnUnlockFlags()
        {
            var player = new PlayerState(Guid.NewGuid())
            {
                IsEvolutionUnlocked = true,
                IsSuperEvolutionUnlocked = false
            };

            Assert.True(player.CanEvolve);
            Assert.False(player.CanSuperEvolve);
        }
    }
}
