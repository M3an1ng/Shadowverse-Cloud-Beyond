using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Infrastructure.Data;

[Index(nameof(CardId), nameof(Revision), IsUnique = true)]
public sealed class CardRevisionRecord
{
    public long Id { get; set; }
    public int CardId { get; set; }
    public int Revision { get; set; }
    public string Editor { get; set; } = "";
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = "";
    public string Operation { get; set; } = "edit";
    public string Snapshot { get; set; } = "";
}
public sealed record CardConfigurationSnapshot(CardDefinition Card, List<AbilityDefinition> Abilities);

public partial class ApplicationDbContext
{
    public DbSet<CardRevisionRecord> CardRevisions => Set<CardRevisionRecord>();
    public string? RevisionOperation { get; set; }
    private bool savingHistory;
    internal static readonly JsonSerializerOptions RevisionJson = new() { ReferenceHandler = ReferenceHandler.IgnoreCycles };

    public async Task<CardConfigurationSnapshot?> ReadCardConfigurationAsync(int id, CancellationToken ct = default)
        => (await ReadCardConfigurationsAsync(new[] { id }, ct)).GetValueOrDefault(id);

    private async Task<Dictionary<int, CardConfigurationSnapshot>> ReadCardConfigurationsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var cards = await CardDefinitions.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        var abilities = await CardAbilities.AsNoTracking().Where(a => ids.Contains(a.CardId)).OrderBy(a => a.Id).ToListAsync(ct);
        foreach (var card in cards) card.CardSetDefinition = null;
        foreach (var ability in abilities) ability.CardDefinition = null;
        var byCard = abilities.ToLookup(a => a.CardId);
        return cards.ToDictionary(c => c.Id, c => new CardConfigurationSnapshot(c, byCard[c.Id].ToList()));
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => SaveChangesAsync(true, ct);
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        if (savingHistory) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        ChangeTracker.DetectChanges();
        var cardEntries = ChangeTracker.Entries<CardDefinition>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        var abilityEntries = ChangeTracker.Entries<AbilityDefinition>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (cardEntries.Count == 0 && abilityEntries.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        var ids = cardEntries.Select(e => e.Entity.Id).Concat(abilityEntries.Select(e => e.Entity.CardId))
            .Concat(abilityEntries.Where(e => e.State != EntityState.Added).Select(e => e.OriginalValues.GetValue<int>(nameof(AbilityDefinition.CardId))))
            .Where(id => id > 0).Distinct().ToList();
        var ownedTransaction = Database.CurrentTransaction == null ? await Database.BeginTransactionAsync(ct) : null;
        savingHistory = true;
        try
        {
            var before = await ReadCardConfigurationsAsync(ids, ct);
            var count = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            var savedEntries = ChangeTracker.Entries().Where(e => !acceptAllChangesOnSuccess && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => (Entry: e, e.State, Originals: e.OriginalValues.Clone(), Modified: e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToHashSet())).ToList();
            ids = ids.Concat(cardEntries.Select(e => e.Entity.Id)).Where(id => id > 0).Distinct().ToList();
            var currentCards = await ReadCardConfigurationsAsync(ids, ct);
            var latestRevisions = await CardRevisions.Where(h => ids.Contains(h.CardId)
                && h.Revision == CardRevisions.Where(r => r.CardId == h.CardId).Max(r => r.Revision)).ToDictionaryAsync(h => h.CardId, ct);
            foreach (var id in ids)
            {
                var latest = latestRevisions.GetValueOrDefault(id);
                var previousRevision = latest?.Revision ?? 0;
                if (previousRevision == 0 && before.GetValueOrDefault(id) is { } initial)
                {
                    CardRevisions.Add(new() { CardId = id, Revision = ++previousRevision, Editor = initial.Card.UpdatedBy ?? "", ModifiedAt = initial.Card.UpdatedAt ?? DateTime.UtcNow, Operation = "baseline", Snapshot = JsonSerializer.Serialize(initial, RevisionJson) });
                }
                var current = currentCards.GetValueOrDefault(id);
                if (current == null) continue;
                var editor = current.Card.UpdatedBy ?? "";
                var now = DateTime.UtcNow;
                var operation = RevisionOperation ?? (before.GetValueOrDefault(id) == null ? "create" : "edit");
                // Keep baseline/import/rollback boundaries, and never infer that unknown editors are the same person.
                if (operation == "edit" && latest != null && latest.Operation is "create" or "edit" &&
                    !string.IsNullOrWhiteSpace(editor) && latest.Editor == editor &&
                    now >= latest.ModifiedAt && now - latest.ModifiedAt <= TimeSpan.FromMinutes(5))
                {
                    // Coalescing keeps one history entry, but every save must invalidate stale restore requests.
                    latest.Revision = previousRevision + 1;
                    latest.Snapshot = JsonSerializer.Serialize(current, RevisionJson);
                    latest.ModifiedAt = now;
                }
                else
                    CardRevisions.Add(new() { CardId = id, Revision = previousRevision + 1, Editor = editor, ModifiedAt = now, Operation = operation, Snapshot = JsonSerializer.Serialize(current, RevisionJson) });
            }
            // Save(false) leaves the first batch pending; only history may be written a second time.
            foreach (var saved in savedEntries) saved.Entry.State = EntityState.Unchanged;
            try { await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct); }
            finally
            {
                foreach (var saved in savedEntries)
                {
                    saved.Entry.State = saved.State;
                    saved.Entry.OriginalValues.SetValues(saved.Originals);
                    if (saved.State == EntityState.Modified)
                        foreach (var property in saved.Entry.Properties) property.IsModified = saved.Modified.Contains(property.Metadata.Name);
                }
            }
            if (ownedTransaction != null) await ownedTransaction.CommitAsync(ct);
            return count;
        }
        finally { savingHistory = false; RevisionOperation = null; if (ownedTransaction != null) await ownedTransaction.DisposeAsync(); }
    }
}
