using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IDeckValidationService
{
    Task<DeckOperationResult<List<DeckCardEntryDto>>> ValidateAsync(
        CardClass deckClass,
        IReadOnlyList<DeckCardEntryDto> cards,
        bool requireComplete,
        CancellationToken cancellationToken);
}
