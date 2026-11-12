import { getUserSession } from "./userSession.js";
import { showAlert, confirmDialog } from "./siteDialog.js";
import { showCardDetailModal } from "./components/CardDetailMockup.js";
import { renderDescriptionHtml } from "./descriptionParser.js";
import "./styles/card-history.css";

const field = (object, name) =>
  object?.[name] ?? object?.[name[0].toUpperCase() + name.slice(1)];
export async function historyRequest(path, options = {}) {
  const session = getUserSession();
  const response = await fetch(path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      "X-Editor-UserId": session.userId || "",
      ...options.headers,
    },
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw Object.assign(
      new Error(
        error?.error ||
          error?.message ||
          (response.status === 403
            ? "需要L10及以上权限"
            : `请求失败：${response.status}`),
      ),
      { status: response.status, code: error?.code, saved: error?.saved, card: error?.card },
    );
  }
  return response.status === 204 ? null : response.json();
}
export function showCardHistory(card, onRestored, scope = null) {
  const id = card.id ?? card.Id ?? card.CardId;
  if (!id && id !== -1 && id !== 0) return;
  const resolvedScope = scope || ((card?.isMirageCard ?? card?.IsMirageCard) ? "mirage" : "standard");
  const scopeQuery = `?scope=${encodeURIComponent(resolvedScope)}`;
  document.getElementById("card-history-overlay")?.remove();
  const overlay = document.createElement("div");
  overlay.id = "card-history-overlay";
  overlay.className = "history-overlay";
  const panel = document.createElement("section");
  panel.className = "history-dialog";
  panel.setAttribute("role", "dialog");
  panel.setAttribute("aria-modal", "true");
  panel.setAttribute("aria-label", "版本历史");
  panel.innerHTML =
    '<header><h2></h2><button type="button" data-action="close" aria-label="关闭">×</button></header><p class="history-status" role="status"></p><main><nav aria-label="卡牌历史版本"></nav><section class="history-detail"></section></main><footer><button type="button" data-action="close">关闭</button><button type="button" data-action="restore">回退到此版本</button></footer>';
  panel.querySelector("h2").textContent =
    "版本历史 · " + (card.name ?? card.Name ?? "");
  overlay.append(panel);
  document.body.append(overlay);
  const nav = panel.querySelector("nav"),
    detail = panel.querySelector(".history-detail"),
    status = panel.querySelector(".history-status");
  let entries = [],
    selected = null,
    snapshot = null,
    note = "",
    revision = 0,
    busy = false,
    generation = 0;
  const previous = document.activeElement;
  const close = () => {
    if (busy) return;
    overlay.remove();
    document.removeEventListener("keydown", key, true);
    previous?.isConnected && previous.focus();
  };
  const key = (event) => {
    if (event.key === "Escape") {
      event.stopImmediatePropagation();
      close();
    }
  };
  document.addEventListener("keydown", key, true);
  const base = `/api/cards/${id}/history`;
  const action = async (fn) => {
    if (busy) return;
    busy = true;
    status.textContent = "";
    panel.querySelectorAll("button").forEach((b) => (b.disabled = true));
    try {
      await fn();
    } catch (error) {
      status.textContent = error.message;
    } finally {
      busy = false;
      renderButtons();
    }
  };
  function renderButtons() {
    panel.querySelectorAll("button").forEach((b) => (b.disabled = false));
    panel.querySelector('[data-action="restore"]').disabled =
      !snapshot || !!field(selected, "current");
    const remove = detail.querySelector('[data-action="delete"]');
    if (remove) remove.disabled = !!field(selected, "current");
  }
  function renderNav() {
    nav.replaceChildren();
    for (const entry of entries) {
      const button = document.createElement("button");
      button.type = "button";
      button.setAttribute(
        "aria-pressed",
        String(field(entry, "id") === field(selected, "id")),
      );
      const title = document.createElement("strong");
      title.textContent = `版本 ${field(entry, "revision")}${field(entry, "current") ? " · 当前使用" : ""}`;
      const editor = document.createElement("span");
      editor.textContent =
        "编辑者：" + (field(entry, "editor") || "未知（历史未记录）");
      const time = document.createElement("time");
      time.textContent = field(entry, "modifiedAt")
        ? new Date(field(entry, "modifiedAt")).toLocaleString("zh-CN")
        : "修改时间未知";
      const annotation = document.createElement("small");
      annotation.textContent = field(entry, "note") || "";
      button.append(title, editor, time, annotation);
      button.onclick = () =>
        void action(async () => {
          const input = detail.querySelector("textarea");
          if (
            input &&
            input.value !== note &&
            !(await confirmDialog("注释尚未保存，确定切换版本？"))
          )
            return;
          await select(entry);
        });
      nav.append(button);
    }
  }
  async function select(entry) {
    selected = entry;
    note = field(entry, "note") || "";
    snapshot = null;
    renderNav();
    detail.textContent = "正在加载版本…";
    const request = ++generation;
    const data = await historyRequest(`${base}/${field(entry, "id")}${scopeQuery}`);
    if (!overlay.isConnected || request !== generation) return;
    snapshot = data.card ?? data.Card;
    detail.innerHTML =
      '<h3></h3><p class="history-meta"></p><div class="history-description"></div><p class="history-ability-count"></p><button data-action="details">查看卡牌详情</button><label>版本注释<textarea maxlength="2000" aria-label="版本注释"></textarea></label><div class="history-actions"><button data-action="note">保存注释</button><button data-action="delete">删除此版本</button></div>';
    detail.querySelector("h3").textContent = field(snapshot, "name");
    detail.querySelector(".history-meta").textContent =
      "费用 " + field(snapshot, "cost");
    detail.querySelector(".history-description").innerHTML =
      renderDescriptionHtml(field(snapshot, "description") || "");
    detail.querySelector(".history-ability-count").textContent =
      `包含 ${(data.abilities ?? data.Abilities ?? []).length} 条能力配置`;
    detail.querySelector("textarea").value = note;
    renderButtons();
  }
  async function load() {
    const data = await historyRequest(`${base}${scopeQuery}`);
    if (!overlay.isConnected) return;
    entries = data.entries ?? data.Entries ?? [];
    revision = data.revision ?? data.Revision;
    await select(
      entries.find((e) => field(e, "id") === field(selected, "id")) ||
        entries[0],
    );
  }
  panel.addEventListener("click", (event) => {
    const button = event.target.closest("[data-action]");
    if (!button) return;
    const type = button.dataset.action;
    if (type === "close") {
      close();
      return;
    }
    if (type === "details") {
      showCardDetailModal(snapshot, { originalCardMode: true, stylePreferenceApplied: true });
      return;
    }
    void action(async () => {
      const entryId = field(selected, "id");
      if (type === "note") {
        const value = detail.querySelector("textarea").value;
        await historyRequest(`${base}/${entryId}${scopeQuery}`, {
          method: "PATCH",
          body: JSON.stringify({ note: value }),
        });
        await load();
        status.textContent = "注释已保存";
      }
      if (
        type === "delete" &&
        (await confirmDialog("确定删除此历史版本？删除后无法再回退到该版本。"))
      ) {
        await historyRequest(`${base}/${entryId}${scopeQuery}`, { method: "DELETE" });
        selected = null;
        await load();
        status.textContent = "历史版本已删除";
      }
      if (
        type === "restore" &&
        (await confirmDialog(
          "确定回退到此版本？将创建新版本，保留回退前的记录，并放弃当前未保存的编辑。",
        ))
      ) {
        let saved, refreshFailure;
        try {
          saved = await historyRequest(`${base}/${entryId}/restore${scopeQuery}`, {
            method: "POST",
            body: JSON.stringify({ revision }),
          });
        } catch (error) {
          if (error.code !== "CARD_RESTORE_CACHE_REFRESH_FAILED" || error.saved !== true) throw error;
          saved = error.card;
          refreshFailure = error;
        }
        try {
          await onRestored?.(saved);
          await load();
        } catch (error) {
          if (refreshFailure) {
            throw new Error(`${refreshFailure.message} 重新读取已保存内容也失败：${error.message}`, { cause: error });
          }
          throw error;
        }
        if (refreshFailure) throw refreshFailure;
        status.textContent = "已回退，并创建新版本";
      }
    });
  });
  void action(load);
}

export function installCardHistoryActions(editor) {
  const sidebar = editor.container?.querySelector(
    ".right-sidebar .sidebar-content",
  );
  if (!sidebar) return;
  let host = sidebar.querySelector(".workshop-history-actions");
  if (!host) {
    host = document.createElement("div");
    host.className = "workshop-history-actions";
    host.innerHTML =
      '<button type="button" class="btn-tech" data-export-card>导出卡牌配置</button><button type="button" class="btn-tech" data-card-history>版本历史</button>';
    sidebar.append(host);
    host.querySelector("[data-card-history]").onclick = () =>
      showCardHistory(
        editor.currentCard,
        async (saved) => {
          editor.clearDataCaches();
          await editor.loadCardList({ force: true });
          await editor.selectCard(saved.id ?? saved.Id ?? saved.CardId);
        },
        editor.cardPoolScope || "standard",
      );
    host.querySelector("[data-export-card]").onclick = async () => {
      try {
        const cardId = editor.getCurrentCardId();
        const scopeParam = `?scope=${encodeURIComponent(editor.cardPoolScope || "standard")}`;
        const [definition, abilities] = await Promise.all([
          historyRequest(`/api/cards/${cardId}${scopeParam}`),
          historyRequest(`/api/cards/${cardId}/abilities${scopeParam}`),
        ]);
        const url = URL.createObjectURL(
          new Blob([JSON.stringify({ card: definition, abilities }, null, 2)], {
            type: "application/json",
          }),
        );
        const link = document.createElement("a");
        link.href = url;
        link.download = `card-${cardId}.json`;
        link.click();
        URL.revokeObjectURL(url);
      } catch (error) {
        showAlert(error.message);
      }
    };
  }
  const session = getUserSession(),
    level = Number(session.permissionLevel) || 0;
  host.hidden = level < 10;
  host.querySelector("[data-export-card]").hidden = level < 100;
  host
    .querySelectorAll("button")
    .forEach(
      (button) =>
        (button.disabled = !editor.currentCard || editor.currentCard.isNew),
    );
}
