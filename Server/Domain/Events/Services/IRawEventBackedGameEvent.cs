using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IRawEventBackedGameEvent
    {
        JsonObject OutputPayload { get; }
    }
}

