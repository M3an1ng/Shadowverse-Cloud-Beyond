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
        /// Gets or sets the current GameContext.
        /// </summary>
        GameContext? GameContext { get; set; }
    }
}