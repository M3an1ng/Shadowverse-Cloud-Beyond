export const cardValue = (card, key, fallback = "") =>
  card?.[key] ?? card?.[key[0].toUpperCase() + key.slice(1)] ?? fallback;
export const cardId = (card) => Number(cardValue(card, "id", card?.CardId));
export const classes = [
  ["Neutral", "中立", "neutral", "#aaa"],
  ["Forestcraft", "精灵", "elf", "#79b887"],
  ["Swordcraft", "皇家护卫", "royal", "#d0bd68"],
  ["Runecraft", "巫师", "witch", "#898fdb"],
  ["Dragoncraft", "龙族", "dragon", "#b88755"],
  ["Abysscraft", "梦魔", "nightmare", "#cd6489"],
  ["Havencraft", "主教", "bishop", "#c9c3a0"],
  ["Portalcraft", "复仇者", "nemesis", "#69b7bf"],
];
