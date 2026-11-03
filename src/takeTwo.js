import { exportPoolCode, importPoolCode } from './takeTwoPoolCode.js';
import { createPoolEnvironmentPicker } from "./poolEnvironmentPicker.js";
import {
  formatRoomHandshake,
  draftRoomHandshake,
} from "./multiplayerRoomPresentation.js";
import {
  draftCard,
  draftCurve,
  professionEmblem,
  showDraftDeckPreview,
} from "./miniDraftPresentation.js";
import { getUserSession, generateGuid } from "./userSession.js";
import { historyRequest } from "./cardHistory.js";
import {
  cardValue,
  cardId,
  cardFace,
  classButtons,
  classes,
  createFeatureModal,
  loadStyles,
} from "./cardStyles.js";
import { catalogCards, mountCatalogSearch } from "./cardGallery.js";
import {
  defaultCatalogFilter,
  compareCatalogCards,
  catalogPackOptions,
} from "./cardCatalogQuery.js";
import { confirmDialog, showAlert, promptDialog, showDialog } from "./siteDialog.js";
import { showCardDetailModal } from "./components/CardDetailMockup.js";
import "./styles/take-two-mini.css";
import "./styles/take-two.css";
const emptyPool = () => ({
  packs: [],
  includedIds: [],
  excludedIds: [],
  redraws: {},
  neutralByClass: {},
});
const button = (text, action) => {
  const b = document.createElement("button");
  b.type = "button";
  b.textContent = text;
  b.onclick = action;
  return b;
};
const api = (path, options = {}) =>
  historyRequest("/api/take-two" + path, options);
export async function editTakeTwoPool({
  environment = "open",
  pool = emptyPool(),
  admin = false,
  savedPoolId = "",
  onSave = () => {},
} = {}) {
  const modal = createFeatureModal(admin ? "双选卡池配置" : "自定义双选卡池", {
    wide: true,
  });
  modal.body.classList.add("take-two-pool-editor");
  let config = structuredClone(pool),
    mode = environment,
    revision = 0,
    selectedPoolId = savedPoolId;
  const [cards, styles] = await Promise.all([catalogCards(), loadStyles()]);
  if (!modal.overlay.isConnected) return;
  if (admin) {
    config = await api(`/settings/${mode}`);
    revision = (await api(`/settings/${mode}/revision`)).revision;
  }
  const render = async () => {
    config.neutralByClass ??= {};
    modal.body.replaceChildren();
    let catalogSearch,
      searched = false;
    const toolbar = document.createElement("div");
    toolbar.className = "pool-toolbar";
    if (admin) {
      const select = document.createElement("select");
      for (const [value, label] of [
        ["rotation", "指定"],
        ["unlimited", "无限"],
        ["as2p", "AS2P"],
      ]) {
        const o = document.createElement("option");
        o.value = value;
        o.textContent = label;
        select.append(o);
      }
      select.value = mode;
      select.onchange = async () => {
        mode = select.value;
        config = await api(`/settings/${mode}`);
        revision = (await api(`/settings/${mode}/revision`)).revision;
        render();
      };
      toolbar.append(select);
    } else {
      const saved = document.createElement("select");
      saved.innerHTML = '<option value="">载入已保存卡池</option>';
      const entries = await api("/saved-pools");
      for (const entry of entries) {
        const option = document.createElement("option");
        option.value = entry.id;
        option.textContent = entry.config.name;
        saved.append(option);
      }
      if (!entries.some((entry) => entry.id === selectedPoolId)) selectedPoolId = "";
      saved.value = selectedPoolId;
      saved.onchange = () => {
        const entry = entries.find((x) => x.id === saved.value);
        if (entry) {
          selectedPoolId = entry.id;
          config = structuredClone(entry.config.pool);
          render();
        }
      };
      const savePool = async (asNew = false) => {
        const selected = entries.find((entry) => entry.id === selectedPoolId);
        const name = !asNew && selected
          ? selected.config.name
          : await promptDialog("为卡池命名");
        if (!name?.trim()) return;
        try {
          const id = !asNew && selected ? selected.id : generateGuid();
          await api(`/saved-pools/${id}`, {
            method: "PUT",
            body: JSON.stringify({ name: name.trim(), pool: config }),
          });
          selectedPoolId = id;
          await render();
        } catch (e) {
          reportDraftError(e);
        }
      };
      toolbar.append(
        saved,
        button(selectedPoolId ? "保存卡池" : "保存为我的卡池", () => savePool()),
        button("另存为新卡池", () => savePool(true)),
        button("删除已保存卡池", async () => {
          if (saved.value && (await confirmDialog("删除选中的卡池？"))) {
            await api(`/saved-pools/${saved.value}`, { method: "DELETE" });
            selectedPoolId = "";
            render();
          }
        }),
      );
    }
    toolbar.append(
      button(admin ? "保存配置" : "确认卡池", async () => {
        try {
          if (admin)
            await api(`/settings/${mode}`, {
              method: "PUT",
              body: JSON.stringify({ revision, pool: config }),
            });
          onSave(config, { savedPoolId: selectedPoolId });
          modal.close();
        } catch (e) {
          reportDraftError(e);
        }
      }),
    );
    if (!admin) {
      const shareActions = document.createElement('div');
      shareActions.className = 'pool-share-actions';
      const shareDialog = exporting => {
        const share = createFeatureModal(exporting ? '导出卡池' : '导入卡池');
        share.body.classList.add('pool-code-dialog');
        const info=document.createElement('p');
        info.textContent=exporting ? '复制分享代码，即可分享当前卡包、单卡调整与职业重抽次数。' : '粘贴卡池分享代码。导入将替换当前编辑内容，不会覆盖已保存卡池。';
        const field=document.createElement('textarea');
        field.setAttribute('aria-label','卡池分享代码');
        field.spellcheck=false;
        field.readOnly=exporting;
        if(exporting) field.value=exportPoolCode(config);
        const actions=document.createElement('div');actions.className='pool-code-actions';
        const status=document.createElement('p');status.setAttribute('role','status');
        actions.append(button('取消',()=>share.close()),button(exporting ? '复制代码' : '导入卡池',async()=>{
          try {
            if(exporting) {
              field.focus();field.select();
              try { await navigator.clipboard.writeText(field.value);status.textContent='分享代码已复制'; }
              catch { status.textContent='请按 Ctrl+C 复制已选中的分享代码'; }
            } else {
              const imported=importPoolCode(field.value);
              config=imported;selectedPoolId='';share.close();await render();
            }
          } catch(e) {status.textContent=e.message;}
        }));
        share.body.append(info,field,status,actions);field.focus();if(exporting)field.select();
      };
      shareActions.append(button('导入卡池',()=>shareDialog(false)),button('导出卡池',()=>shareDialog(true)));
      toolbar.append(shareActions);
    }
    modal.body.append(toolbar);
    if (mode === "as2p") {
      const rules = document.createElement("p");
      rules.className = "as2p-rules";
      rules.textContent =
        "AS2P：第7、14次抓牌仅出现金卡和虹卡，四张候选中至少一张虹卡；其余轮次不限制稀有度。中立卡勾选职业后，仅进入勾选职业的卡池；未勾选时随基础卡池。";
      modal.body.append(rules);
    }
    const packSection = document.createElement("section");
    packSection.className = "pool-pack-section";
    const packSearch = document.createElement("input");
    packSearch.placeholder = "搜索选取的卡包";
    const packGrid = document.createElement("div");
    packGrid.className = "pool-pack-grid";
    let packPage = 1;
    const sets = catalogPackOptions(cards);
    const drawPacks = () => {
      const filtered = sets.filter((x) => x.includes(packSearch.value.trim()));
      packGrid.replaceChildren();
      for (const name of filtered.slice((packPage - 1) * 12, packPage * 12)) {
        const label = document.createElement("label");
        label.className = "catalog-check";
        const input = document.createElement("input");
        input.type = "checkbox";
        input.checked = config.packs.includes(name);
        input.onchange = () => {
          config.packs = input.checked
            ? [...config.packs, name]
            : config.packs.filter((x) => x !== name);
          catalogSearch?.setPackScope(config.packs, searched);
        };
        label.append(input, document.createTextNode(name));
        packGrid.append(label);
      }
      const pages = document.createElement("div");
      pages.className = "catalog-pagination";
      const prev = button("‹", () => {
        packPage--;
        drawPacks();
      });
      prev.disabled = packPage <= 1;
      const next = button("›", () => {
        packPage++;
        drawPacks();
      });
      next.disabled = packPage >= Math.ceil(filtered.length / 12);
      pages.append(
        prev,
        document.createTextNode(
          `${packPage} / ${Math.max(1, Math.ceil(filtered.length / 12))}`,
        ),
        next,
      );
      packGrid.append(pages);
    };
    packSearch.oninput = () => {
      packPage = 1;
      drawPacks();
    };
    drawPacks();
    packSection.append(packSearch, packGrid);
    modal.body.append(packSection);
    const rerolls = document.createElement("section");
    rerolls.className = "pool-rerolls";
    for (const [id, label] of classes.slice(1)) {
      const item = document.createElement("label");
      item.textContent = label + "重抽";
      const n = document.createElement("input");
      n.type = "number";
      n.min = 0;
      n.max = 20;
      n.value = config.redraws[id] ?? 2;
      n.oninput = () => {
        if (n.value !== "" && Number.isInteger(Number(n.value)))
          config.redraws[id] = Math.max(0, Math.min(20, Number(n.value)));
      };
      item.append(n);
      rerolls.append(item);
    }
    modal.body.append(rerolls);
    const search = document.createElement("div"),
      results = document.createElement("div");
    results.className = "pool-single-results";
    modal.body.append(search, results);
    let filtered = [],
      page = 1;
    const draw = () => {
      results.replaceChildren();
      for (const card of filtered.slice((page - 1) * 18, page * 18)) {
        const item = document.createElement("div");
        item.className = "pool-single";
        item.append(
          cardFace(card, { onClick: () => showCardDetailModal(card) }),
        );
        const select = document.createElement("select");
        for (const [value, label] of [
          ["base", "随卡包"],
          ["include", "加入"],
          ["exclude", "剔除"],
        ]) {
          const o = document.createElement("option");
          o.value = value;
          o.textContent = label;
          select.append(o);
        }
        const id = cardId(card);
        select.value = config.includedIds.includes(id)
          ? "include"
          : config.excludedIds.includes(id)
            ? "exclude"
            : "base";
        select.onchange = () => {
          config.includedIds = config.includedIds.filter((x) => x !== id);
          config.excludedIds = config.excludedIds.filter((x) => x !== id);
          if (select.value === "include") config.includedIds.push(id);
          if (select.value === "exclude") config.excludedIds.push(id);
          checkbox.checked = select.value === "include";
        };
        const checked = document.createElement("label");
        checked.className = "catalog-check";
        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.checked = config.includedIds.includes(id);
        checkbox.onchange = () => {
          config.includedIds = config.includedIds.filter((x) => x !== id);
          config.excludedIds = config.excludedIds.filter((x) => x !== id);
          if (checkbox.checked) config.includedIds.push(id);
          select.value = checkbox.checked ? "include" : "base";
        };
        checked.append(checkbox, document.createTextNode("加入卡池"));
        item.append(select, checked);
        if (mode === "as2p" && cardValue(card, "class") === "Neutral") {
          const assignments = document.createElement("fieldset");
          assignments.className = "as2p-neutral-assignments";
          const legend = document.createElement("legend");
          legend.textContent = "仅加入以下职业";
          assignments.append(legend);
          for (const [profession, name] of classes.slice(1)) {
            const label = document.createElement("label");
            label.className = "catalog-check";
            const input = document.createElement("input");
            input.type = "checkbox";
            input.checked = (config.neutralByClass[profession] ?? []).includes(
              id,
            );
            input.onchange = () => {
              const ids = (config.neutralByClass[profession] ?? []).filter(
                (x) => x !== id,
              );
              if (input.checked) {
                ids.push(id);
                config.excludedIds = config.excludedIds.filter((x) => x !== id);
              }
              config.neutralByClass[profession] = ids;
              select.value = config.includedIds.includes(id)
                ? "include"
                : config.excludedIds.includes(id)
                  ? "exclude"
                  : "base";
            };
            label.append(input, document.createTextNode(name));
            assignments.append(label);
          }
          item.append(assignments);
        }
        results.append(item);
      }
      const pages = document.createElement("div");
      pages.className = "catalog-pagination";
      const prev = button("上一页", () => {
        page--;
        draw();
      });
      prev.disabled = page === 1;
      const next = button("下一页", () => {
        page++;
        draw();
      });
      next.disabled = page >= Math.ceil(filtered.length / 18);
      pages.append(
        prev,
        document.createTextNode(
          `${page} / ${Math.max(1, Math.ceil(filtered.length / 18))}`,
        ),
        next,
      );
      results.append(pages);
    };
    catalogSearch = mountCatalogSearch(search, {
      cards,
      styles,
      initial: {
        ...defaultCatalogFilter(),
        environment: "open",
        sets: [...config.packs],
      },
      onSearch: (filter, list) => {
        searched = true;
        const active =
          filter.query.trim() ||
          filter.sets.length ||
          filter.costs.length ||
          filter.types.length ||
          filter.rarities.length ||
          filter.tribe ||
          filter.keyword ||
          filter.attackMin !== "" ||
          filter.attackMax !== "" ||
          filter.healthMin !== "" ||
          filter.healthMax !== "" ||
          filter.hasStyles;
        filtered = active ? list : [];
        page = 1;
        draw();
      },
    });
    const empty = document.createElement("p");
    empty.className = "pool-search-empty";
    empty.textContent = "搜索或设置筛选条件后展示单卡";
    results.append(empty);
  };
  await render();
}
async function viewPool(environment) {
  const modal = createFeatureModal("查看双选卡池", { wide: true });
  const [cards, ids, settings] = await Promise.all([
    catalogCards(),
    api("/pool/" + environment),
    environment === "as2p" ? api("/settings/as2p") : Promise.resolve(null),
  ]);
  let selected = ["Forestcraft"],
    page = 1;
  const render = () => {
    modal.body.replaceChildren();
    modal.body.append(
      classButtons(
        selected,
        (v) => {
          selected = v;
          page = 1;
          render();
        },
        { multiple: false },
      ),
    );
    const list = cards
      .filter(
        (c) =>
          ids.includes(cardId(c)) &&
          [selected[0], "Neutral"].includes(cardValue(c, "class")) &&
          (environment !== "as2p" ||
            cardValue(c, "class") !== "Neutral" ||
            !Object.values(settings.neutralByClass ?? {}).some((ids) =>
              ids.includes(cardId(c)),
            ) ||
            (settings.neutralByClass?.[selected[0]] ?? []).includes(cardId(c))),
      )
      .sort(compareCatalogCards);
    const grid = document.createElement("div");
    grid.className = "pool-single-results";
    grid.append(
      ...list
        .slice((page - 1) * 18, page * 18)
        .map((c) => cardFace(c, { onClick: () => showCardDetailModal(c) })),
    );
    const pages = document.createElement("div");
    pages.className = "catalog-pagination";
    const prev = button("上一页", () => {
      page--;
      render();
    });
    prev.disabled = page <= 1;
    const next = button("下一页", () => {
      page++;
      render();
    });
    next.disabled = page >= Math.ceil(list.length / 18);
    pages.append(
      prev,
      document.createTextNode(
        `${page} / ${Math.max(1, Math.ceil(list.length / 18))}`,
      ),
      next,
    );
    modal.body.append(grid, pages);
  };
  render();
}
let waitingLobby = null;
let activeDraftRoom = null;
function setActiveDraftRoom(room) {
  activeDraftRoom = room;
  document.dispatchEvent(new CustomEvent("svb:draft-room-state"));
}
function releaseDraftRoom() {
  waitingLobby?.stop();
  localTestHost = null;
  const userId = getUserSession().userId;
  if (userId) sessionStorage.removeItem(draftStoreKey(userId));
  setActiveDraftRoom(null);
}
let localTestHost = null;
let draftErrorDialog = null;
async function reportDraftError(error) {
  const message = String(error?.message || error);
  if (!/请先退出.*双选房间/.test(message)) return showAlert(message);
  if (draftErrorDialog) return draftErrorDialog;
  draftErrorDialog = (async () => {
    const exit = await showDialog({ message, cancelText: "关闭", confirmText: "退出房间", tone: "danger" });
    if (!exit) return;
    try {
      const room = activeDraftRoom;
      if (room) await api(`/rooms/${room.id}/leave`, {
        method: "POST", headers: { "X-Take-Two-Token": room.player.token },
      });
      else await api("/rooms/leave-current", { method: "POST" });
      releaseDraftRoom();
      const output = document.getElementById("mp-create-output");
      if (output) output.textContent = "已退出房间，可以重新创建。";
      const go = document.getElementById("mp-go-game");
      if (go) go.disabled = true;
    } catch (failure) { await showAlert(failure.message); }
  })().finally(() => { draftErrorDialog = null; });
  return draftErrorDialog;
}
const isLocalDraftWindow = () => new URLSearchParams(location.search).has("localDraftRoom");
const draftStoreKey = (userId) => `svb.take-two.${userId}${isLocalDraftWindow() ? ".local-test" : ""}`;
export async function enterTakeTwoRoom(initial) {
  waitingLobby?.stop();
  setActiveDraftRoom(initial.closed ? null : initial);
  if (initial.closed) { releaseDraftRoom(); return; }
  const role = initial.role || (initial.opponentJoined ? "guest" : "host");
  initial.role = role;
  const session = getUserSession(),
    key = draftStoreKey(session.userId);
  if (role === "host") localTestHost = initial;
  const localButton = document.getElementById("mp-go-game");
  if (localButton && role === "host") {
    localButton.textContent = "本机对战";
    localButton.disabled = !!initial.opponentJoined;
  }
  const output = document.getElementById(
    role === "host" ? "mp-create-output" : "mp-join-output",
  );
  if (output) {
    output.style.whiteSpace = "";
    output.style.color = "rgba(255,255,255,0.85)";
    output.innerHTML = `${role === "host" ? "房间已创建，等待对手加入。" : "已加入房间。"}<br>${formatRoomHandshake(draftRoomHandshake(initial))}`;
    delete output.dataset.lastOutput;
  }
  if (session.userId)
    sessionStorage.setItem(
      key,
      JSON.stringify({ id: initial.id, token: initial.player.token, role }),
    );
  if (initial.opponentJoined) {
    await openDraft(initial);
    return;
  }
  const create = document.getElementById("take-two-create");
  if (create) create.disabled = false;
  let stopped = false,
    busy = false;
  const stop = () => {
    stopped = true;
    clearInterval(timer);
    if (create) create.disabled = false;
    if (waitingLobby?.stop === stop) waitingLobby = null;
  };
  const poll = async () => {
    if (stopped || busy) return;
    busy = true;
    try {
      const state = await api(`/rooms/${initial.id}`, {
        headers: { "X-Take-Two-Token": initial.player.token },
      });
      if (stopped) return;
      if (state.closed) {
        stop();
        releaseDraftRoom();
        if (localButton) localButton.disabled = true;
        if (output) output.textContent = "房间已结束。";
        return;
      }
      if (state.opponentJoined) {
        if (localButton) localButton.disabled = true;
        state.player.plans ??= initial.player.plans;
        stop();
        await openDraft(state);
      }
    } catch (error) {
      if (output) output.title = error.message;
    } finally {
      busy = false;
    }
  };
  const timer = setInterval(poll, 1000);
  waitingLobby = { stop, roomId: initial.id };
}
export async function openDraft(initial) {
  if (!initial.opponentJoined) {
    await enterTakeTwoRoom(initial);
    return;
  }
  const modal = createFeatureModal("双选", { wide: true });
  modal.overlay.classList.add("draft-overlay");
  const closeButton = modal.panel.querySelector("header>button");
  closeButton.textContent = "‹ 退出";
  closeButton.setAttribute("aria-label", "退出房间");
  modal.panel.querySelector("header").classList.add("draft-shell-header");
  const session = getUserSession();
  let room = initial,
    player = structuredClone(initial.player),
    selectedClass = Object.keys(player.plans)[0],
    queue = Promise.resolve(),
    pending = 0,
    closed = false,
    pollBusy = false,
    queueGeneration = 0,
    fitObserver = null,
    motionTimer = null,
    motion = null,
    enteredDraft = false;
  const cards = await catalogCards(),
    byId = new Map(cards.map((c) => [cardId(c), c]));
  const storeKey = draftStoreKey(session.userId);
  if (session.userId)
    sessionStorage.setItem(
      storeKey,
      JSON.stringify({ id: room.id, token: player.token }),
    );
  const headers = { "X-Take-Two-Token": player.token };
  const finish = () => {
    closed = true;
    clearInterval(timer);
    modal.close();
  };
  const checkState = async (state) => {
    const changed =
      player.sequence !== state.player.sequence ||
      room.opponentCount !== state.opponentCount ||
      room.opponentReady !== state.opponentReady ||
      room.opponentJoined !== state.opponentJoined;
    state.player.plans ??= player.plans;
    room = state;
    if (state.closed && !closed) {
      releaseDraftRoom();
      finish();
      await showAlert("对方退出了房间");
      return;
    }
    if (state.handshake && !closed) {
      releaseDraftRoom();
      finish();
      document.dispatchEvent(
        new CustomEvent("svb:take-two-start", { detail: state.handshake }),
      );
      return;
    }
    if (!pending && changed) {
      player = structuredClone(state.player);
      render();
    }
  };
  const send = (action, details = {}) => {
    const sequence = ++player.sequence;
    pending++;
    const generation = queueGeneration;
    queue = queue.then(async () => {
      if (generation !== queueGeneration) return;
      if (closed && action !== "leave") return;
      try {
        const state = await api(`/rooms/${room.id}/action`, {
          method: "POST",
          headers,
          body: JSON.stringify({ action, sequence, ...details }),
        });
        pending--;
        if (pending === 0 && action !== "leave") await checkState(state);
      } catch (e) {
        queueGeneration++;
        pending = 0;
        queue = Promise.resolve();
        const state = await api(`/rooms/${room.id}?includePlans=false`, {
          headers,
        }).catch(() => null);
        if (state) {
          state.player.plans ??= player.plans;
          player = structuredClone(state.player);
          await checkState(state);
          render();
        }
        reportDraftError(e);
      }
    });
  };
  const leave = async () => {
    if (await confirmDialog("是否退出房间？")) {
      try {
        // Leaving must not wait behind queued picks or depend on their sequence.
        await api(`/rooms/${room.id}/leave`, { method: "POST", headers });
        queueGeneration++;
        releaseDraftRoom();
        finish();
      } catch (error) { reportDraftError(error); }
    }
  };
  closeButton.onclick = leave;
  // Escape must follow the same room-exit confirmation as the visible button.
  const stopEscape = (e) => {
    if (e.key === "Escape" && modal.overlay.isConnected) {
      const layer = Number(getComputedStyle(modal.overlay).zIndex) || 0;
      if (
        [
          ...document.querySelectorAll(
            ".feature-overlay,.card-detail-modal-overlay,.history-overlay,.site-dialog-backdrop",
          ),
        ].some(
          (other) =>
            other !== modal.overlay &&
            (Number(getComputedStyle(other).zIndex) || 0) > layer,
        )
      )
        return;
      e.stopImmediatePropagation();
      void leave();
    }
  };
  window.addEventListener("keydown", stopEscape, true);
  const baseFinish = modal.close;
  modal.close = () => {
    fitObserver?.disconnect();
    clearTimeout(motionTimer);
    window.removeEventListener("keydown", stopEscape, true);
    baseFinish();
  };
  const timer = setInterval(async () => {
    if (closed || pollBusy || pending) return;
    pollBusy = true;
    try {
      await checkState(
        await api(`/rooms/${room.id}?includePlans=false`, { headers }),
      );
    } catch {
    } finally {
      pollBusy = false;
    }
  }, 1200);
  const artSeen = new Set(),
    artQueue = [];
  let artWorkers = 0;
  const pumpArt = () => {
    while (artWorkers < 2 && artQueue.length && !closed) {
      const src = artQueue.shift();
      artWorkers++;
      const image = new Image();
      image.decoding = "async";
      image.fetchPriority = "low";
      const done = () => {
        artWorkers--;
        pumpArt();
      };
      image.onload = done;
      image.onerror = done;
      image.src = src;
    }
  };
  const preload = () => {
    if (closed) return;
    const plan = player.plans[player.class || selectedClass];
    if (!plan) return;
    const remaining = plan.rounds.slice(Math.max(0, player.round - 1));
    const ids = remaining
      .flatMap((r) => [...r.offer, ...r.redrawOffers.flat()])
      .flat();
    for (const id of new Set(ids)) {
      const url = cardValue(byId.get(id), "artPreview");
      if (url && !artSeen.has(url)) {
        artSeen.add(url);
        artQueue.push(url);
      }
    }
    pumpArt();
  };

  const render = () => {
    if (closed) return;
    modal.body.replaceChildren();
    modal.panel.classList.add("take-two-screen");
    modal.panel.classList.toggle("choosing-profession", !player.class);
    const classInfo = classes.find(
      (c) => c[0] === (player.class || selectedClass),
    );
    modal.panel.style.setProperty(
      "--profession-rgb",
      classInfo ? hexRgb(classInfo[3]) : "113,177,198",
    );
    const node = (tag, cls, text) => {
      const e = document.createElement(tag);
      if (cls) e.className = cls;
      if (text != null) e.textContent = text;
      return e;
    };
    const pair = (ids, decorative = false) => {
      const row = node("div", "take-two-pair");
      row.append(...ids.map((id) => draftCard(byId.get(id), decorative)));
      return row;
    };
    const preview = () =>
      showDraftDeckPreview(player.cards, byId, {
        profession: player.class,
        environment: room.environment,
      });
    const changeProfession = (name, direction) => {
      if (name === selectedClass || motion) return;
      if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        selectedClass = name;
        render();
        return;
      }
      const core = modal.body.querySelector(".take-two-core");
      if (core) {
        core.dataset.motion = "exit";
        core.dataset.direction = String(direction);
      }
      motion = { phase: "exit", direction };
      motionTimer = setTimeout(() => {
        selectedClass = name;
        motion = { phase: "enter", direction };
        render();
        motionTimer = setTimeout(() => {
          motion = null;
          render();
        }, 210);
      }, 100);
    };
    const header = node("header", "take-two-header");
    const heading = node("div", "take-two-heading");
    const titles = node("div");
    titles.append(
      node("small", "take-two-eyebrow", "TAKE TWO"),
      node(
        "h1",
        "",
        player.class ? `双选 · ${classInfo?.[1]}` : "双选 · 选择职业",
      ),
      node(
        "p",
        "",
        `${{ rotation: "指定", unlimited: "无限", open: "开放", as2p: "AS2P" }[room.environment]}卡池${room.packs?.length ? ` · ${room.packs.length}个卡包` : ""}`,
      ),
    );
    if (player.class) heading.append(professionEmblem(player.class));
    heading.append(titles);
    header.append(heading);
    if (!player.class) {
      const professions = classButtons(
        [selectedClass],
        (v) => {
          if (player.plans[v[0]]) {
            const keys = Object.keys(player.plans),
              index = keys.indexOf(selectedClass),
              next = keys.indexOf(v[0]);
            changeProfession(
              v[0],
              (next - index + keys.length) % keys.length <= keys.length / 2
                ? 1
                : -1,
            );
          }
        },
        { neutral: false, multiple: false },
      );
      professions.className = "take-two-professions";
      professions.setAttribute("aria-label", "职业");
      for (const b of professions.children) {
        b.classList.add("mini-profession-button");
        const id = classes.find((c) => c[1] === b.title)?.[0];
        b.disabled = !!motion || (id && !player.plans[id]);
        if (id) b.replaceChildren(professionEmblem(id));
      }
      header.append(professions);
    }
    const progress = node("div", "take-two-progress");
    const enemy = node("span", "take-two-opponent-progress", "对手已选 ");
    enemy.append(node("strong", "", `${room.opponentCount} / 30`));
    const track = node("i"),
      bar = node("i");
    bar.style.width = `${(room.opponentCount / 30) * 100}%`;
    track.append(bar);
    enemy.append(track);
    const deckButton = button(
      `☰ 我的牌组 ${player.cards.length} / 30`,
      preview,
    );
    deckButton.className = "take-two-deck-button";
    const share = button("复制房间信息", () => copyDraftRoom(room));
    share.className = "take-two-copy";
    if (!modal.panel.querySelector(".draft-room-copy")) {
      share.classList.add("draft-room-copy");
      modal.panel.querySelector(".draft-shell-header").append(share);
    }
    progress.append(enemy, deckButton);
    header.append(progress);
    modal.body.append(header);
    const area = node("div", "take-two-fit-area");
    const content = node("div", "take-two-fit-content");
    area.append(content);
    modal.body.append(area);
    if (!player.class) {
      const layout = node("div", "take-two-class-layout"),
        carousel = node("div", "take-two-carousel");
      const available = Object.keys(player.plans),
        index = available.indexOf(selectedClass),
        plan = player.plans[selectedClass];
      for (const offset of [-1, 1]) {
        const name =
          available[(index + offset + available.length) % available.length];
        const neighbor = node(
          "div",
          `take-two-neighbor ${offset < 0 ? "previous" : "next"}`,
        );
        const neighborHeading = node("span", "take-two-neighbor-heading");
        neighborHeading.append(
          professionEmblem(name),
          document.createTextNode(
            classes.find((c) => c[0] === name)?.[1] || name,
          ),
        );
        neighbor.append(neighborHeading, pair(player.plans[name].core, true));
        neighbor.setAttribute("role", "button");
        neighbor.tabIndex = 0;
        neighbor.setAttribute(
          "aria-label",
          `预览${classes.find((c) => c[0] === name)?.[1] || name}`,
        );
        neighbor.onkeydown = (e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            changeProfession(name, offset);
          }
        };
        neighbor.onclick = () => {
          changeProfession(name, offset);
        };
        carousel.append(neighbor);
        const arrow = button(offset < 0 ? "‹" : "›", () => {
          changeProfession(name, offset);
        });
        arrow.setAttribute(
          "aria-label",
          offset < 0 ? "上一个职业" : "下一个职业",
        );
        arrow.className = `take-two-carousel-arrow ${offset < 0 ? "previous" : "next"}`;
        carousel.append(arrow);
      }
      const core = node("div", "take-two-core");
      if (motion) {
        core.dataset.motion = motion.phase;
        core.dataset.direction = String(motion.direction);
      }
      const coreHeading = node("header", "take-two-core-heading");
      const professionTitle = node("h2");
      professionTitle.append(
        professionEmblem(selectedClass),
        document.createTextNode(classInfo?.[1] || selectedClass),
      );
      const redrawCount = node("span");
      redrawCount.append(
        document.createTextNode("可重抽 "),
        node("b", "", plan.redraws),
        document.createTextNode(" 次"),
      );
      coreHeading.append(professionTitle, redrawCount);
      const choose = button("选择", () => {
        player.class = selectedClass;
        player.cards = [...plan.core];
        player.round = 1;
        player.redraws = plan.redraws;
        send("class", { class: selectedClass });
        render();
      });
      choose.className = "take-two-primary";
      choose.disabled = !!motion;
      choose.setAttribute("aria-label", `选择${classInfo?.[1]}`);
      core.append(coreHeading, pair(plan.core), choose);
      carousel.append(core);
      layout.append(carousel);
      content.append(layout);
    } else if (player.cards.length < 30) {
      const offers = node("div", "take-two-offers");
      if (enteredDraft) offers.style.animation = "none";
      enteredDraft = true;
      const summary = node("aside", "take-two-draft-summary");
      const roundHeading = node("div", "take-two-round");
      roundHeading.append(
        node("h2", "", `第 ${player.round} / 14 轮`),
        node("p", "", "选择一组卡牌"),
      );
      const profession = node("div", "take-two-summary-profession");
      profession.append(
        professionEmblem(player.class),
        node("span", "", classInfo?.[1]),
      );
      summary.append(roundHeading, profession, draftCurve(player.cards, byId));
      const previewButton = button("☰ 查看我的牌组", preview);
      previewButton.className = "take-two-summary-preview";
      summary.append(previewButton);
      const redraw = button(`重抽 · 剩余 ${player.redraws} 次`, () => {
        if (player.redraws <= 0) return;
        const round = player.plans[player.class].rounds[player.round - 1];
        round.offer = round.redrawOffers[round.redrawIndex || 0];
        round.redrawIndex = (round.redrawIndex || 0) + 1;
        player.redraws--;
        send("redraw");
        render();
      });
      redraw.disabled = player.redraws <= 0;
      summary.append(redraw);
      offers.append(summary);
      const round = player.plans[player.class].rounds[player.round - 1];
      for (let side = 0; side < 2; side++) {
        const group = node("section", "take-two-option");
        group.dataset.side = side;
        group.setAttribute("aria-label", side === 0 ? "左侧卡组" : "右侧卡组");
        const choose = button("选择", () => {
          player.cards.push(...round.offer[side]);
          player.round++;
          send("pick", { side });
          render();
        });
        choose.className = "take-two-primary";
        choose.setAttribute(
          "aria-label",
          `选取${side === 0 ? "左" : "右"}侧两张`,
        );
        group.append(pair(round.offer[side]), choose);
        offers.append(group);
      }
      content.append(offers);
    } else {
      const wait = node("div", "take-two-wait");
      wait.append(
        node("h2", "", player.ready ? "等待对手确认" : "牌组已完成"),
        node("p", "", `${classInfo?.[1]} · 30张`),
      );
      const ready = button(player.ready ? "已准备" : "确认牌组并准备", () => {
        if (player.ready) return;
        player.ready = true;
        send("ready");
        render();
      });
      ready.disabled = player.ready;
      ready.className = "take-two-primary";
      wait.append(ready);
      content.append(wait);
    }
    fitObserver?.disconnect();
    const fit = () => {
      if (!area.isConnected) return;
      const scale = Math.min(
        1,
        Math.max(0, area.clientHeight - 6) / Math.max(1, content.offsetHeight),
      );
      content.style.transform = `translate(-50%, ${player.class ? "-50%" : "0"}) scale(${scale})`;
    };
    fitObserver = new ResizeObserver(fit);
    fitObserver.observe(area);
    fitObserver.observe(content);
    requestAnimationFrame(fit);
    requestIdleCallbackSafe(preload);
  };
  render();
}
function hexRgb(hex) {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)).join(",");
}
function requestIdleCallbackSafe(fn) {
  if (window.requestIdleCallback)
    window.requestIdleCallback(fn, { timeout: 2000 });
  else setTimeout(fn, 50);
}
export function initializeTakeTwo() {
  const panel = document.getElementById("mp-create-host")?.closest(".mp-card");
  if (panel && !panel.querySelector(".entry-format-switch")) {
    panel.querySelector("#take-two-entry")?.remove();
    const heading = document.createElement("div");
    heading.className = "entry-create-heading";
    heading.append(panel.querySelector(".mp-card-title"));
    const toggle = document.createElement("div");
    toggle.className = "entry-format-switch";
    toggle.setAttribute("role", "group");
    toggle.setAttribute("aria-label", "房间模式");
    let mode = "standard",
      environment = "rotation",
      pool = emptyPool(),
      savedPoolId = "",
      creating = false;
    const settings = document.createElement("div");
    settings.className = "take-two-create-settings";
    settings.hidden = true;
    const environmentSelect = createPoolEnvironmentPicker(environment);
    let poolUpdate = Promise.resolve();
    const syncSelectedPool = () => {
      const room = activeDraftRoom;
      if (!room || room.role !== "host") return;
      const body = JSON.stringify({ environment, pool });
      poolUpdate = poolUpdate.catch(() => {}).then(async () => {
        if (activeDraftRoom?.id !== room.id) return;
        const updated = await api(`/rooms/${room.id}/pool`, {
          method: "PUT", headers: { "X-Take-Two-Token": room.player.token }, body,
        });
        if (activeDraftRoom?.id === room.id && !creating) {
          activeDraftRoom = updated;
          await enterTakeTwoRoom(updated);
        }
      }).catch(error => { void reportDraftError(error); });
    };
    const configure = button("查看卡池", () =>
      environment === "open"
        ? editTakeTwoPool({ pool, savedPoolId, onSave: (v, selection) => {
            pool = v;
            savedPoolId = selection.savedPoolId;
            syncSelectedPool();
          } })
        : viewPool(environment),
    );
    environmentSelect.onchange = () => {
      environment = environmentSelect.value;
      configure.textContent =
        environment === "open" ? "自定义卡池" : "查看卡池";
      syncSelectedPool();
    };
    const syncRoomControls = () => {
      const active = creating;
      environmentSelect.closeMenu();
      for (const control of environmentSelect.querySelectorAll("button")) control.disabled = active;
      configure.disabled = active;
      create.disabled = active;
      for (const control of toggle.children) control.disabled = active;
      normal.disabled = active;
    };
    document.addEventListener("svb:draft-room-state", syncRoomControls);
    settings.append(environmentSelect, configure);
    const normal = panel.querySelector("#mp-create-host");
    const create = button("创建房间", async () => {
      if (creating) return;
      creating = true;
      syncRoomControls();
      try {
        if (!getUserSession().userId) throw Error("请先登录");
        const room = await api("/rooms", {
          method: "POST",
          body: JSON.stringify({ environment, pool }),
        });
        await enterTakeTwoRoom({ ...room, role: "host" });
      } catch (e) {
        reportDraftError(e);
      } finally {
        creating = false;
        syncRoomControls();
      }
    });
    create.className = "mp-btn primary";
    create.hidden = true;
    create.id = "take-two-create";
    for (const [value, label] of [
      ["standard", "普通模式"],
      ["takeTwo", "双选模式"],
    ]) {
      const b = button(label, () => {
        mode = value;
        for (const child of toggle.children)
          child.setAttribute("aria-pressed", child === b);
        settings.hidden = mode !== "takeTwo";
        create.hidden = mode !== "takeTwo";
        normal.hidden = mode === "takeTwo";
        const go = document.getElementById("mp-go-game");
        if (go) {
          go.textContent = mode === "takeTwo" ? "本机对战" : "进入对战";
          go.disabled = mode === "takeTwo" && !waitingLobby;
        }
        const switcher = document.querySelector(
          "#multiplayer-panel .mp-deck-switcher",
        );
        switcher.querySelector(".mp-deck-label").textContent =
          mode === "takeTwo" ? "当前卡池" : "当前卡组";
        switcher.querySelector(".mp-deck-controls").hidden = mode === "takeTwo";
        switcher.querySelector("#mp-deck-output").hidden = mode === "takeTwo";
      });
      b.setAttribute("aria-pressed", value === mode);
      toggle.append(b);
    }
    heading.append(toggle);
    panel.prepend(heading);
    document
      .querySelector("#multiplayer-panel .mp-deck-switcher")
      .append(settings);
    normal.after(create);

  }
  const localButton = document.getElementById("mp-go-game");
  if (localButton && !localButton.dataset.takeTwoLocalBound) {
    localButton.dataset.takeTwoLocalBound = "true";
    localButton.addEventListener("click", async (event) => {
      if (document.getElementById("take-two-create")?.hidden !== false) return;
      event.preventDefault();
      event.stopImmediatePropagation();
      if (!localTestHost || !waitingLobby || localButton.disabled) return;
      const popup = window.open("about:blank", "_blank");
      if (!popup) { showAlert("请允许弹出窗口后重试本机对战"); return; }
      localButton.disabled = true;
      try {
        const guest = await api(`/rooms/${localTestHost.id}/local-test`, {
          method: "POST",
          headers: { "X-Take-Two-Token": localTestHost.player.token },
        });
        // Keep the token out of the URL and scope the second seat to its own tab.
        popup.sessionStorage.setItem("svb.local-draft-seat", JSON.stringify({
          id: guest.id, token: guest.player.token,
        }));
        popup.sessionStorage.removeItem("svb.lobby.handshake");
        const url = new URL(location.href);
        url.search = "";
        url.searchParams.set("localDraftRoom", guest.id);
        url.hash = "multiplayer";
        popup.location.replace(url.toString());
        popup.opener = null;
      } catch (error) {
        popup.close();
        localButton.disabled = false;
        reportDraftError(error);
      }
    }, true);
  }
  const localRoomId = new URLSearchParams(location.search).get("localDraftRoom");
  if (localRoomId && !document.body.dataset.localDraftStarted) {
    document.body.dataset.localDraftStarted = "true";
    Promise.resolve().then(async () => {
      const seat = JSON.parse(sessionStorage.getItem("svb.local-draft-seat") || "null");
      if (!seat || seat.id !== localRoomId) throw Error("本机对战凭证已失效，请在主窗口重新打开");
      await enterTakeTwoRoom(await api(`/rooms/${seat.id}`, {
        headers: { "X-Take-Two-Token": seat.token },
      }));
    }).catch((error) => reportDraftError(error));
  }
  const tools = document.querySelector(
    "#system-operation-settings",
  )?.parentElement;
  if (tools && !document.getElementById("take-two-admin")) {
    const section = document.createElement("section");
    section.id = "take-two-admin";
    section.className = "tool-card";
    section.innerHTML =
      "<h3>双选卡池配置</h3><p>指定、无限、AS2P卡池与职业重抽次数</p>";
    const openConfiguration = button("打开双选卡池配置", () =>
      editTakeTwoPool({ admin: true, environment: "rotation" }).catch((e) =>
        reportDraftError(e),
      ),
    );
    openConfiguration.className = "tool-button";
    section.append(openConfiguration);
    tools.append(section);
    const update = () =>
      (section.hidden = getUserSession().permissionLevel < 100);
    update();
    document.addEventListener("svb:user-session-changed", update);
  }
}

export async function tryJoinTakeTwo(roomId, code) {
  const response = await fetch(
    `/api/take-two/rooms/${encodeURIComponent(roomId)}/kind`,
  );
  if (response.status === 404) return false;
  if (!response.ok) throw Error("房间类型查询失败");
  const kind = await response.json();
  if (!kind.takeTwo) return false;
  await enterTakeTwoRoom(
    await api("/join", {
      method: "POST",
      body: JSON.stringify({ code, roomId: kind.roomId }),
    }),
  );
  return true;
}
async function copyDraftRoom(room) {
  const text = `GameId: ${room.id}\nJoinCode: ${room.code}`;
  try {
    if (navigator.clipboard?.writeText)
      await navigator.clipboard.writeText(text);
    else {
      const area = document.createElement("textarea");
      area.value = text;
      area.style.position = "fixed";
      area.style.left = "-9999px";
      document.body.append(area);
      area.select();
      const copied = document.execCommand("copy");
      area.remove();
      if (!copied) throw Error();
    }
    showAlert("房间信息已复制");
  } catch {
    showAlert(text);
  }
}
