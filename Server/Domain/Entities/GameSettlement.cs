using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Records a per-user settlement for a finished game to guarantee idempotency.
/// </summary>
public class GameSettlement
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string GameId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public GameResult Result { get; set; }

    public Guid WinnerUserId { get; set; }

    public DateTime SettledAtUtc { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

