using System.Collections.Generic;
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

        var code = service.Encode(CardClass.Runecraft, cards);

        var success = service.TryDecode(code, out var deckClass, out var decoded, out var error);

        Assert.True(success, error);
        Assert.Equal(CardClass.Runecraft, deckClass);

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
        var success = service.TryDecode("BADPREFIX", out _, out _, out var error);

        Assert.False(success);
        Assert.Contains("prefix", error ?? string.Empty);
    }
}
