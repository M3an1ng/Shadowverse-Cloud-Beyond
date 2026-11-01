let pickerSequence = 0;
const choices = [
  ["rotation", "指定"],
  ["unlimited", "无限"],
  ["open", "开放"],
  ["as2p", "AS2P"],
];
export function createPoolEnvironmentPicker(initial = "rotation") {
  const root = document.createElement("div");
  root.className = "pool-environment-picker";
  root.value = initial;
  const trigger = document.createElement("button");
  trigger.type = "button";
  trigger.className = "pool-environment-trigger";
  trigger.setAttribute("role", "combobox");
  trigger.setAttribute("aria-label", "双选环境");
  trigger.setAttribute("aria-haspopup", "listbox");
  trigger.setAttribute("aria-expanded", "false");
  const menu = document.createElement("div");
  menu.className = "pool-environment-menu";
  menu.id = `pool-environment-${++pickerSequence}`;
  menu.setAttribute("role", "listbox");
  menu.setAttribute("aria-label", "卡池环境");
  menu.hidden = true;
  trigger.setAttribute("aria-controls", menu.id);
  root.closeMenu = () => {
    menu.hidden = true;
    trigger.setAttribute("aria-expanded", "false");
  };
  const draw = () => {
    trigger.textContent =
      choices.find(([value]) => value === root.value)?.[1] || "指定";
    for (const option of menu.children)
      option.setAttribute(
        "aria-selected",
        String(option.dataset.value === root.value),
      );
  };
  root.setValue = (value) => { root.value = value; draw(); root.closeMenu(); };
  const open = () => {
    menu.hidden = false;
    trigger.setAttribute("aria-expanded", "true");
  };
  for (const [value, label] of choices) {
    const option = document.createElement("button");
    option.type = "button";
    option.textContent = label;
    option.dataset.value = value;
    option.setAttribute("role", "option");
    option.onclick = () => {
      root.value = value;
      draw();
      root.closeMenu();
      trigger.focus();
      root.onchange?.();
    };
    menu.append(option);
  }
  trigger.onclick = () => {
    if (menu.hidden) open();
    else root.closeMenu();
  };
  trigger.onkeydown = (event) => {
    if (["ArrowDown", "ArrowUp"].includes(event.key)) {
      event.preventDefault();
      open();
      [...menu.children]
        .find((option) => option.dataset.value === root.value)
        ?.focus();
    }
  };
  menu.onkeydown = (event) => {
    if (["ArrowDown", "ArrowUp"].includes(event.key)) {
      event.preventDefault();
      const options = [...menu.children],
        index = options.indexOf(document.activeElement);
      options[
        (index + (event.key === "ArrowDown" ? 1 : -1) + options.length) %
          options.length
      ]?.focus();
    }
  };
  root.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !menu.hidden) {
      event.stopPropagation();
      root.closeMenu();
      trigger.focus();
    }
  });
  root.addEventListener("focusout", (event) => {
    if (!root.contains(event.relatedTarget)) root.closeMenu();
  });
  root.append(trigger, menu);
  draw();
  return root;
}
document.addEventListener("pointerdown", (event) => {
  for (const picker of document.querySelectorAll(".pool-environment-picker"))
    if (!picker.contains(event.target)) picker.closeMenu();
});
