// File: ShadowverseBeyond.Server.Dtos.SignalR.UnitPlayedEventDto.cs
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR
{
    /// <summary>
    /// Event DTO representing a unit (follower) being played onto the field.
    /// </summary>
    public class UnitPlayedEventDto : GameEventDto
    {
        /// <summary>
        /// Unique identifier for the unit instance on the field.
        /// </summary>
        public Guid UnitUid { get; set; }

        /// <summary>
        /// ID of the card definition from which this unit originates.
        /// </summary>
        public int CardDefinitionId { get; set; } // Or string CardId depending on definition key

        /// <summary>
        /// ID of the player who played the unit.
        /// </summary>
        public string PlayerId { get; set; }

        /// <summary>
        /// The 0-based index on the player's side of the field where the unit was placed.
        /// </summary>
        public int FieldIndex { get; set; }

        // Maybe add initial stats if needed? Or client derives from CardDefinitionId? Keep it simple for now.
        // public int InitialAttack { get; set; }
        // public int InitialDefense { get; set; }

        public UnitPlayedEventDto(Guid unitUid, int cardDefinitionId, string playerId, int fieldIndex)
            : base(playerId, "UnitPlayed") // Pass playerId as sourcePlayerId, set EventType
        {
            UnitUid = unitUid;
            CardDefinitionId = cardDefinitionId;
            PlayerId = playerId;
            FieldIndex = fieldIndex;
        }
    }
}