using System.Collections.Generic;
using System.Text;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class DeckCodeServiceTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_PreservesCards()
    {
        var service = new DeckCodeService();
        var cards = new List<DeckCardEntryDto>
        {
            new DeckCardEntryDto { CardDefinitionId = 1001, Count = 3 },
            new DeckCardEntryDto { CardDefinitionId = 2001, Count = 2 }
        };

        var code = service.Encode(
            CardClass.Runecraft,
            CardClass.Forestcraft,
            DeckEnvironment.Open,
            DeckRule.DualClass,
            cards);

        var success = service.TryDecode(code, out var deckClass, out var secondaryClass, out var environment, out var mode, out var decoded, out var error);

        Assert.True(success, error);
        Assert.Equal(CardClass.Runecraft, deckClass);
        Assert.Equal(CardClass.Forestcraft, secondaryClass);
        Assert.Equal(DeckEnvironment.Open, environment);
        Assert.Equal(DeckRule.DualClass, mode);

        Assert.Collection(decoded,
            entry =>
            {
                Assert.Equal(1001, entry.CardDefinitionId);
                Assert.Equal(3, entry.Count);
            },
            entry =>
            {
                Assert.Equal(2001, entry.CardDefinitionId);
                Assert.Equal(2, entry.Count);
            });
    }

    [Fact]
    public void Decode_Rejects_InvalidPrefix()
    {
        var service = new DeckCodeService();
        var success = service.TryDecode("BADPREFIX", out _, out _, out _, out _, out _, out var error);

        Assert.False(success);
        Assert.Contains("prefix", error ?? string.Empty);
    }

    [Fact]
    public void Decode_V1Code_RemainsSupported()
    {
        var service = new DeckCodeService();
        var legacyCode = "SVB1eyJ2IjoxLCJjbGFzcyI6IlJ1bmVjcmFmdCIsImNhcmRzIjpbWzEwMDEsM11dfQ";

        var success = service.TryDecode(legacyCode, out var deckClass, out var secondaryClass, out var environment, out var mode, out var cards, out var error);

        Assert.True(success, error);
        Assert.Equal(CardClass.Runecraft, deckClass);
        Assert.Null(secondaryClass);
        Assert.Null(environment);
        Assert.Null(mode);
        Assert.Single(cards);
        Assert.Equal(1001, cards[0].CardDefinitionId);
        Assert.Equal(3, cards[0].Count);
    }

    [Fact]
    public void Decode_V2CodeWithInvalidMode_ReturnsError()
    {
        var service = new DeckCodeService();
        var payloadJson = "{\"v\":2,\"class\":\"Runecraft\",\"environment\":\"Open\",\"mode\":\"UnknownMode\",\"cards\":[[1001,3]]}";
        var code = "SVB2" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var success = service.TryDecode(code, out _, out _, out _, out _, out _, out var error);

        Assert.False(success);
        Assert.Contains("mode", error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
