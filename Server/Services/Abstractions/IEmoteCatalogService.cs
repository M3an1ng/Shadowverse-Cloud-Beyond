using ShadowverseBeyond.Server.Dtos.Emotes;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IEmoteCatalogService
    {
        EmoteCatalogDto GetCatalog();
        bool TryResolveEmote(string? emoteId, out EmoteItemDto? emote);
    }
}
