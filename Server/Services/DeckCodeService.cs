using System.Linq;
using System.Text;
using System.Text.Json;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class DeckCodeService : IDeckCodeService
{
    private const string PrefixV1 = "SVB1";
    private const string PrefixV2 = "SVB2";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Encode(
        CardClass deckClass,
        CardClass? secondaryClass,
        DeckEnvironment? environment,
        DeckRule? rule,
        IReadOnlyList<DeckCardEntryDto> cards)
    {
        var payload = new DeckCodePayloadV2
        {
            V = 2,
            Class = deckClass.ToString(),
            SecondaryClass = secondaryClass?.ToString(),
            Environment = environment?.ToString(),
            Rule = rule?.ToString(),
            Cards = cards
                .OrderBy(card => card.CardDefinitionId)
                .Select(card => new[] { card.CardDefinitionId, card.Count })
                .ToList()
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return PrefixV2 + Base64UrlEncode(bytes);
    }

    public bool TryDecode(
        string code,
        out CardClass deckClass,
        out CardClass? secondaryClass,
        out DeckEnvironment? environment,
        out DeckRule? rule,
        out List<DeckCardEntryDto> cards,
        out string? error)
    {
        deckClass = CardClass.Neutral;
        secondaryClass = null;
        environment = null;
        rule = null;
        cards = new List<DeckCardEntryDto>();
        error = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Deck code is empty.";
            return false;
        }

        var trimmed = code.Trim();
        if (trimmed.StartsWith(PrefixV2, StringComparison.Ordinal))
        {
            return TryDecodeV2(trimmed, out deckClass, out secondaryClass, out environment, out rule, out cards, out error);
        }

        if (trimmed.StartsWith(PrefixV1, StringComparison.Ordinal))
        {
            return TryDecodeV1(trimmed, out deckClass, out cards, out error);
        }

        error = "Deck code prefix is invalid.";
        return false;
    }

    private bool TryDecodeV2(
        string code,
        out CardClass deckClass,
        out CardClass? secondaryClass,
        out DeckEnvironment? environment,
        out DeckRule? rule,
        out List<DeckCardEntryDto> cards,
        out string? error)
    {
        deckClass = CardClass.Neutral;
        secondaryClass = null;
        environment = null;
        rule = null;
        cards = new List<DeckCardEntryDto>();
        error = null;

        var payloadToken = code.Substring(PrefixV2.Length);
        if (string.IsNullOrWhiteSpace(payloadToken))
        {
            error = "Deck code payload is missing.";
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(payloadToken);
            var payload = JsonSerializer.Deserialize<DeckCodePayloadV2>(payloadBytes, _jsonOptions);
            if (payload == null)
            {
                error = "Deck code payload is invalid.";
                return false;
            }

            if (payload.V != 2)
            {
                error = "Deck code version is not supported.";
                return false;
            }

            if (!Enum.TryParse(payload.Class, true, out deckClass) || !Enum.IsDefined(deckClass))
            {
                error = "Deck class is invalid.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(payload.SecondaryClass))
            {
                if (!Enum.TryParse(payload.SecondaryClass, true, out CardClass parsedSecondary) || !Enum.IsDefined(parsedSecondary))
                {
                    error = "Deck secondary class is invalid.";
                    return false;
                }

                secondaryClass = parsedSecondary;
            }

            if (!string.IsNullOrWhiteSpace(payload.Environment))
            {
                if (!Enum.TryParse(payload.Environment, true, out DeckEnvironment parsedEnvironment) || !Enum.IsDefined(parsedEnvironment))
                {
                    error = "Deck environment is invalid.";
                    return false;
                }

                environment = parsedEnvironment;
            }

            if (!string.IsNullOrWhiteSpace(payload.Rule))
            {
                if (!Enum.TryParse(payload.Rule, true, out DeckRule parsedRule) || !Enum.IsDefined(parsedRule))
                {
                    error = "Deck rule is invalid.";
                    return false;
                }

                if (parsedRule != DeckRule.Standard && parsedRule != DeckRule.DualClass)
                {
                    error = "Deck rule is not supported.";
                    return false;
                }

                rule = parsedRule;
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

    private bool TryDecodeV1(string code, out CardClass deckClass, out List<DeckCardEntryDto> cards, out string? error)
    {
        deckClass = CardClass.Neutral;
        cards = new List<DeckCardEntryDto>();
        error = null;

        try
        {
            var payloadToken = code.Substring(PrefixV1.Length);
            if (string.IsNullOrWhiteSpace(payloadToken))
            {
                error = "Deck code payload is missing.";
                return false;
            }

            var payloadBytes = Base64UrlDecode(payloadToken);
            var payload = JsonSerializer.Deserialize<DeckCodePayloadV1>(payloadBytes, _jsonOptions);
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

            if (!Enum.TryParse(payload.Class, true, out deckClass) || !Enum.IsDefined(deckClass))
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

    private sealed class DeckCodePayloadV1
    {
        public int V { get; set; }
        public string Class { get; set; } = string.Empty;
        public List<int[]> Cards { get; set; } = new();
    }

    private sealed class DeckCodePayloadV2
    {
        public int V { get; set; }
        public string Class { get; set; } = string.Empty;
        public string? SecondaryClass { get; set; }
        public string? Environment { get; set; }
        public string? Rule { get; set; }
        public List<int[]> Cards { get; set; } = new();
    }
}
