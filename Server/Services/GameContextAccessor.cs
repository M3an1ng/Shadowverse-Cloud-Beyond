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
        private readonly AsyncLocal<GameContextScope?> _current = new();

        public GameContext? GameContext
        {
            get
            {
                for (var scope = _current.Value; scope != null; scope = scope.Previous)
                {
                    if (scope.Context is { } context)
                    {
                        return context;
                    }
                }
                return null;
            }
        }

        public GameContextScope Enter(GameContext context)
        {
            var scope = new GameContextScope(this, context, _current.Value);
            _current.Value = scope;
            return scope;
        }

        internal void Release(GameContextScope scope)
        {
            if (ReferenceEquals(_current.Value, scope))
            {
                _current.Value = scope.Previous;
            }
        }
    }
}
