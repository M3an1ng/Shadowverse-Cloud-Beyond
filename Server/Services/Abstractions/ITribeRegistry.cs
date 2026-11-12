using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface ITribeRegistry
    {
        string Normalize(string? raw);

        IReadOnlyList<string> NormalizeMany(IEnumerable<string>? rawValues);

        IReadOnlyCollection<string> GetKnownNormalTribes(GameMode mode = GameMode.Standard);

        bool TryGetDefinition(string? raw, out TribeDefinition definition);

        Task<IReadOnlyList<string>> NormalizeAndRegisterAsync(
            IEnumerable<string>? rawValues,
            CancellationToken cancellationToken = default,
            bool isMirageScope = false);

        Task RefreshAsync(CancellationToken cancellationToken = default);
    }
}
