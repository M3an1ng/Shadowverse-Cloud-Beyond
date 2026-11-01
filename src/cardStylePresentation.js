import { cardId, cardValue } from "./cardCatalogModel.js";
export function createCardStylePresentation(configs, preferences) {
  const appearances = new Map();
  const bound = new Set(configs.flatMap(c => (c.styles || []).flatMap(s => (s.bindings || []).map(b => Number(b.cardId)))));
  for (const config of configs) {
    if (bound.has(Number(config.cardId))) continue;
    const style = config.styles?.find(
      (item) => item.id === preferences[config.cardId],
    );
    if (!style) continue;
    appearances.set(Number(config.cardId), style.appearance);
    for (const binding of style.bindings || [])
      appearances.set(Number(binding.cardId), binding.appearance);
  }
  return (card) => {
    if (!card) return card;
    card = card.styleOriginalCard || card;
    const appearance = appearances.get(cardId(card));
    if (!appearance) return card;
    const styled = {
      ...card,
      name: appearance.name || cardValue(card, "name"),
      description: appearance.description ?? cardValue(card, "description"),
      artPreview: appearance.artPreview || cardValue(card, "artPreview"),
      artDetail:
        appearance.artDetail ||
        appearance.artPreview ||
        cardValue(card, "artDetail"),
      catalogOriginalName: cardValue(card, "name"),
      catalogOriginalDescription: cardValue(card, "description"),
      styleOriginalCard: card,
    };
    for (const key of ['name', 'description', 'artPreview', 'artDetail']) {
      const upper = key[0].toUpperCase() + key.slice(1);
      if (upper in card) styled[upper] = styled[key];
    }
    return styled;
  };
}
