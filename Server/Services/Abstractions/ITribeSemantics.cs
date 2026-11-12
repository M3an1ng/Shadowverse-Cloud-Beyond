using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface ITribeSemantics
    {
        IReadOnlyCollection<string> Expand(IEnumerable<string>? sourceTribes, GameMode? mode = null);

        bool Contains(IEnumerable<string>? sourceTribes, string? expectedTribe, GameMode? mode = null);

        bool Overlaps(IEnumerable<string>? left, IEnumerable<string>? right, GameMode? mode = null);
    }
}
