using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IDeckService
{
    Task<bool> HasAnyDeckAsync(DeckOwner owner, CancellationToken cancellationToken);
    Task<DeckOperationResult<bool>> EnsureDefaultDeckAsync(DeckOwner owner, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeckSummaryDto>> ListAsync(DeckOwner owner, CancellationToken cancellationToken);
    Task<DeckDetailDto?> GetAsync(DeckOwner owner, Guid deckId, CancellationToken cancellationToken);
    Task<DeckOperationResult<DeckDetailDto>> CreateAsync(DeckOwner owner, CreateDeckRequest request, CancellationToken cancellationToken);
    Task<DeckOperationResult<DeckDetailDto>> UpdateAsync(DeckOwner owner, Guid deckId, UpdateDeckRequest request, CancellationToken cancellationToken);
    Task<DeckOperationResult<bool>> DeleteAsync(DeckOwner owner, Guid deckId, CancellationToken cancellationToken);
    Task<DeckOperationResult<string>> ExportCodeAsync(DeckOwner owner, Guid deckId, CancellationToken cancellationToken);
    Task<DeckOperationResult<DeckDetailDto>> ImportCodeAsync(DeckOwner owner, ImportDeckCodeRequest request, CancellationToken cancellationToken);
}
