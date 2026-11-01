import "./styles/site-dialog.css";

let chain = Promise.resolve();
export function showDialog({
  title = "提示",
  message = "",
  confirmText = "确认",
  cancelText = null,
  input = null,
  tone = "normal",
} = {}) {
  const open = () =>
    new Promise((resolve) => {
      const previous = document.activeElement;
      const overlay = document.createElement("div");
      overlay.className = "site-dialog-backdrop";
      const panel = document.createElement("section");
      panel.className = "site-dialog";
      panel.setAttribute("data-tone", tone);
      panel.setAttribute("role", "dialog");
      panel.setAttribute("aria-modal", "true");
      const heading = document.createElement("h2");
      heading.textContent = title;
      heading.id = `site-dialog-title-${Date.now()}`;
      panel.setAttribute("aria-labelledby", heading.id);
      const body = document.createElement("p");
      body.textContent = message;
      panel.append(heading, body);
      let field = null;
      if (input !== null) {
        field = document.createElement("input");
        field.value = String(input);
        field.setAttribute("aria-label", message || title);
        panel.append(field);
      }
      const actions = document.createElement("footer");
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
        const cancel = document.createElement("button");
        cancel.textContent = cancelText;
        cancel.addEventListener("click", () =>
          finish(input !== null ? null : false),
        );
        actions.append(cancel);
      }
      const confirm = document.createElement("button");
      confirm.textContent = confirmText;
      confirm.className = "site-dialog-primary";
      confirm.addEventListener("click", () =>
        finish(field ? field.value : true),
      );
      actions.append(confirm);
      panel.append(actions);
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
  const result = showDialog({ message: text, tone: alertTone(text) }).finally(
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
