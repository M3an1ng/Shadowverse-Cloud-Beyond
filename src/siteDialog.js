import "./styles/site-dialog.css";
import { createDialogFrame, createDialogButton } from "./ui/dialogFrame.js";

let chain = Promise.resolve();
export function showDialog({
  title = "提示",
  message = "",
  confirmText = "确认",
  cancelText = null,
  input = null,
  tone = "normal",
  variant = null,
  target = null,
} = {}) {
  const open = () =>
    new Promise((resolve) => {
      const previous = document.activeElement;
      const overlay = document.createElement("div");
      const frame = variant ? createDialogFrame(title, variant) : null;
      overlay.className = frame ? "battle-dialog-backdrop" : "site-dialog-backdrop";
      const panel = frame?.panel || document.createElement("section");
      if (!frame) panel.className = "site-dialog";
      panel.setAttribute("data-tone", tone);
      panel.setAttribute("role", "dialog");
      panel.setAttribute("aria-modal", "true");
      const heading = frame?.heading || document.createElement("h2");
      heading.textContent = title;
      heading.id = `site-dialog-title-${Date.now()}`;
      panel.setAttribute("aria-labelledby", heading.id);
      const body = document.createElement("p");
      body.textContent = message;
      body.id = `${heading.id}-message`;
      panel.setAttribute("aria-describedby", body.id);
      if (frame) {
        body.className = "battle-dialog-message";
        if (target) {
          const card = document.createElement("div");
          card.className = "battle-dialog-target";
          card.innerHTML = '<div class="battle-dialog-resource" aria-hidden="true"></div><div><div class="battle-dialog-target-name"></div><div class="battle-dialog-target-cost"><span></span><strong></strong></div></div>';
          card.id = body.id;
          card.querySelector('.battle-dialog-resource').textContent = target.resource;
          card.querySelector('.battle-dialog-target-name').textContent = target.name;
          card.querySelector('.battle-dialog-target-cost span').textContent = target.free ? '免费进化' : '消耗';
          card.querySelector('.battle-dialog-target-cost strong').textContent = target.free ? '不消耗 EP' : `1 ${target.resource}`;
          frame.body.append(card);
        } else {
          frame.body.append(body);
        }
      } else {
        panel.append(heading, body);
      }
      let field = null;
      if (input !== null) {
        field = document.createElement("input");
        field.value = String(input);
        field.setAttribute("aria-label", message || title);
        (frame?.body || panel).append(field);
      }
      const actions = frame?.actions || document.createElement("footer");
      let settled = false;
      const finish = (value) => {
        if (settled) return;
        settled = true;
        overlay.remove();
        document.removeEventListener("keydown", onKey, true);
        if (previous?.isConnected) previous.focus();
        resolve(value);
      };
      const onKey = (event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          event.stopImmediatePropagation();
          finish(input !== null ? null : false);
        }
        if (event.key === "Enter" && field && event.target === field) {
          event.preventDefault();
          finish(field.value);
        }
        if (event.key === "Tab") {
          const focusable = [...panel.querySelectorAll("button,input")];
          const index = focusable.indexOf(document.activeElement);
          event.preventDefault();
          focusable[
            (index + (event.shiftKey ? -1 : 1) + focusable.length) %
              focusable.length
          ].focus();
        }
      };
      if (cancelText) {
        const cancel = frame ? createDialogButton(cancelText) : document.createElement("button");
        cancel.type = "button";
        cancel.textContent = cancelText;
        cancel.addEventListener("click", () =>
          finish(input !== null ? null : false),
        );
        actions.append(cancel);
      }
      const confirm = frame ? createDialogButton(confirmText, true) : document.createElement("button");
      confirm.type = "button";
      confirm.textContent = confirmText;
      if (!frame) confirm.className = "site-dialog-primary";
      confirm.addEventListener("click", () =>
        finish(field ? field.value : true),
      );
      actions.append(confirm);
      if (!frame) panel.append(actions);
      overlay.append(panel);
      document.body.append(overlay);
      document.addEventListener("keydown", onKey, true);
      (field || confirm).focus();
    });
  const result = chain.then(open, open);
  chain = result.then(() => {});
  return result;
}
const pendingAlerts = new Map();
export const showAlert = (message) => {
  const text = String(message);
  if (pendingAlerts.has(text)) return pendingAlerts.get(text);
  const result = showDialog({ title: "操作提示", message: text, confirmText: "知道了", tone: alertTone(text), variant: "notice" }).finally(
    () => pendingAlerts.delete(text),
  );
  pendingAlerts.set(text, result);
  return result;
};
export const alertTone = (message) =>
  /失败|错误|异常|出错|未能|无权|不存在|不正确|不可用|不支持|未找到|HTTP\s*[45]\d{2}|\berror\b|timeout/i.test(
    String(message),
  )
    ? "error"
    : /成功|已保存|已复制|已完成|已更新/.test(String(message))
      ? "success"
      : "normal";
export const confirmDialog = (message) =>
  showDialog({
    title: "确认操作",
    message: String(message),
    cancelText: "取消",
    tone: /投降/.test(String(message)) ? "danger" : "confirm",
  });
export const promptDialog = (message, initial = "") =>
  showDialog({
    title: "输入信息",
    message: String(message),
    input: initial,
    cancelText: "取消",
    tone: "confirm",
  });

const battleConfirmations = {
  evolve: { title: '进化确认', confirmText: '确认进化' },
  super: { title: '超进化确认', confirmText: '确认超进化' },
  end: { title: '结束回合', message: '结束后，将进入对手的回合。', cancelText: '继续操作', confirmText: '结束回合' },
  fusion: { title: '融合确认', message: '发动这张卡牌的融合能力？', confirmText: '确认融合' },
  surrender: { title: '投降确认', message: '本局对战将以败北结束，并返回主菜单。', cancelText: '继续对战', confirmText: '确认投降' },
};

export function confirmBattleDialog(kind, { cardName = '该卡牌', freeEvolution = false } = {}) {
  const config = battleConfirmations[kind];
  if (!config) throw new Error(`未知的对战确认类型：${kind}`);
  return showDialog({
    cancelText: '取消',
    ...config,
    variant: kind,
    target: kind === 'evolve' || kind === 'super'
      ? { name: cardName, resource: kind === 'super' ? 'SEP' : 'EP', free: kind === 'evolve' && freeEvolution }
      : null,
  });
}
