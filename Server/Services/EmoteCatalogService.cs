using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Dtos.Emotes;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public class EmoteCatalogService : IEmoteCatalogService
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".gif",
            ".avif"
        };

        private static readonly NaturalStringComparer NaturalComparer = new();

        private readonly IWebHostEnvironment _environment;
        private readonly EmoteCatalogOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EmoteCatalogService> _logger;
        private readonly object _cacheLock = new();

        private EmoteCatalogDto? _cachedCatalog;
        private Dictionary<string, EmoteItemDto>? _cachedItemsById;
        private DateTimeOffset _cacheExpiresAtUtc;

        public EmoteCatalogService(
            IWebHostEnvironment environment,
            IOptions<EmoteCatalogOptions> options,
            TimeProvider timeProvider,
            ILogger<EmoteCatalogService> logger)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _options = options?.Value ?? new EmoteCatalogOptions();
            _timeProvider = timeProvider ?? TimeProvider.System;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public EmoteCatalogDto GetCatalog()
        {
            lock (_cacheLock)
            {
                var now = _timeProvider.GetUtcNow();
                if (_cachedCatalog != null && now < _cacheExpiresAtUtc)
                {
                    return _cachedCatalog;
                }

                var catalog = ScanCatalog();
                _cachedCatalog = catalog;
                _cachedItemsById = catalog.Packs
                    .SelectMany(pack => pack.Items)
                    .ToDictionary(item => item.Id, item => item, StringComparer.Ordinal);
                _cacheExpiresAtUtc = now.Add(NormalizeTtl(_options.CacheTtl));
                return catalog;
            }
        }

        public bool TryResolveEmote(string? emoteId, out EmoteItemDto? emote)
        {
            emote = null;
            if (!IsSafeEmoteId(emoteId))
            {
                return false;
            }

            _ = GetCatalog();
            lock (_cacheLock)
            {
                return _cachedItemsById != null && _cachedItemsById.TryGetValue(emoteId!, out emote);
            }
        }

        private EmoteCatalogDto ScanCatalog()
        {
            var root = GetEmoteRootPath();
            if (!Directory.Exists(root))
            {
                return new EmoteCatalogDto();
            }

            var packs = new List<EmotePackDto>();
            foreach (var directoryPath in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, NaturalComparer))
            {
                var directory = new DirectoryInfo(directoryPath);
                if (IsHiddenOrSystem(directory))
                {
                    continue;
                }

                var packId = directory.Name;
                if (string.IsNullOrWhiteSpace(packId) || packId.Contains('/') || packId.Contains('\\'))
                {
                    continue;
                }

                var items = directory.EnumerateFiles()
                    .Where(file => IsSupportedEmoteFile(root, file))
                    .OrderBy(file => file.Name, NaturalComparer)
                    .Select(file => CreateItem(packId, file))
                    .Where(item => item != null)
                    .Select(item => item!)
                    .ToList();

                if (items.Count == 0)
                {
                    continue;
                }

                packs.Add(new EmotePackDto
                {
                    Id = packId,
                    Name = packId,
                    Items = items
                });
            }

            return new EmoteCatalogDto { Packs = packs };
        }

        private string GetEmoteRootPath()
        {
            var workspaceRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
            return Path.GetFullPath(Path.Combine(workspaceRoot, NormalizeRelativeRoot(_options.RelativeRoot)));
        }

        private static string NormalizeRelativeRoot(string? relativeRoot)
        {
            var value = string.IsNullOrWhiteSpace(relativeRoot)
                ? "assets/emotes"
                : relativeRoot.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return value;
        }

        private static TimeSpan NormalizeTtl(TimeSpan ttl)
        {
            return ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(60) : ttl;
        }

        private static bool IsSupportedEmoteFile(string root, FileInfo file)
        {
            if (IsHiddenOrSystem(file) || string.IsNullOrWhiteSpace(file.Name))
            {
                return false;
            }

            if (!SupportedExtensions.Contains(file.Extension))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(file.FullName);
            var rootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(root));
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHiddenOrSystem(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.Name.StartsWith(".", StringComparison.Ordinal)
                || fileSystemInfo.Attributes.HasFlag(FileAttributes.Hidden)
                || fileSystemInfo.Attributes.HasFlag(FileAttributes.System);
        }

        private static EmoteItemDto? CreateItem(string packId, FileInfo file)
        {
            var id = $"{packId}/{file.Name}";
            if (!IsSafeEmoteId(id))
            {
                return null;
            }

            var name = Path.GetFileNameWithoutExtension(file.Name);
            return new EmoteItemDto
            {
                Id = id,
                PackId = packId,
                Name = name,
                Url = $"/assets/emotes/{Uri.EscapeDataString(packId)}/{Uri.EscapeDataString(file.Name)}"
            };
        }

        private static bool IsSafeEmoteId(string? emoteId)
        {
            if (string.IsNullOrWhiteSpace(emoteId)
                || emoteId.Contains('\\')
                || emoteId.Contains("://", StringComparison.Ordinal)
                || emoteId.StartsWith("/", StringComparison.Ordinal)
                || emoteId.StartsWith("\\", StringComparison.Ordinal)
                || Path.IsPathRooted(emoteId))
            {
                return false;
            }

            var parts = emoteId.Split('/');
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            return parts.All(part => part != "." && part != "..")
                && SupportedExtensions.Contains(Path.GetExtension(parts[1]));
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private sealed class NaturalStringComparer : IComparer<string?>
        {
            public int Compare(string? x, string? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                var ix = 0;
                var iy = 0;
                while (ix < x.Length && iy < y.Length)
                {
                    var cx = x[ix];
                    var cy = y[iy];
                    if (char.IsDigit(cx) && char.IsDigit(cy))
                    {
                        var nx = ReadNumber(x, ref ix);
                        var ny = ReadNumber(y, ref iy);
                        var numberCompare = nx.CompareTo(ny);
                        if (numberCompare != 0)
                        {
                            return numberCompare;
                        }
                        continue;
                    }

                    var charCompare = char.ToUpper(cx, CultureInfo.InvariantCulture)
                        .CompareTo(char.ToUpper(cy, CultureInfo.InvariantCulture));
                    if (charCompare != 0)
                    {
                        return charCompare;
                    }

                    ix++;
                    iy++;
                }

                return x.Length.CompareTo(y.Length);
            }

            private static long ReadNumber(string value, ref int index)
            {
                long number = 0;
                while (index < value.Length && char.IsDigit(value[index]))
                {
                    var digit = value[index] - '0';
                    if (number <= (long.MaxValue - digit) / 10)
                    {
                        number = number * 10 + digit;
                    }
                    index++;
                }
                return number;
            }
        }
    }
}
