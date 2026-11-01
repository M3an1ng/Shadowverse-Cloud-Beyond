import { cardId, cardValue } from "./cardCatalogModel.js";
export function createCardStylePresentation(configs, preferences) {
  const appearances = new Map();
  for (const config of configs) {
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
    const appearance = appearances.get(cardId(card));
    if (!appearance) return card;
    return {
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
  };
}
