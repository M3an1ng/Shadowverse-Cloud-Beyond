// Location: Server/Dtos/PlayabilityResultDto.cs
namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// DTO for returning the result of a card playability check.
    /// </summary>
    public class PlayabilityResultDto
    {
        /// <summary>
        /// Indicates if the card meets basic play conditions (cost, field space) and all pre-play conditions.
        /// </summary>
        public bool IsPlayable { get; set; }

        /// <summary>
        /// Indicates if playing this card requires the player to select a target for its effects.
        /// </summary>
        public bool RequiresTargetSelection { get; set; }

        /// <summary>
        /// If target selection is required for effects, indicates if there are currently valid targets available.
        /// </summary>
        public bool HasValidTargets { get; set; }

        /// <summary>
        /// If the card is not playable (IsPlayable == false), provides a brief reason.
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// If target selection is required for effects and valid targets exist, provides a list of their Instance IDs.
        /// Used for UI highlighting.
        /// </summary>
        public List<Guid> ValidTargetIds { get; set; } = new List<Guid>(); // Assuming InstanceId is int based on other DTOs/design
    }
}