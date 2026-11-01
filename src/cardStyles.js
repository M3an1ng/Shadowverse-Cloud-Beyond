import { showCardDetailModal } from "./components/CardDetailMockup.js";
import { DefinitionPicker } from "./components/DefinitionPicker.js";
import { DefinitionPickerCatalog } from "./components/definitionPickerCatalog.js";
import { miniCardBadge } from "./miniCardBadge.js";
import { getUserSession, generateGuid } from "./userSession.js";
import { historyRequest } from "./cardHistory.js";
import { confirmDialog, showAlert } from "./siteDialog.js";
import "./styles/card-styles.css";
import { cardValue, cardId, classes } from "./cardCatalogModel.js";
export { cardValue, cardId, classes } from "./cardCatalogModel.js";
const iconAssets = import.meta.glob("./icon/svg/class_*.svg", {
  eager: true,
  query: "?url",
  import: "default",
});
export function classButtons(
  selected,
  onChange,
  { neutral = true, multiple = true } = {},
) {
  const row = document.createElement("div");
  row.className = "catalog-classes";
  for (const [id, label, icon, color] of classes.filter(
    (_, i) => neutral || i > 0,
  )) {
    const button = document.createElement("button");
    button.type = "button";
    button.title = label;
    button.setAttribute("aria-label", label);
    button.style.setProperty("--class-color", color);
    button.classList.toggle("selected", selected.includes(id));
    button.setAttribute("aria-pressed", selected.includes(id));
    const img = document.createElement("img");
    img.src = iconAssets[`./icon/svg/class_${icon}.svg`];
    img.alt = label;
    button.append(img);
    button.onclick = () =>
      onChange(
        multiple
          ? selected.includes(id)
            ? selected.filter((x) => x !== id)
            : [...selected, id]
          : [id],
      );
    row.append(button);
  }
  return row;
}
export function cardFace(card, { appearance = null, onClick = null } = {}) {
  const root = document.createElement(onClick ? "button" : "div");
  root.className = "catalog-card";
  if (onClick) {
    root.type = "button";
    root.onclick = onClick;
  }
  const face = document.createElement("div");
  face.className =
    "catalog-face " + String(cardValue(card, "rarity")).toLowerCase();
  const img = document.createElement("img");
  img.src = appearance?.artPreview || cardValue(card, "artPreview");
  img.alt = appearance?.name || cardValue(card, "name");
  img.decoding = "async";
  img.loading = "lazy";
  face.append(img);
  face.append(miniCardBadge("cost", cardValue(card, "cost", 0)));
  if (String(cardValue(card, "type")).toLowerCase() === "follower")
    face.append(
      miniCardBadge("attack", cardValue(card, "attack", 0)),
      miniCardBadge("health", cardValue(card, "health", 0)),
    );
  const name = document.createElement("span");
  name.className = "catalog-card-name";
  name.textContent = appearance?.name || cardValue(card, "name");
  root.append(face, name);
  return root;
}
export function createFeatureModal(title, { wide = false } = {}) {
  const overlay = document.createElement("div");
  overlay.className = "feature-overlay";
  const panel = document.createElement("section");
  panel.className = "feature-dialog" + (wide ? " wide" : "");
  panel.setAttribute("role", "dialog");
  panel.setAttribute("aria-modal", "true");
  const header = document.createElement("header");
  const heading = document.createElement("h2");
  heading.textContent = title;
  const close = document.createElement("button");
  close.textContent = "×";
  close.setAttribute("aria-label", "关闭");
  header.append(heading, close);
  const body = document.createElement("div");
  body.className = "feature-body";
  panel.append(header, body);
  overlay.append(panel);
  const layers = [
    ...document.querySelectorAll(
      ".feature-overlay,.card-detail-modal-overlay,.history-overlay",
    ),
  ].map((el) => Number(getComputedStyle(el).zIndex) || 0);
  overlay.style.zIndex = String(Math.max(100010, ...layers) + 20);
  document.body.append(overlay);
  const previous = document.activeElement;
  const dispose = () => {
    overlay.remove();
    document.removeEventListener("keydown", keydown, true);
    previous?.isConnected && previous.focus();
  };
  const keydown = (e) => {
    if (e.key === "Escape") {
      const layer = Number(getComputedStyle(overlay).zIndex) || 0;
      if (
        [
          ...document.querySelectorAll(
            ".feature-overlay,.card-detail-modal-overlay,.history-overlay,.site-dialog-backdrop",
          ),
        ].some(
          (other) =>
            other !== overlay &&
            (Number(getComputedStyle(other).zIndex) || 0) > layer,
        )
      )
        return;
      e.stopImmediatePropagation();
      dispose();
    }
  };
  close.onclick = dispose;
  document.addEventListener("keydown", keydown, true);
  close.focus();
  return { overlay, panel, body, close: dispose };
}
let styleTask = null;
export function loadStyles(force = false) {
  if (force) styleTask = null;
  return (styleTask ??= historyRequest("/api/card-styles").catch((e) => {
    styleTask = null;
    throw e;
  }));
}
export async function styleFor(card, owner) {
  const definitions = await loadStyles();
  const bound = definitions.find((c) =>
    c.styles.some((s) => s.bindings.some((b) => b.cardId === cardId(card))),
  );
  if (bound) return { bound, definitions };
  const config = definitions.find((c) => c.cardId === cardId(card));
  return { config, definitions };
}
export async function mountStylePicker(
  container,
  card,
  onSelected,
  restorePreference = true,
) {
  const id = cardId(card);
  const session = getUserSession();
  const owner = card.OwnerPlayerId || session.userId;
  if (
    !session.userId ||
    (owner && owner.toLowerCase() !== session.userId.toLowerCase())
  )
    return;
  const { config, bound } = await styleFor(card, owner);
  if (bound || !config?.styles.length || !container.isConnected) return;
  let base = card;
  if (card.OwnerPlayerId) {
    try {
      base = await historyRequest(`/api/cards/${id}`);
    } catch {}
  }
  const button = document.createElement("button");
  button.className = "card-style-open inspect-btn";
  button.type = "button";
  const styleLabel = document.createElement("span");
  styleLabel.className = "card-style-open-label";
  styleLabel.textContent = "卡片\n风格";
  button.append(styleLabel);
  button.setAttribute("aria-label", "卡片风格");
  button.title = "卡片风格";
  container.append(button);
  if (restorePreference) {
    const prefs = await historyRequest("/api/card-styles/preferences");
    const selected = config.styles.find((s) => s.id === prefs[id]);
    if (selected && container.isConnected) {
      onSelected(selected.appearance, base);
    }
  }
  button.onclick = async () => {
    const modal = createFeatureModal("选择卡片风格", { wide: true });
    modal.overlay.classList.add("card-style-picker");
    const grid = document.createElement("div");
    grid.className = "style-picker-grid";
    modal.body.append(grid);
    const prefs = await historyRequest("/api/card-styles/preferences");
    let busy = false;
    for (const style of [null, ...config.styles]) {
      const choice = document.createElement("article");
      choice.classList.toggle(
        "selected",
        (prefs[id] || null) === (style?.id || null),
      );
      const wrap = document.createElement("div");
      wrap.className = "style-face-wrap";
      wrap.append(
        cardFace(base, {
          appearance: style?.appearance,
          onClick: () =>
            showCardDetailModal(
              { ...base, ...style?.appearance },
              { stylePreferenceApplied: true, styleOriginalCard: base },
            ),
        }),
      );
      const label = document.createElement("span");
      label.textContent = style?.label || "默认风格";
      const select = document.createElement("button");
      select.type = "button";
      select.className = "style-select-button";
      select.textContent = "选择";
      select.setAttribute("aria-label", `选择${style?.label || "默认风格"}`);
      select.onclick = async () => {
        if (busy) return;
        busy = true;
        for (const action of grid.querySelectorAll(".style-select-button"))
          action.disabled = true;
        try {
          await historyRequest("/api/card-styles/preferences", {
            method: "PUT",
            body: JSON.stringify({ cardId: id, styleId: style?.id ?? null }),
          });
          document.dispatchEvent(
            new CustomEvent("svb:card-style-changed", {
              detail: { cardId: id, styleId: style?.id ?? null },
            }),
          );
          if (modal.overlay.isConnected) {
            onSelected(style?.appearance ?? null, base);
            modal.close();
          }
        } catch (error) {
          showAlert(error.message);
        } finally {
          busy = false;
          for (const action of grid.querySelectorAll(".style-select-button"))
            action.disabled = false;
        }
      };
      choice.append(wrap, label, select);
      grid.append(choice);
    }
  };
}
function appearance(card) {
  return {
    name: cardValue(card, "name"),
    description: cardValue(card, "description"),
    artPreview: cardValue(card, "artPreview"),
    artDetail: cardValue(card, "artDetail"),
  };
}
function input(label, value, onChange, { area = false } = {}) {
  const field = document.createElement("label");
  field.className = "style-field";
  field.append(document.createTextNode(label));
  const control = document.createElement(area ? "textarea" : "input");
  control.value = value || "";
  control.oninput = () => onChange(control.value);
  field.append(control);
  return field;
}
export async function mountStyleEditor(editor) {
  const panel = editor.container.querySelector("#styles-tab");
  const id = editor.getCurrentCardId();
  if (!panel || !id) return;
  panel.textContent = "正在读取卡片风格";
  try {
    let config = await historyRequest(`/api/card-styles/${id}`);
    if (editor.getCurrentCardId() !== id || !panel.isConnected) return;
    editor._styleDrafts ??= new Map();
    config = editor._styleDrafts.get(id) || config;
    editor._styleDrafts.set(id, config);
    let active = config.styles[0]?.id,
      dirty = false;
    const picker = new DefinitionPicker(new DefinitionPickerCatalog());
    const changed = () => {
      dirty = true;
    };
    const render = () => {
      panel.replaceChildren();
      const toolbar = document.createElement("div");
      toolbar.className = "style-editor-toolbar";
      const select = document.createElement("select");
      for (const s of config.styles) {
        const o = document.createElement("option");
        o.value = s.id;
        o.textContent = s.label;
        select.append(o);
      }
      select.value = active || "";
      select.onchange = () => {
        active = select.value;
        render();
      };
      const add = document.createElement("button");
      add.textContent = "新增风格";
      add.onclick = () => {
        const style = {
          id: generateGuid(),
          label: "新风格",
          appearance: appearance(editor.currentCard),
          bindings: [],
        };
        config.styles.push(style);
        active = style.id;
        changed();
        render();
      };
      const save = document.createElement("button");
      save.textContent = "保存卡片风格";
      save.onclick = async () => {
        if (save.disabled) return;
        save.disabled = true;
        try {
          config = await historyRequest(`/api/card-styles/${id}`, {
            method: "PUT",
            body: JSON.stringify(config),
          });
          dirty = false;
          editor._styleDrafts.set(id, config);
          await loadStyles(true);
          showAlert("卡片风格已保存");
          render();
        } catch (e) {
          showAlert(e.message);
        } finally {
          save.disabled = false;
        }
      };
      editor._saveStyle = () => save.onclick();
      editor._saveStyleCardId = id;
      if (config.styles.length) toolbar.append(select);
      toolbar.append(add, save);
      panel.append(toolbar);
      const style = config.styles.find((s) => s.id === active);
      if (!style) return;
      const remove = document.createElement("button");
      remove.textContent = "删除此风格";
      remove.onclick = async () => {
        if (await confirmDialog("删除此卡片风格？")) {
          config.styles = config.styles.filter((s) => s !== style);
          active = config.styles[0]?.id;
          changed();
          render();
        }
      };
      toolbar.append(remove);
      panel.append(
        input("风格名称", style.label, (v) => {
          style.label = v;
          const option = [...select.options].find(
            (item) => item.value === style.id,
          );
          if (option) option.textContent = v || "未命名风格";
          changed();
        }),
      );
      const editAppearance = (target, host) => {
        host.append(
          input("卡名", target.name, (v) => {
            target.name = v;
            changed();
          }),
          input(
            "描述文本",
            target.description,
            (v) => {
              target.description = v;
              changed();
            },
            { area: true },
          ),
        );
        const media = document.createElement("div");
        media.className = "asset-panel style-art-panel";
        const heading = document.createElement("div");
        heading.className = "asset-header";
        const title = document.createElement("h4");
        title.className = "tech-header-small";
        title.textContent = "预览图";
        heading.append(title);
        const preview = document.createElement("div");
        preview.className = "asset-preview";
        const drawPreview = () => {
          preview.replaceChildren();
          preview.classList.toggle("has-image", Boolean(target.artPreview));
          if (target.artPreview) {
            const image = document.createElement("img");
            image.src = target.artPreview;
            image.alt = target.name || "风格预览图";
            preview.append(image);
          } else {
            const placeholder = document.createElement("div");
            placeholder.className = "asset-placeholder";
            placeholder.textContent = "暂无预览图";
            preview.append(placeholder);
          }
        };
        drawPreview();
        const actions = document.createElement("div");
        actions.className = "asset-actions";
        const upload = document.createElement("button");
        upload.type = "button";
        upload.className = "btn-tech btn-small";
        upload.textContent = "上传";
        upload.setAttribute(
          "aria-label",
          target === style.appearance ? "上传风格预览图" : "上传绑定卡预览图",
        );
        const path = document.createElement("div");
        path.className = "asset-path";
        path.textContent = target.artPreview || "暂无文件";
        upload.onclick = () =>
          editor.openArtCropModal("preview", {
            onUpload: async (blob, filename) => {
              upload.disabled = true;
              try {
                const data = new FormData();
                data.append("file", blob, filename);
                const response = await fetch("/api/card-styles/art", {
                  method: "POST",
                  headers: { "X-Editor-UserId": getUserSession().userId },
                  body: data,
                });
                if (!response.ok) {
                  const failure = await response.json().catch(() => null);
                  throw Error(failure?.error || "图片上传失败");
                }
                const image = await response.json();
                target.artPreview = image.url;
                target.artDetail = image.url;
                changed();
                drawPreview();
                path.textContent = image.url;
              } finally {
                upload.disabled = false;
              }
            },
          });
        actions.append(upload);
        media.append(heading, preview, actions, path);
        host.append(media);
      };
      editAppearance(style.appearance, panel);

      const bindingArea = document.createElement("div");
      bindingArea.className = "style-bindings";
      const title = document.createElement("h3");
      title.textContent = "绑定衍生卡";
      bindingArea.append(title);
      for (const binding of style.bindings) {
        const box = document.createElement("fieldset");
        const legend = document.createElement("legend");
        legend.textContent = `绑定卡牌 #${binding.cardId}`;
        const unlink = document.createElement("button");
        unlink.textContent = "解除绑定";
        unlink.onclick = () => {
          style.bindings = style.bindings.filter((b) => b !== binding);
          changed();
          render();
        };
        box.append(legend, unlink);
        editAppearance(binding.appearance, box);
        bindingArea.append(box);
      }
      const controls = document.createElement("div");
      controls.className = "style-binding-picker";
      const cardInput = document.createElement("input");
      cardInput.type = "text";
      cardInput.inputMode = "numeric";
      cardInput.placeholder = "输入卡牌ID并添加";
      cardInput.setAttribute("aria-label", "绑定卡牌ID");
      const bindCards = async (ids) => {
        const incoming = [...new Set(ids.map(Number))];
        if (incoming.some((value) => !Number.isInteger(value) || value <= 0))
          throw Error("请输入有效的卡牌ID");
        if (incoming.includes(Number(id))) throw Error("不能将主卡绑定到自身");
        const fresh = incoming.filter(
          (value) =>
            !style.bindings.some((binding) => binding.cardId === value),
        );
        if (!fresh.length) return;
        const cards = await Promise.all(
          fresh.map((value) => historyRequest(`/api/cards/${value}`)),
        );
        for (const card of cards)
          style.bindings.push({
            cardId: cardId(card),
            appearance: appearance(card),
          });
        changed();
        if (panel.isConnected && editor.getCurrentCardId() === id) render();
      };
      const addCard = document.createElement("button");
      addCard.type = "button";
      addCard.textContent = "添加";
      addCard.onclick = async () => {
        const value = cardInput.value.trim();
        if (!value) return;
        addCard.disabled = true;
        try {
          await bindCards([value]);
        } catch (error) {
          showAlert(error.message);
        } finally {
          addCard.disabled = false;
        }
      };
      cardInput.onkeydown = (event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          addCard.click();
        }
      };
      const chooseCards = document.createElement("button");
      chooseCards.type = "button";
      chooseCards.textContent = "选择卡牌";
      chooseCards.onclick = async () => {
        chooseCards.disabled = true;
        try {
          const chosen = await picker.openCards([], { allowMultiple: true });
          if (chosen.length) await bindCards(chosen);
        } catch (error) {
          showAlert(error.message);
        } finally {
          chooseCards.disabled = false;
        }
      };
      controls.append(cardInput, addCard, chooseCards);
      bindingArea.append(controls);
      panel.append(bindingArea);
    };
    render();
  } catch (e) {
    panel.textContent = e.message;
  }
}
