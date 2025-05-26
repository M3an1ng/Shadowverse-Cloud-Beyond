// File: Server/Domain/Entities/EffectState.cs
namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of an ongoing effect on a unit or player.
    /// (Placeholder - Define actual properties as needed)
    /// </summary>
    public record EffectState
    {
        public string EffectId { get; init; } = string.Empty; // Unique identifier for the effect type or instance
        public string Name { get; init; } = "Unknown Effect"; // Display name or type name
        public int DurationTurns { get; set; } = -1; // Remaining duration in turns (-1 for permanent or not applicable)
        public bool IsActive { get; set; } = true;

        // Add other properties as needed: source, target, specific parameters, etc.
    }
}