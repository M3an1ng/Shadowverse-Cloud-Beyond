import { cardValue, cardId } from "./cardCatalogModel.js";

// Mirrors Mini's deckSummary: count copies in statistics, merge copies only in preview.
export function summarizeDraftDeck(ids, byId) {
  const curve = Array(9).fill(0),
    counts = { follower: 0, spell: 0, amulet: 0, missing: 0 },
    copies = new Map();
  for (const id of ids) {
    const card = byId.get(id);
    copies.set(id, (copies.get(id) || 0) + 1);
    if (!card) {
      counts.missing++;
      continue;
    }
    curve[Math.min(8, Math.max(0, Number(cardValue(card, "cost", 0)) || 0))]++;
    const type = String(cardValue(card, "type")).toLowerCase();
    counts[type] = (counts[type] || 0) + 1;
  }
  const entries = [...copies]
    .map(([id, count]) => ({ id, card: byId.get(id), count }))
    .sort(
      (a, b) =>
        Number(cardValue(a.card, "cost", 0)) -
        Number(cardValue(b.card, "cost", 0)),
    );
  return { curve, counts, entries, total: ids.length };
}
