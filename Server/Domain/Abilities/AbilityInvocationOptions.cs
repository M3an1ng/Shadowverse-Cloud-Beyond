namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Invocation-scoped overrides applied when an effect explicitly triggers another runtime ability.
    /// </summary>
    public record AbilityInvocationOptions
    {
        /// <summary>
        /// When enabled, target selections that would normally require player choice
        /// are resolved automatically with a random valid subset.
        /// </summary>
        public bool AutoRandomTargetSelection { get; init; }

        public static AbilityInvocationOptions None { get; } = new();

        public bool HasOverrides => AutoRandomTargetSelection;
    }
}
