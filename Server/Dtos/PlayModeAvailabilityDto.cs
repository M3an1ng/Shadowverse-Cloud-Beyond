using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Describes availability for a single play mode.
    /// </summary>
    public class PlayModeAvailabilityDto
    {
        /// <summary>
        /// The play mode represented by this entry.
        /// </summary>
        public PlayMode Mode { get; set; }

        /// <summary>
        /// The effective cost to use this mode.
        /// </summary>
        public int EffectiveCost { get; set; }

        /// <summary>
        /// Whether the mode can currently be selected.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// The reason why the mode is unavailable, if any.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
}
