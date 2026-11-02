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
    {
        var card = await CardDefinitions.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (card == null) return null;
        card.CardSetDefinition = null;
        var abilities = await CardAbilities.AsNoTracking().Where(a => a.CardId == id).OrderBy(a => a.Id).ToListAsync(ct);
        foreach (var ability in abilities) ability.CardDefinition = null;
        return new(card, abilities);
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
            var before = new Dictionary<int, CardConfigurationSnapshot?>();
            foreach (var id in ids) before[id] = await ReadCardConfigurationAsync(id, ct);
            var count = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            ids = ids.Concat(cardEntries.Select(e => e.Entity.Id)).Where(id => id > 0).Distinct().ToList();
            foreach (var id in ids)
            {
                var previousRevision = await CardRevisions.Where(h => h.CardId == id).Select(h => (int?)h.Revision).MaxAsync(ct) ?? 0;
                if (previousRevision == 0 && before.GetValueOrDefault(id) is { } initial)
                {
                    CardRevisions.Add(new() { CardId = id, Revision = ++previousRevision, Editor = initial.Card.UpdatedBy ?? "", ModifiedAt = initial.Card.UpdatedAt ?? DateTime.UtcNow, Operation = "baseline", Snapshot = JsonSerializer.Serialize(initial, RevisionJson) });
                }
                var current = await ReadCardConfigurationAsync(id, ct);
                if (current == null) continue;
                var editor = current.Card.UpdatedBy ?? "";
                var now = DateTime.UtcNow;
                var operation = RevisionOperation ?? (before.GetValueOrDefault(id) == null ? "create" : "edit");
                var latest = await CardRevisions.Where(h => h.CardId == id).OrderByDescending(h => h.Revision).FirstOrDefaultAsync(ct);
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
            await base.SaveChangesAsync(true, ct);
            if (ownedTransaction != null) await ownedTransaction.CommitAsync(ct);
            return count;
        }
        finally { savingHistory = false; RevisionOperation = null; if (ownedTransaction != null) await ownedTransaction.DisposeAsync(); }
    }
}
