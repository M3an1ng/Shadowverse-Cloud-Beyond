import { compareCardSetDisplayOrder } from "./cardSetDisplayOrder.js";
import { cardValue, cardId, classes } from "./cardCatalogModel.js";
export const defaultCatalogFilter = () => ({
  query: "",
  classes: [],
  sets: [],
  environment: "rotation",
  costs: [],
  types: [],
  rarities: [],
  attackMin: "",
  attackMax: "",
  healthMin: "",
  healthMax: "",
  tribe: "",
  keyword: "",
  includeAttached: false,
  hasStyles: false,
});
export function matchCatalogCard(
  card,
  f,
  styleIds = new Set(),
  keywordLabels = new Map(),
) {
  const get = (k) => cardValue(card, k);
  const type = String(get("type")).toLowerCase();
  if (!["follower", "spell", "amulet"].includes(type)) return false;
  if (!f.includeAttached && get("isCollectible") === false) return false;
  const env = card.card_set_environment || card.cardSetEnvironment || [];
  if (
    f.environment !== "open" &&
    !env.some((x) => normalizeCatalogEnvironment(x) === f.environment)
  )
    return false;
  if (
    f.classes.length &&
    !f.classes.some(
      (x) => x.toLowerCase() === String(get("class")).toLowerCase(),
    )
  )
    return false;
  if (f.sets.length && !f.sets.includes(card.card_set || get("cardSet")))
    return false;
  if (
    f.costs.length &&
    !f.costs.some((x) =>
      Number(x) === 10
        ? Number(get("cost")) >= 10
        : Number(x) === Number(get("cost")),
    )
  )
    return false;
  if (f.types.length && !f.types.includes(type)) return false;
  if (
    f.rarities.length &&
    !f.rarities.includes(String(get("rarity")).toLowerCase())
  )
    return false;
  const q = f.query.trim().toLowerCase();
  if (
    q &&
    !`${get("name")} ${cardId(card)} ${get("description")} ${card.catalogOriginalName || ""} ${card.catalogOriginalDescription || ""}`
      .toLowerCase()
      .includes(q)
  )
    return false;
  for (const stat of ["attack", "health"]) {
    if (
      f[stat + "Min"] !== "" &&
      (get(stat) == null || Number(get(stat)) < Number(f[stat + "Min"]))
    )
      return false;
    if (
      f[stat + "Max"] !== "" &&
      (get(stat) == null || Number(get(stat)) > Number(f[stat + "Max"]))
    )
      return false;
  }
  if (
    f.tribe &&
    !String(get("tribe")).toLowerCase().includes(f.tribe.toLowerCase())
  )
    return false;
  if (
    f.keyword &&
    !`${get("keywords")} ${(get("keywords") || []).map((x) => keywordLabels.get(String(x).toLowerCase()) || "").join(" ")} ${get("description")}`
      .toLowerCase()
      .includes(f.keyword.toLowerCase())
  )
    return false;
  return !f.hasStyles || styleIds.has(cardId(card));
}
export function compareCatalogCards(a, b) {
  const c = Number(cardValue(a, "cost", 0)) - Number(cardValue(b, "cost", 0));
  if (c) return c;
  const order = (x) =>
    classes.findIndex(
      ([id]) =>
        id.toLowerCase() === String(cardValue(x, "class")).toLowerCase(),
    );
  return (
    order(a) - order(b) ||
    String(a.card_set || cardValue(a, "cardSet")).localeCompare(
      String(b.card_set || cardValue(b, "cardSet")),
      "zh-CN",
    ) ||
    cardId(a) - cardId(b)
  );
}

export function normalizeCatalogEnvironment(value) {
  return (
    { 0: "rotation", 1: "unlimited", 2: "open" }[value] ||
    String(value).toLowerCase()
  );
}
export function catalogPackOptions(cards) {
  const packs = new Map();
  for (const card of cards) {
    const name = card.card_set || cardValue(card, "cardSet");
    if (!name) continue;
    const environments =
      card.card_set_environment || card.cardSetEnvironment || [];
    const rank = Math.min(
      2,
      ...environments
        .map((value) =>
          ["rotation", "unlimited", "open"].indexOf(
            normalizeCatalogEnvironment(value),
          ),
        )
        .filter((value) => value >= 0),
    );
    const existing = packs.get(name);
    if (!existing)
      packs.set(name, {
        name,
        card_set_code: card.card_set_code || card.cardSetCode,
        rank,
      });
    else existing.rank = Math.min(existing.rank, rank);
  }
  return [...packs.values()]
    .sort((a, b) => a.rank - b.rank || compareCardSetDisplayOrder(a, b))
    .map((pack) => pack.name);
}
