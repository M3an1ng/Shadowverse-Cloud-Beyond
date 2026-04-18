using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IDeckCodeService
{
    string Encode(
        CardClass deckClass,
        CardClass? secondaryClass,
        DeckEnvironment? environment,
        DeckRule? rule,
        IReadOnlyList<DeckCardEntryDto> cards);

    bool TryDecode(
        string code,
        out CardClass deckClass,
        out CardClass? secondaryClass,
        out DeckEnvironment? environment,
        out DeckRule? rule,
        out List<DeckCardEntryDto> cards,
        out string? error);
}
