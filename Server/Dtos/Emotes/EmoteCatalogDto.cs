using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Emotes
{
    public class EmoteCatalogDto
    {
        public IReadOnlyList<EmotePackDto> Packs { get; init; } = [];
    }
}
