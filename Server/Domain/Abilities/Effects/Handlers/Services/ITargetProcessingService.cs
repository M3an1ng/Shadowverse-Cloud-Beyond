using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    public interface ITargetProcessingService
    {
        GameStateActionResult ProcessTargets(
            GameContext context,
            IReadOnlyList<TargetInfo> targets,
            Func<TargetInfo, GameContext, DomainOperationResult> action,
            string effectName);
    }
}