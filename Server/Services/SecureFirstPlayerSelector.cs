using System;
using System.Security.Cryptography;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Uses cryptographically secure randomness to avoid time-seed bias
    /// during first-player selection.
    /// </summary>
    public sealed class SecureFirstPlayerSelector : IFirstPlayerSelector
    {
        private readonly Func<int, int, int> _nextInt;

        public SecureFirstPlayerSelector()
            : this(RandomNumberGenerator.GetInt32)
        {
        }

        public SecureFirstPlayerSelector(Func<int, int, int> nextInt)
        {
            _nextInt = nextInt ?? throw new ArgumentNullException(nameof(nextInt));
        }

        public bool IsPlayer1First()
        {
            var roll = _nextInt(0, 2);
            return roll switch
            {
                0 => true,
                1 => false,
                _ => throw new InvalidOperationException($"First-player selector returned invalid value {roll}. Expected 0 or 1.")
            };
        }
    }
}
