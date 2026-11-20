using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ShadowverseBeyond.Server.Services;

public sealed class ReplaySeatTicket(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Shadowverse.Replay.Participant.v1");

    public string Issue(Guid game, Guid player) => protector.Protect($"{game:D}/{player:D}");

    public (string GameId, string PlayerId)? Read(string? ticket)
    {
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 1024) return null;
        try
        {
            var parts = protector.Unprotect(ticket).Split('/');
            if (parts.Length == 2 && Guid.TryParse(parts[0], out var game) && Guid.TryParse(parts[1], out var player))
                return (game.ToString(), player.ToString());
        }
        catch (CryptographicException) { }
        return null;
    }
}
