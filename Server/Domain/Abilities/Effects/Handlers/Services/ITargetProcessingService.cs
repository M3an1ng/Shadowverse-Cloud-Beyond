using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    public interface ITargetProcessingService
    {
        GameStateActionResult ProcessTargets(
            GameContext context,
            Func<PlayerState, GameContext, DomainOperationResult>? playerAction,
            Func<CardInstance, GameContext, DomainOperationResult>? cardAction,
            string effectName);
    }
}