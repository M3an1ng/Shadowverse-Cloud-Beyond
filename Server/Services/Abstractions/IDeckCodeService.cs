using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IDeckCodeService
{
    string Encode(CardClass deckClass, IReadOnlyList<DeckCardEntryDto> cards);
    bool TryDecode(string code, out CardClass deckClass, out List<DeckCardEntryDto> cards, out string? error);
}
