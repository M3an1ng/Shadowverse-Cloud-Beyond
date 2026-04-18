using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IDeckValidationService
{
    Task<DeckOperationResult<List<DeckCardEntryDto>>> ValidateAsync(
        CardClass primaryClass,
        CardClass? secondaryClass,
        IReadOnlyList<DeckCardEntryDto> cards,
        DeckEnvironment environment,
        DeckRule rule,
        bool requireComplete,
        CancellationToken cancellationToken);
}
