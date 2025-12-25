using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Marks an event as eligible for the client-facing event log, and provides
    /// the human-readable (or command-like) announcement text.
    /// </summary>
    public interface ILogAnnounceableEvent
    {
        string GetAnnouncement(GameState gameState);
    }
}
