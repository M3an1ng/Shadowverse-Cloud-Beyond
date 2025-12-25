using System.Linq;
using System.Text;
using System.Text.Json;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class DeckCodeService : IDeckCodeService
{
    private const string Prefix = "SVB1";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Encode(CardClass deckClass, IReadOnlyList<DeckCardEntryDto> cards)
    {
        var payload = new DeckCodePayload
        {
            V = 1,
            Class = deckClass.ToString(),
            Cards = cards
                .OrderBy(card => card.CardDefinitionId)
                .Select(card => new[] { card.CardDefinitionId, card.Count })
                .ToList()
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Prefix + Base64UrlEncode(bytes);
    }

    public bool TryDecode(string code, out CardClass deckClass, out List<DeckCardEntryDto> cards, out string? error)
    {
        deckClass = CardClass.Neutral;
        cards = new List<DeckCardEntryDto>();
        error = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Deck code is empty.";
            return false;
        }

        var trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            error = "Deck code prefix is invalid.";
            return false;
        }

        var payloadToken = trimmed.Substring(Prefix.Length);
        if (string.IsNullOrWhiteSpace(payloadToken))
        {
            error = "Deck code payload is missing.";
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(payloadToken);
            var payload = JsonSerializer.Deserialize<DeckCodePayload>(payloadBytes, _jsonOptions);
            if (payload == null)
            {
                error = "Deck code payload is invalid.";
                return false;
            }

            if (payload.V != 1)
            {
                error = "Deck code version is not supported.";
                return false;
            }

            if (!Enum.TryParse(payload.Class, true, out deckClass))
            {
                error = "Deck class is invalid.";
                return false;
            }

            cards = payload.Cards
                .Where(entry => entry.Length >= 2)
                .Select(entry => new DeckCardEntryDto
                {
                    CardDefinitionId = entry[0],
                    Count = entry[1]
                })
                .ToList();

            return true;
        }
        catch (Exception ex)
        {
            error = $"Deck code decode failed: {ex.Message}";
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }

    private sealed class DeckCodePayload
    {
        public int V { get; set; }
        public string Class { get; set; } = string.Empty;
        public List<int[]> Cards { get; set; } = new();
    }
}
