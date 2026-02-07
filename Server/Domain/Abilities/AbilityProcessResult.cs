using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Represents the result of processing a single ability.
    /// It can indicate success (with created effects), failure, or a pause for player input.
    /// </summary>
    public class AbilityProcessResult
    {
        public bool IsSuccess { get; set; }
        public bool IsCancelled { get; set; }
        public List<Effect> Effects { get; set; } = new();
        public List<IGameEvent> GeneratedEvents { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public static AbilityProcessResult Success(List<Effect> effects, List<IGameEvent>? generatedEvents = null)
        {
            return new AbilityProcessResult
            {
                IsSuccess = true,
                Effects = effects,
                GeneratedEvents = generatedEvents ?? new List<IGameEvent>()
            };
        }

        public static AbilityProcessResult Failure(string errorMessage)
        {
            return new AbilityProcessResult { IsSuccess = false, ErrorMessage = errorMessage };
        }

        public static AbilityProcessResult Cancelled(string? errorMessage = null)
        {
            return new AbilityProcessResult { IsSuccess = false, IsCancelled = true, ErrorMessage = errorMessage };
        }
    }
}
