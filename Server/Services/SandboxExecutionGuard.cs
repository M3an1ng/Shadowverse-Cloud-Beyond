using System;
using System.Threading;

namespace ShadowverseBeyond.Server.Services
{
    public static class SandboxExecutionGuard
    {
        private static readonly AsyncLocal<int> Depth = new();

        public static bool IsSuspended => Depth.Value > 0;

        public static IDisposable Enter()
        {
            Depth.Value = Depth.Value + 1;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Depth.Value = Math.Max(0, Depth.Value - 1);
                }
            }
        }
    }
}
