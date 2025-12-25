using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Services.Abstractions;
using System.Threading;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Default implementation of IGameContextAccessor.
    /// It uses an AsyncLocal field to store the context, making it safe
    /// for async operations within a single logical flow of control.
    /// </summary>
    public class GameContextAccessor : IGameContextAccessor
    {
        private static readonly AsyncLocal<GameContextHolder> _gameContextCurrent = new();

        public GameContext? GameContext
        {
            get => _gameContextCurrent.Value?.Context;
            set
            {
                var holder = _gameContextCurrent.Value;
                if (holder != null)
                {
                    // Do not null out an existing holder.
                    holder.Context = null;
                }

                if (value != null)
                {
                    // Use an object indirection to hold the GameContext in the AsyncLocal,
                    // so it can be cleared out without affecting other scopes.
                    _gameContextCurrent.Value = new GameContextHolder { Context = value };
                }
            }
        }

        private class GameContextHolder
        {
            public GameContext? Context;
        }
    }
}