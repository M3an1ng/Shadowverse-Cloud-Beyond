using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Provides access to the current GameContext within a scoped request.
    /// This allows services to access the context of the currently processing event or action
    /// without needing it to be passed down through every method call.
    /// </summary>
    public interface IGameContextAccessor
    {
        /// <summary>
        /// Gets the active context in this accessor's logical execution flow.
        /// </summary>
        GameContext? GameContext { get; }

        GameContextScope Enter(GameContext context);
    }
}
