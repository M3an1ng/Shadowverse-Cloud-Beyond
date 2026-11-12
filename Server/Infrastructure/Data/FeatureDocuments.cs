using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace ShadowverseBeyond.Server.Infrastructure.Data;
public sealed class FeatureDocument { internal static readonly SemaphoreSlim WriteLock = new(1, 1); public string Id { get; set; } = ""; public string Json { get; set; } = "{}"; public int Revision { get; set; } public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; }
public partial class ApplicationDbContext { public DbSet<FeatureDocument> FeatureDocuments => Set<FeatureDocument>(); }
public sealed record CardAppearance(string Name, string Description, string? ArtPreview, string? ArtDetail);
public sealed record StyleBinding(int CardId, CardAppearance Appearance);
public sealed record CardStyle(string Id, string Label, CardAppearance Appearance, List<StyleBinding> Bindings);
public sealed record StyleConfiguration(int CardId, int Revision, List<CardStyle> Styles);
public sealed record CardSetLogo(int CardSetId, string LogoUrl);
public sealed record MseCardMetadata(int CardId, bool IsImplemented, JsonElement? Presentation);
public static class CardStyleCatalog
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<int, StyleConfiguration> definitions = new();
    private static readonly ConcurrentDictionary<int, string> cardSetLogos = new();
    private static readonly ConcurrentDictionary<int, MseCardMetadata> mseCards = new();
    private static readonly ConcurrentDictionary<Guid, Dictionary<int, string>> selections = new();
    private static readonly ConcurrentDictionary<Guid, (long Revision, Dictionary<int, CardAppearance> Faces)> appearances = new();
    private static long catalogRevision;
    public static IEnumerable<StyleConfiguration> All => definitions.Values;
    public static async Task Load(ApplicationDbContext db)
    {
        foreach (var doc in await db.FeatureDocuments.AsNoTracking().Where(d => d.Id.StartsWith("style:") || d.Id.StartsWith("style-prefs:") || d.Id.StartsWith("cardset-logo:") || d.Id.StartsWith("mse-card:")).ToListAsync()) Apply(doc);
    }
    public static void Apply(FeatureDocument doc)
    {
        if (doc.Id.StartsWith("style:")) { var config = JsonSerializer.Deserialize<StyleConfiguration>(doc.Json, JsonOptions)!; definitions[config.CardId] = config; }
        else if (doc.Id.StartsWith("style-prefs:") && Guid.TryParse(doc.Id[12..], out var id)) selections[id] = JsonSerializer.Deserialize<Dictionary<int, string>>(doc.Json, JsonOptions) ?? new();
        else if (doc.Id.StartsWith("cardset-logo:")) { var logo = JsonSerializer.Deserialize<CardSetLogo>(doc.Json, JsonOptions)!; cardSetLogos[logo.CardSetId] = logo.LogoUrl; }
        else if (doc.Id.StartsWith("mse-card:")) { var meta = JsonSerializer.Deserialize<MseCardMetadata>(doc.Json, JsonOptions)!; mseCards[meta.CardId] = meta; }
        Interlocked.Increment(ref catalogRevision);
    }
    public static StyleConfiguration Get(int id) => definitions.GetValueOrDefault(id) ?? new(id, 0, new());
    public static string? GetCardSetLogo(int setId) => cardSetLogos.GetValueOrDefault(setId);
    public static MseCardMetadata? GetMseCardMeta(int cardId) => mseCards.GetValueOrDefault(cardId);
    public static bool IsCardImplemented(int cardId) => GetMseCardMeta(cardId)?.IsImplemented ?? true;
    public static void RemoveMseCardMeta(int cardId) => mseCards.TryRemove(cardId, out _);
    public static int? Parent(int id) => definitions.Values.FirstOrDefault(c => c.Styles.Any(s => s.Bindings.Any(b => b.CardId == id)))?.CardId;
    public static Dictionary<int, string> Preferences(Guid id) => new(selections.GetValueOrDefault(id) ?? new());
    public static CardAppearance? Resolve(Guid owner, int cardId)
    {
        var revision = Interlocked.Read(ref catalogRevision);
        if (appearances.TryGetValue(owner, out var cached) && cached.Revision == revision) return cached.Faces.GetValueOrDefault(cardId);
        var faces = new Dictionary<int, CardAppearance>();
        if (selections.TryGetValue(owner, out var prefs))
        {
            var bound = definitions.Values.SelectMany(c => c.Styles).SelectMany(s => s.Bindings).Select(b => b.CardId).ToHashSet();
            foreach (var choice in prefs) { if (bound.Contains(choice.Key)) continue; var style = Get(choice.Key).Styles.FirstOrDefault(s => s.Id == choice.Value); if (style == null) continue; faces[choice.Key] = style.Appearance; foreach (var binding in style.Bindings) faces[binding.CardId] = binding.Appearance; }
        }
        appearances[owner] = (revision, faces); return faces.GetValueOrDefault(cardId);
    }
}
