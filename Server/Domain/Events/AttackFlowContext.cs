using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Runtime-only attack flow context that scopes effects/events produced by one attack chain.
    /// </summary>
    public sealed class AttackFlowContext
    {
        private readonly HashSet<Guid> _scoredDestroyedTargetIds = new();

        public Guid RootEventId { get; }
        public Guid AttackerId { get; }
        public Guid DefenderId { get; }

        public AttackFlowContext(Guid rootEventId, Guid attackerId, Guid defenderId)
        {
            RootEventId = rootEventId;
            AttackerId = attackerId;
            DefenderId = defenderId;
        }

        public bool Matches(Guid attackerId, Guid defenderId)
        {
            return AttackerId == attackerId && DefenderId == defenderId;
        }

        public bool TryMarkScoredDestroyedTarget(Guid targetId)
        {
            return _scoredDestroyedTargetIds.Add(targetId);
        }
    }
}
