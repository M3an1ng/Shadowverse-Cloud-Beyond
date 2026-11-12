using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed class RoomJoinCodeRegistry : IRoomJoinCodeRegistry
    {
        public const string DefaultAlphanumericAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const string DigitsAlphabet = "0123456789";

        private readonly ConcurrentDictionary<string, ResolvedJoinCodeTarget> _entries =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new();

        public bool TryRegister(string joinCode, RegisteredRoomKind kind, string roomId)
        {
            var normalized = NormalizeCode(joinCode);
            if (string.IsNullOrEmpty(normalized) || string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            lock (_gate)
            {
                if (_entries.TryGetValue(normalized, out var existing))
                {
                    return existing.Kind == kind
                           && string.Equals(existing.RoomId, roomId.Trim(), StringComparison.OrdinalIgnoreCase);
                }

                _entries[normalized] = new ResolvedJoinCodeTarget(kind, roomId.Trim(), normalized);
                return true;
            }
        }

        public string AllocateUniqueCode(
            RegisteredRoomKind kind,
            string roomId,
            string alphabet,
            int length = 6)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("roomId is required.", nameof(roomId));
            }

            if (string.IsNullOrEmpty(alphabet))
            {
                alphabet = DefaultAlphanumericAlphabet;
            }

            var codeLength = Math.Clamp(length, 4, 12);
            lock (_gate)
            {
                for (var attempt = 0; attempt < 256; attempt++)
                {
                    var candidate = GenerateRandomCode(alphabet, codeLength);
                    // For TakeTwo numeric codes, keep leading digit >= 1 to match 100000..999999 expectation
                    if (ReferenceEquals(alphabet, DigitsAlphabet) || alphabet == DigitsAlphabet)
                    {
                        if (candidate[0] == '0')
                        {
                            candidate = string.Concat(((char)('1' + RandomNumberGenerator.GetInt32(0, 9))).ToString(), candidate.AsSpan(1));
                        }
                    }

                    if (!_entries.ContainsKey(candidate))
                    {
                        _entries[candidate] = new ResolvedJoinCodeTarget(kind, roomId.Trim(), candidate);
                        return candidate;
                    }
                }

                // Fallback with guaranteed retry loop
                while (true)
                {
                    var fallback = GenerateRandomCode(alphabet, codeLength);
                    if (!_entries.ContainsKey(fallback))
                    {
                        _entries[fallback] = new ResolvedJoinCodeTarget(kind, roomId.Trim(), fallback);
                        return fallback;
                    }
                }
            }
        }

        public bool TryResolve(string? joinCode, out ResolvedJoinCodeTarget target)
        {
            var normalized = NormalizeCode(joinCode);
            if (string.IsNullOrEmpty(normalized))
            {
                target = default;
                return false;
            }

            return _entries.TryGetValue(normalized, out target);
        }

        public void Release(string? joinCode, string? expectedRoomId = null)
        {
            var normalized = NormalizeCode(joinCode);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            lock (_gate)
            {
                if (!_entries.TryGetValue(normalized, out var existing))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(expectedRoomId) &&
                    !string.Equals(existing.RoomId, expectedRoomId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _entries.TryRemove(normalized, out _);
            }
        }

        private static string NormalizeCode(string? raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();
        }

        private static string GenerateRandomCode(string alphabet, int length)
        {
            Span<byte> buffer = stackalloc byte[length];
            RandomNumberGenerator.Fill(buffer);
            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[buffer[i] % alphabet.Length];
            }

            return new string(chars);
        }
    }
}
