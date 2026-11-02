import { bindCardStyle } from './cardStyleDom.js';
import { cardValue, classes, createFeatureModal } from "./cardStyles.js";
import { miniCardBadge } from "./miniCardBadge.js";
import { showCardDetailModal } from "./components/CardDetailMockup.js";
import { summarizeDraftDeck } from "./draftDeckModel.js";
import "./styles/mini-draft-presentation.css";

export const element = (tag, className = "", text = null) => {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== null) node.textContent = text;
  return node;
};
export function professionEmblem(id) {
  const icon = element("img", "take-two-emblem");
  const key = {
    Forestcraft: "forestcraft",
    Swordcraft: "swordcraft",
    Runecraft: "runecraft",
    Dragoncraft: "dragoncraft",
    Abysscraft: "abysscraft",
    Havencraft: "havencraft",
    Portalcraft: "portalcraft",
  }[id];
  icon.src = `/assets/reference-icons/class-${key}.png`;
  icon.alt = "";
  icon.setAttribute("aria-hidden", "true");
  return icon;
}
const rarityName = (card) =>
  ({
    bronze: "青铜",
    silver: "白银",
    gold: "黄金",
    legend: "传说",
    legendary: "传说",
  })[String(cardValue(card, "rarity")).toLowerCase()] || "白银";
export function draftCard(card, decorative = false) {
  const root = element(decorative ? "span" : "button", "take-two-card");
  if (!decorative) {
    root.type = "button";
    root.setAttribute("aria-label", `查看 ${cardValue(card, "name")}`);
    root.onclick = () => showCardDetailModal(card);
  }
  const frame = element("span", "pool-card take-two-card-frame");
  frame.dataset.rarity = rarityName(card);
  const art = element("span", "take-two-card-art"),
    img = element("img");
  img.src = cardValue(card, "artPreview");
  img.alt = cardValue(card, "name");
  img.decoding = "async";
  img.loading = decorative ? "lazy" : "eager";
  img.fetchPriority = decorative ? "low" : "high";
  art.append(img);
  frame.append(art, miniCardBadge("cost", cardValue(card, "cost", 0)));
  if (String(cardValue(card, "type")).toLowerCase() === "follower")
    frame.append(
      miniCardBadge("attack", cardValue(card, "attack", 0)),
      miniCardBadge("health", cardValue(card, "health", 0)),
    );
  root.append(
    frame,
    element("strong", "take-two-card-name", cardValue(card, "name")),
  );
  bindCardStyle(root, present => {
    const styled = present(card);
    img.src = cardValue(styled, 'artPreview');
    img.alt = cardValue(styled, 'name');
    root.querySelector('.take-two-card-name').textContent = cardValue(styled, 'name');
    if (!decorative) {
      root.setAttribute('aria-label', `查看 ${cardValue(styled, 'name')}`);
      root.onclick = () => showCardDetailModal(styled, { stylePreferenceApplied: true, styleOriginalCard: styled.styleOriginalCard || card });
    }
  });
  return root;
}
export function draftCurve(ids, byId, { preview = false } = {}) {
  const { curve, counts, total } = summarizeDraftDeck(ids, byId),
    max = Math.max(1, ...curve);
  const summary = element("div", "deck-summary"),
    chart = element("div", "deck-curve");
  chart.setAttribute("aria-label", "费用曲线");
  curve.forEach((n, cost) => {
    const column = element("div");
    column.title = `${cost === 8 ? "8+" : cost} 费：${n} 张`;
    const bar = element("i");
    if (preview) {
      const area = element("div", "preview-curve-bar-area");
      bar.style.height = `${(n / max) * 100}%`;
      area.append(bar);
      column.append(area);
    } else {
      bar.style.height = `${Math.max(2, (n / max) * 48)}px`;
      column.append(element("b", "", n || ""), bar);
    }
    column.append(element("small", "", cost === 8 ? "8+" : cost));
    chart.append(column);
  });
  const count = element("div", "deck-summary-count"),
    types = element("small");
  for (const [type, label] of [
    ["follower", "从者"],
    ["spell", "法术"],
    ["amulet", "护符"],
  ]) {
    const span = element("span", "", `${label} ${counts[type]}`);
    span.dataset.cardType = type;
    types.append(span);
  }
  if (counts.missing)
    types.append(element("span", "", `缺失 ${counts.missing}`));
  count.append(element("strong", "", `${total} / 30`), types);
  summary.append(chart, count);
  return summary;
}
export function showDraftDeckPreview(
  ids,
  byId,
  { profession, environment } = {},
) {
  const modal = createFeatureModal("卡组预览", { wide: true });
  modal.overlay.classList.add("reference-deck-preview", "draft-deck-preview");
  modal.panel.classList.add("mini-draft-preview");
  const heading = element("div", "reference-deck-preview-heading"),
    titles = element("div");
  titles.append(
    element("span", "preview-caption", "卡组预览"),
    element("h2", "", "我的双选牌组"),
    element(
      "p",
      "",
      `${classes.find((c) => c[0] === profession)?.[1] || "中立"} | 双选 | ${{ rotation: "指定", unlimited: "无限", open: "开放", as2p: "AS2P" }[environment] || "开放"}`,
    ),
  );
  const actions = element("div", "deck-preview-actions"),
    close = element("button", "", "关闭");
  close.type = "button";
  close.onclick = modal.close;
  actions.append(close);
  heading.append(titles, actions);
  const grid = element("div", "deck-preview-grid");
  grid.style.gridTemplateRows = "repeat(2,max-content)";
  for (const { card, count } of summarizeDraftDeck(ids, byId).entries) {
    const tile = element("button", "pool-card");
    tile.type = "button";
    tile.dataset.rarity = rarityName(card);
    tile.setAttribute(
      "aria-label",
      `${cardValue(card, "name", "缺失卡牌")} ×${count}`,
    );
    tile.onclick = () => card && showCardDetailModal(card);
    const art = element("div", "deck-card-art"),
      img = element("img");
    img.src = cardValue(card, "artPreview", "");
    img.alt = cardValue(card, "name", "缺失卡牌");
    img.loading = "lazy";
    img.decoding = "async";
    art.append(img);
    tile.append(
      element("span", "cost-orb", cardValue(card, "cost", "?")),
      art,
      element("strong", "", cardValue(card, "name", "缺失卡牌")),
      element(
        "small",
        "preview-card-type",
        { follower: "从者", spell: "法术", amulet: "护符" }[
          String(cardValue(card, "type")).toLowerCase()
        ] || "",
      ),
      element("b", "deck-copy-count", `×${count}`),
    );
    bindCardStyle(tile, present => {
      const styled = present(card);
      img.src = cardValue(styled, 'artPreview', '');
      img.alt = cardValue(styled, 'name');
      tile.querySelector('strong').textContent = cardValue(styled, 'name');
      tile.onclick = () => styled && showCardDetailModal(styled, { stylePreferenceApplied: true, styleOriginalCard: styled.styleOriginalCard || card });
    });
    grid.append(tile);
  }
  modal.body.append(heading, draftCurve(ids, byId, { preview: true }), grid);
  if (!ids.length) modal.body.append(element("p", "", "牌组还没有卡牌"));
  return modal;
}
