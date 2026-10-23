using System.Security.Cryptography;
using System.Text;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    internal static class AbilityDefinitionIdResolver
    {
        public static long CreateInlineDefinitionId(
            long parentDefinitionId,
            string path,
            Guid? parentRuntimeId = null)
        {
            var parentKey = parentDefinitionId != 0
                ? parentDefinitionId.ToString(global::System.Globalization.CultureInfo.InvariantCulture)
                : parentRuntimeId.HasValue && parentRuntimeId.Value != Guid.Empty
                    ? parentRuntimeId.Value.ToString("N")
                    : "unknown";
            var normalizedPath = string.IsNullOrWhiteSpace(path)
                ? "inline/unknown"
                : path.Trim().Trim('/');
            var canonical = $"inline-ability:{parentKey}:{normalizedPath}";
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical), hash);
            var value = global::System.BitConverter.ToInt64(hash[..8]);
            value &= long.MaxValue;
            if (value == 0)
            {
                value = 1;
            }

            return -value;
        }
    }
}
