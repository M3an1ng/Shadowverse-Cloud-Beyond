using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Emotes
{
    public class EmotePackDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public IReadOnlyList<EmoteItemDto> Items { get; init; } = [];
    }
}
