using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    public static class PlayerScopeResolver
    {
        public static IReadOnlyList<PlayerState> ResolvePlayers(GameState gameState, Guid requestingPlayerId, PlayerScope scope)
        {
            var players = new List<PlayerState>();
            var requestingPlayer = gameState.GetPlayerState(requestingPlayerId);
            if (requestingPlayer == null)
            {
                return players;
            }

            switch (scope)
            {
                case PlayerScope.Self:
                    players.Add(requestingPlayer);
                    break;
                case PlayerScope.Opponent:
                    var opponent = gameState.GetOpponentPlayerState(requestingPlayerId);
                    if (opponent != null)
                    {
                        players.Add(opponent);
                    }
                    break;
                case PlayerScope.Both:
                    players.Add(requestingPlayer);
                    var other = gameState.GetOpponentPlayerState(requestingPlayerId);
                    if (other != null && other.PlayerId != requestingPlayer.PlayerId)
                    {
                        players.Add(other);
                    }
                    break;
                default:
                    players.Add(requestingPlayer);
                    break;
            }

            return players;
        }

        public static IReadOnlyList<TargetInfo> ResolvePlayerTargets(GameState gameState, Guid requestingPlayerId, PlayerScope scope)
        {
            return ResolvePlayers(gameState, requestingPlayerId, scope)
                .Select(TargetInfo.ForPlayer)
                .ToList();
        }
    }
}
