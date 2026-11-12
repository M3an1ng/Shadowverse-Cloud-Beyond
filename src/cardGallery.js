import { createCardStylePresentation } from "./cardStylePresentation.js";
import { getUserSession } from "./userSession.js";
import { historyRequest } from "./cardHistory.js";
import { ensureEnumLocalizations } from "./enumLocalizationCache.js";
import { loadCardDefinitions } from "./cardDefinitionsLoader.js";
import {
  defaultCatalogFilter,
  matchCatalogCard,
  compareCatalogCards,
  catalogPackOptions,
} from "./cardCatalogQuery.js";
import {
  cardValue,
  cardId,
  cardFace,
  classButtons,
  createFeatureModal,
  loadStyles,
} from "./cardStyles.js";
import { showCardDetailModal } from "./components/CardDetailMockup.js";
import "./styles/card-gallery.css";
let catalogTask;
export function catalogCards() {
  return (catalogTask ??= loadCardDefinitions().catch((e) => {
    catalogTask = null;
    throw e;
  }));
}
document.addEventListener("svb:card-catalog-invalidated", () => {
  catalogTask = null;
});
const button = (text, action) => {
  const b = document.createElement("button");
  b.type = "button";
  b.textContent = text;
  b.onclick = action;
  return b;
};
export function mountCatalogSearch(
  host,
  {
    cards,
    styles = [],
    initial = defaultCatalogFilter(),
    onSearch,
    advanced = true,
  },
) {
  let draft = structuredClone(initial);
  let defaultPackScope = [...initial.sets];
  const resetDraft = () => ({
    ...defaultCatalogFilter(),
    environment: initial.environment,
    sets: [...defaultPackScope],
  });
  const keywordLabels = new Map();
  const suggestions = {
    tribe: [
      ...new Set(cards.flatMap((card) => cardValue(card, "tribe") || [])),
    ].map((value) => ({ value: String(value), search: String(value) })),
    keyword: [
      ...new Set(cards.flatMap((card) => cardValue(card, "keywords") || [])),
    ].map((value) => ({ value: String(value), search: String(value) })),
  };
  let metadataTask;
  const loadSuggestions = () =>
    (metadataTask ??= Promise.all([
      ensureEnumLocalizations(["CardKeyword", "AbilityKeyword"]),
      fetch("/api/tribes")
        .then((response) => (response.ok ? response.json() : []))
        .catch(() => []),
    ]).then(([enums, tribes]) => {
      for (const entry of enums)
        for (const [key, label] of Object.entries(entry.raw)) {
          if (key.toLowerCase() === "none" || label === "无") continue;
          keywordLabels.set(key.toLowerCase(), label);
          suggestions.keyword.push({ value: label, search: `${label} ${key}` });
        }
      for (const tribe of Array.isArray(tribes) ? tribes : []) {
        const name = tribe.name || tribe.Name;
        if (name)
          suggestions.tribe.push({
            value: name,
            search: `${name} ${(tribe.aliases || []).join(" ")}`,
          });
      }
      suggestions.keyword = suggestions.keyword.filter(
        (item) => !keywordLabels.has(item.value.toLowerCase()),
      );
      for (const key of ["tribe", "keyword"])
        suggestions[key] = [
          ...new Map(
            suggestions[key].map((item) => [item.value, item]),
          ).values(),
        ];
    }));
  const renderTop = (target, apply, rerender) => {
    const row = document.createElement("div");
    row.className = "gallery-search-row";
    const query = document.createElement("input");
    query.placeholder = "输入卡名、ID或关键词";
    query.value = draft.query;
    query.oninput = () => (draft.query = query.value);
    query.onkeydown = (e) => {
      if (e.key === "Enter") apply();
    };
    row.append(
      query,
      classButtons(draft.classes, (selection) => {
        draft.classes = selection;
        rerender();
      }),
      button("清除职业", () => {
        draft.classes = [];
        rerender();
      }),
      button("全部清除", () => {
        draft = resetDraft();
        rerender();
      }),
      button("搜索", apply),
    );
    target.append(row);
  };
  const apply = () => {
    const styleIds = new Set(
      styles.filter((c) => c.styles.length).map((c) => c.cardId),
    );
    onSearch(
      structuredClone(draft),
      cards
        .filter((c) => matchCatalogCard(c, draft, styleIds, keywordLabels))
        .sort(compareCatalogCards),
    );
  };
  const render = () => {
    host.replaceChildren();
    renderTop(host, apply, render);
    if (advanced) host.append(button("详细搜寻 ⌄", openAdvanced));
  };
  const openAdvanced = () => {
    const modal = createFeatureModal("详细搜寻", { wide: true });
    let packQuery = "",
      packPage = 1;
    const packSize = window.innerHeight < 800 ? 4 : 5;
    const renderAdvanced = () => {
      modal.body.replaceChildren();
      renderTop(
        modal.body,
        () => {
          apply();
          modal.close();
        },
        renderAdvanced,
      );
      const layout = document.createElement("div");
      layout.className = "gallery-advanced";
      const left = document.createElement("section"),
        right = document.createElement("section");
      const packTitle = document.createElement("h3");
      packTitle.textContent = "卡包";
      const search = document.createElement("input");
      search.placeholder = "搜索卡包";
      search.value = packQuery;
      const packResults = document.createElement("div");
      packResults.className = "gallery-pack-results";
      const packs = catalogPackOptions(cards);
      const drawPacks = () => {
        packResults.replaceChildren();
        const filtered = packs.filter((p) => p.includes(packQuery));
        packPage = Math.max(
          1,
          Math.min(packPage, Math.ceil(filtered.length / packSize) || 1),
        );
        for (const p of filtered.slice(
          (packPage - 1) * packSize,
          packPage * packSize,
        ))
          packResults.append(
            check(p, draft.sets.includes(p), (v) => {
              draft.sets = v
                ? [...draft.sets, p]
                : draft.sets.filter((x) => x !== p);
            }),
          );
        const pagination = document.createElement("div");
        pagination.className = "catalog-pagination";
        pagination.append(
          button("‹", () => {
            packPage--;
            drawPacks();
          }),
          document.createTextNode(
            `${packPage} / ${Math.ceil(filtered.length / packSize) || 1}`,
          ),
          button("›", () => {
            packPage++;
            drawPacks();
          }),
        );
        packResults.append(pagination);
      };
      search.oninput = () => {
        packQuery = search.value;
        packPage = 1;
        drawPacks();
      };
      drawPacks();
      left.append(packTitle, search, packResults);
      for (const [key, label] of [
        ["attack", "攻击力"],
        ["health", "生命值"],
      ]) {
        const row = document.createElement("label");
        row.className = "gallery-range";
        row.append(document.createTextNode(label));
        for (const suffix of ["Min", "Max"]) {
          const n = document.createElement("input");
          n.type = "number";
          n.min = "0";
          n.placeholder = "不限";
          n.value = draft[key + suffix];
          n.oninput = () => (draft[key + suffix] = n.value);
          row.append(n);
          if (suffix === "Min") row.append(document.createTextNode("～"));
        }
        left.append(row);
      }
      const environment = document.createElement("select");
      for (const [value, label] of [
        ["rotation", "指定"],
        ["unlimited", "无限"],
        ["open", "开放"],
      ]) {
        const o = document.createElement("option");
        o.value = value;
        o.textContent = label;
        environment.append(o);
      }
      environment.value = draft.environment;
      environment.onchange = () => (draft.environment = environment.value);
      right.append(filterRow("环境", environment));
      for (const [key, title, options] of [
        [
          "costs",
          "费用",
          Array.from({ length: 11 }, (_, i) => [
            i,
            i === 10 ? "10+" : String(i),
          ]),
        ],
        [
          "types",
          "种类",
          [
            ["follower", "随从"],
            ["spell", "法术"],
            ["amulet", "护符"],
          ],
        ],
        [
          "rarities",
          "稀有度",
          [
            ["bronze", "铜卡"],
            ["silver", "银卡"],
            ["gold", "金卡"],
            ["legend", "虹卡"],
          ],
        ],
      ]) {
        const content = document.createElement("div");
        content.className =
          "gallery-options" + (key === "costs" ? " cost-options" : "");
        for (const [value, label] of options) {
          if (key === "costs") {
            const cost = button("", () => {
              draft[key] = draft[key].includes(value)
                ? draft[key].filter((x) => x !== value)
                : [...draft[key], value];
              cost.classList.toggle("selected", draft[key].includes(value));
              cost.setAttribute("aria-pressed", draft[key].includes(value));
            });
            const image = document.createElement("img");
            image.src = `/assets/reference-cost/cost_${value}.png`;
            image.alt = "";
            cost.append(image);
            if (value === 10) {
              const plus = document.createElement("sup");
              plus.textContent = "+";
              cost.append(plus);
            }
            cost.setAttribute("aria-label", `费用 ${label}`);
            cost.setAttribute("aria-pressed", draft[key].includes(value));
            cost.classList.toggle("selected", draft[key].includes(value));
            content.append(cost);
          } else
            content.append(
              check(label, draft[key].includes(value), (v) => {
                draft[key] = v
                  ? [...draft[key], value]
                  : draft[key].filter((x) => x !== value);
              }),
            );
        }
        right.append(filterRow(title, content));
      }
      const bottom = document.createElement("div");
      bottom.className = "gallery-extra";
      const searches = document.createElement("div"),
        flags = document.createElement("div");
      searches.className = "gallery-filter-searches";
      flags.className = "gallery-filter-flags";
      for (const [key, label] of [
        ["tribe", "类型"],
        ["keyword", "能力关键词"],
      ]) {
        searches.append(
          filterRow(
            label,
            suggestionControl(
              label,
              draft[key],
              () => suggestions[key],
              (value) => (draft[key] = value),
              loadSuggestions(),
            ),
          ),
        );
      }
      flags.append(
        check(
          "包含附属卡牌",
          draft.includeAttached,
          (v) => (draft.includeAttached = v),
        ),
        check(
          "仅显示拥有卡牌风格的卡牌",
          draft.hasStyles,
          (v) => (draft.hasStyles = v),
        ),
      );
      bottom.append(searches, flags);
      right.append(bottom);
      layout.append(left, right);
      modal.body.append(layout);
    };
    void loadSuggestions();
    renderAdvanced();
  };
  render();
  return {
    apply,
    setPackScope: (sets, refresh = false) => {
      defaultPackScope = [...sets];
      draft.sets = [...sets];
      render();
      if (refresh) apply();
    },
    reset: () => {
      draft = resetDraft();
      render();
    },
  };
}
function check(label, checked, change) {
  const l = document.createElement("label");
  l.className = "catalog-check";
  const input = document.createElement("input");
  input.type = "checkbox";
  input.checked = checked;
  input.onchange = () => change(input.checked);
  const text = document.createElement("span");
  text.textContent = label;
  l.append(input, text);
  return l;
}
function filterRow(title, content) {
  const row = document.createElement("div");
  row.className = "gallery-filter-row";
  const label = document.createElement("span");
  label.textContent = title;
  row.append(label, content);
  return row;
}
export async function showCardGallery(container, onBack) {
  const page = document.createElement("section");
  page.className = "card-gallery";
  page.innerHTML =
    '<header><div><small>CARD GALLERY</small><h1>卡片一览</h1></div></header><div class="gallery-search"></div><div class="gallery-results-count"></div><div class="gallery-grid"></div><footer class="catalog-pagination"></footer>';
  container.replaceChildren(page);
  document.body.classList.add("card-gallery-active");
  const lifetime = new AbortController();
  const observer = new MutationObserver(() => {
    if (!page.isConnected) {
      lifetime.abort();
      observer.disconnect();
    }
  });
  observer.observe(container, { childList: true });
  const close = () => {
    lifetime.abort();
    observer.disconnect();
    document.body.classList.remove("card-gallery-active");
    onBack();
  };
  page.querySelector("header").append(button("« 返回牌组", close));
  try {
    const [originalCards, styles, preferences] = await Promise.all([
      catalogCards(),
      loadStyles(),
      getUserSession().userId
        ? historyRequest("/api/card-styles/preferences").catch(() => ({}))
        : Promise.resolve({}),
    ]);
    const cards = originalCards.map(
      createCardStylePresentation(styles, preferences),
    );
    let preservePage = false;
    if (!page.isConnected) return;
    let results = [],
      current = 1;
    const draw = () => {
      page.querySelector(".gallery-results-count").textContent =
        `共 ${results.length} 张卡片`;
      const grid = page.querySelector(".gallery-grid");
      grid.replaceChildren(
        ...results
          .slice((current - 1) * 12, current * 12)
          .map((card) =>
            cardFace(card, {
              onClick: () =>
                showCardDetailModal(card, {
                  styleOriginalCard: card.styleOriginalCard || card,
                  stylePreferenceApplied: true,
                }),
            }),
          ),
      );
      const pages = page.querySelector("footer");
      pages.replaceChildren();
      const total = Math.max(1, Math.ceil(results.length / 12));
      const prev = button("上一页", () => {
        current--;
        draw();
      });
      prev.disabled = current <= 1;
      const next = button("下一页", () => {
        current++;
        draw();
      });
      next.disabled = current >= total;
      pages.append(
        prev,
        document.createTextNode(`${current} / ${total}`),
        next,
      );
    };
    const search = mountCatalogSearch(page.querySelector(".gallery-search"), {
      cards,
      styles,
      onSearch: (_, filtered) => {
        results = filtered;
        current = preservePage
          ? Math.min(current, Math.max(1, Math.ceil(results.length / 12)))
          : 1;
        draw();
      },
    });
    search.apply();
    document.addEventListener(
      "svb:card-style-changed",
      async (event) => {
        if (!page.isConnected) return;
        if (!("styleId" in (event.detail || {}))) {
          styles.splice(0, styles.length, ...await loadStyles(true));
          if (!page.isConnected) return;
        } else if (event.detail.styleId)
          preferences[event.detail.cardId] = event.detail.styleId;
        else delete preferences[event.detail.cardId];
        cards.splice(
          0,
          cards.length,
          ...originalCards.map(
            createCardStylePresentation(styles, preferences),
          ),
        );
        preservePage = true;
        try {
          search.apply();
        } finally {
          preservePage = false;
        }
      },
      { signal: lifetime.signal },
    );
  } catch (e) {
    page.querySelector(".gallery-results-count").textContent = e.message;
  }
}

// A blank area need not take keyboard focus, so focusout alone cannot dismiss suggestions.
document.addEventListener("pointerdown", (event) => {
  for (const root of document.querySelectorAll(".gallery-suggest")) {
    if (root.contains(event.target)) continue;
    root.querySelector(".gallery-suggestions").hidden = true;
    root.querySelector("input").setAttribute("aria-expanded", "false");
  }
});
let suggestionSequence = 0;
function suggestionControl(label, value, items, onChange, ready) {
  const root = document.createElement("div"),
    input = document.createElement("input"),
    list = document.createElement("div");
  root.className = "gallery-suggest";
  list.className = "gallery-suggestions";
  list.id = `gallery-suggestions-${++suggestionSequence}`;
  list.setAttribute("role", "listbox");
  list.hidden = true;
  input.type = "search";
  input.placeholder = `搜索${label}`;
  input.value = value;
  input.setAttribute("aria-label", label);
  input.setAttribute("role", "combobox");
  input.setAttribute("aria-autocomplete", "list");
  input.setAttribute("aria-controls", list.id);
  input.setAttribute("aria-expanded", "false");
  const hide = () => {
    list.hidden = true;
    input.setAttribute("aria-expanded", "false");
  };
  const draw = () => {
    list.replaceChildren();
    const matches = items().filter((item) =>
      item.search.toLowerCase().includes(input.value.trim().toLowerCase()),
    );
    for (const item of matches) {
      const option = button(item.value, () => {
        input.value = item.value;
        onChange(item.value);
        hide();
        input.focus();
        hide();
      });
      option.setAttribute("role", "option");
      option.onmousedown = (e) => e.preventDefault();
      list.append(option);
    }
    list.hidden = !matches.length;
    input.setAttribute("aria-expanded", String(!!matches.length));
  };
  input.oninput = () => {
    onChange(input.value);
    draw();
  };
  input.onfocus = draw;
  input.onkeydown = (e) => {
    if (e.key === "Escape") {
      e.stopPropagation();
      hide();
    }
    if (e.key === "ArrowDown") {
      e.preventDefault();
      draw();
      list.firstElementChild?.focus();
    }
  };
  list.onkeydown = (e) => {
    const options = [...list.children],
      index = options.indexOf(document.activeElement);
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      options[
        (index + (e.key === "ArrowDown" ? 1 : -1) + options.length) %
          options.length
      ]?.focus();
    }
    if (e.key === "Escape") {
      e.stopPropagation();
      input.focus();
      hide();
    }
  };
  root.onfocusout = (e) => {
    if (!root.contains(e.relatedTarget)) hide();
  };
  void ready?.then(() => {
    if (input.isConnected && document.activeElement === input) draw();
  });
  root.append(input, list);
  return root;
}
