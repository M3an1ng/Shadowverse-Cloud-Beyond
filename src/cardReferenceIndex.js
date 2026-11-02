// Reference names are aliases of a stable card ID, never replacement identities.
export function buildReferenceNameIndex(cards) {
  const cardsById = new Map(), idsByName = new Map();
  for (const card of cards || []) {
    const id = Number(card?.id ?? card?.CardId ?? card?.Id);
    if (!Number.isFinite(id)) continue;
    if (!cardsById.has(id)) cardsById.set(id, card);
    const original = card.styleOriginalCard;
    for (const value of [card.name, card.Name, card.catalogOriginalName, original?.name, original?.Name]) {
      const name = String(value ?? '').trim().toLowerCase();
      if (!name) continue;
      const ids = idsByName.get(name) || [];
      if (!ids.includes(id)) ids.push(id);
      idsByName.set(name, ids);
    }
  }
  return { cardsById, idsByName };
}
