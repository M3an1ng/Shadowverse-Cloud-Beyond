using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Playability info for a specific Act ability.
    /// </summary>
    public class ActAbilityPlayabilityDto
    {
        public Guid AbilityRuntimeId { get; set; }
        public string AbilityName { get; set; } = string.Empty;
        public int ActCost { get; set; }
        public bool IsPlayable { get; set; }
        public bool RequiresTargetSelection { get; set; }
        public bool HasValidTargets { get; set; } = true;
        public string FailureReason { get; set; } = string.Empty;
        public List<Guid> ValidTargetIds { get; set; } = new();
    }
}
