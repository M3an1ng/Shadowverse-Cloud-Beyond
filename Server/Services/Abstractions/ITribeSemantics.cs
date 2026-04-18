using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface ITribeSemantics
    {
        IReadOnlyCollection<string> Expand(IEnumerable<string>? sourceTribes);

        bool Contains(IEnumerable<string>? sourceTribes, string? expectedTribe);

        bool Overlaps(IEnumerable<string>? left, IEnumerable<string>? right);
    }
}
