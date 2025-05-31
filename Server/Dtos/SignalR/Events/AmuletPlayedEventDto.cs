using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that an Amulet card has been played to the field.
    /// </summary>
    public class AmuletPlayedEventDto : GameEventDto
    {
        // Removed the problematic 'override Type' property

        /// <summary>
        /// The identifier of the player who played the amulet. This corresponds to SourcePlayerId in the base DTO.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier (instance ID) assigned to this amulet on the field.
        /// </summary>
        public string AmuletUid { get; set; } = string.Empty;

        /// <summary>
        /// Information about the amulet card that was played.
        /// We might need a simplified DTO later (AmuletStateDto?), but use CardDto for now.
        /// </summary>
        public CardDto AmuletCard { get; set; } = null!; // Assuming non-nullable for played card info

        /// <summary>
        /// The position (index) where the amulet was placed on the player's board.
        /// </summary>
        public int BoardIndex { get; set; }

        /// <summary>
        /// The Play Point cost paid to play the amulet.
        /// </summary>
        public int CostPaid { get; set; }

        // Constructor
        public AmuletPlayedEventDto(string playerId, string amuletUid, CardDto amuletCard, int boardIndex, int costPaid)
            : base(playerId, "AmuletPlayed") // Call base constructor, using PlayerId as sourcePlayerId
        {
            PlayerId = playerId;
            AmuletUid = amuletUid;
            AmuletCard = amuletCard;
            BoardIndex = boardIndex;
            CostPaid = costPaid;
        }

        // Consider adding initial countdown if applicable to the amulet type.
        // Or perhaps amulet state needs its own specific DTO like UnitStateDto.
    }
}