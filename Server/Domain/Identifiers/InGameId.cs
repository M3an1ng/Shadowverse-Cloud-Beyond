using System;
using System.Globalization;

namespace ShadowverseBeyond.Server.Domain.Identifiers
{
    public readonly record struct InGameId(InGameIdKind Kind, int Value)
    {
        public override string ToString()
        {
            return ToCanonicalString();
        }

        public string ToCanonicalString()
        {
            var prefix = GetPrefix(Kind);
            return Kind switch
            {
                InGameIdKind.Player => $"{prefix}-{Value}",
                InGameIdKind.Card => $"{prefix}-{Value:D6}",
                InGameIdKind.Buff => $"{prefix}-{Value:D6}",
                InGameIdKind.Event => $"{prefix}-{Value:D6}",
                _ => $"{prefix}-{Value:D6}"
            };
        }

        public static bool TryParse(string? token, out InGameId id)
        {
            id = default;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var span = token.Trim().AsSpan();
            if (span.Length < 2) return false;

            // Accept "C-000001", "c000001", "C000001", "C-1", "EV-12", "CH-3"
            int prefixEnd = 0;
            while (prefixEnd < span.Length && char.IsLetter(span[prefixEnd]))
            {
                prefixEnd++;
            }

            if (prefixEnd == 0) return false;

            var prefixSpan = span[..prefixEnd];

            int index = prefixEnd;
            if (index < span.Length && span[index] == '-') index++;
            if (index >= span.Length) return false;

            if (!int.TryParse(span[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return false;
            if (value <= 0) return false;

            if (!TryFromPrefix(prefixSpan, out var kind)) return false;

            id = new InGameId(kind, value);
            return true;
        }

        public static bool TryParseCanonical(string? token, out InGameId id)
        {
            id = default;
            if (!TryParse(token, out var parsed)) return false;

            var trimmed = token!.Trim();
            var canonical = parsed.ToCanonicalString();
            if (!string.Equals(trimmed, canonical, StringComparison.Ordinal)) return false;

            id = parsed;
            return true;
        }

        internal static string GetPrefix(InGameIdKind kind)
        {
            return kind switch
            {
                InGameIdKind.Card => "C",
                InGameIdKind.Buff => "B",
                InGameIdKind.Ability => "A",
                InGameIdKind.Effect => "E",
                InGameIdKind.Player => "P",
                InGameIdKind.Choice => "CH",
                InGameIdKind.Event => "EV",
                _ => "X"
            };
        }

        private static bool TryFromPrefix(ReadOnlySpan<char> prefix, out InGameIdKind kind)
        {
            if (prefix.Equals("C".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Card;
                return true;
            }
            if (prefix.Equals("B".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Buff;
                return true;
            }
            if (prefix.Equals("A".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Ability;
                return true;
            }
            if (prefix.Equals("E".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Effect;
                return true;
            }
            if (prefix.Equals("P".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Player;
                return true;
            }
            if (prefix.Equals("EV".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Event;
                return true;
            }
            if (prefix.Equals("CH".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                kind = InGameIdKind.Choice;
                return true;
            }

            kind = default;
            return false;
        }
    }
}
