using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services;

public sealed class GameContextScope : IDisposable
{
    private readonly GameContextAccessor _accessor;
    private GameContext? _context;
    internal GameContextScope? Previous { get; }
    internal GameContext? Context => Volatile.Read(ref _context);

    internal GameContextScope(GameContextAccessor accessor, GameContext context, GameContextScope? previous)
    {
        _accessor = accessor;
        _context = context;
        Previous = previous;
    }

    public void Dispose()
    {
        // 撤销共享的作用域引用，使已捕获 ExecutionContext 的后台流也看不到已结束的请求。
        Interlocked.Exchange(ref _context, null);
        _accessor.Release(this);
    }
}
