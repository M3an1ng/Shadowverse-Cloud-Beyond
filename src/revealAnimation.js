import "./styles/reveal-animation.css";

export function revealGeometry(bounds, origin) {
  const factor = bounds.width / (bounds.layoutWidth || bounds.width) || 1;
  const width = Math.min(
    240,
    (bounds.width / factor) * 0.35,
    ((bounds.height / factor) * 0.52) / 1.4,
  );
  const height = width * 1.4;
  const x = (origin.left + origin.width / 2 - bounds.left) / factor;
  const y = (origin.top + origin.height / 2 - bounds.top) / factor;
  return {
    x,
    y,
    width,
    height,
    dx: bounds.width / factor / 2 - x,
    dy: bounds.height / factor / 2 - y,
    scale: origin.width / factor / width,
  };
}

// Visual-only queue: never inserts revealed cards into the authoritative hand.
export function createRevealAnimator({
  getContainer,
  isActive,
  resolveOrigin,
  resolveArt,
}) {
  let queue = [],
    running = false,
    generation = 0,
    root = null;
  const timers = new Map();
  let completions = [];
  const complete = () => {
    const batch = completions;
    completions = [];
    for (const resolve of batch) resolve();
  };
  const hiddenSources = new Map();
  const returned = new Map();
  const wait = (ms) =>
    new Promise((resolve) => {
      const id = setTimeout(() => {
        timers.delete(id);
        resolve();
      }, ms);
      timers.set(id, resolve);
    });
  const restore = () => {
    for (const [element, value] of hiddenSources)
      element.style.visibility = value;
    hiddenSources.clear();
    returned.clear();
    root?.remove();
    root = null;
  };
  function clear() {
    generation++;
    queue = [];
    running = false;
    for (const [id, resolve] of timers) {
      clearTimeout(id);
      resolve();
    }
    timers.clear();
    restore();
    complete();
  }
  function face(card, hidden) {
    const flipper = document.createElement("div");
    flipper.className = "mini-reveal-flipper" + (hidden ? " from-hidden" : "");
    const back = document.createElement("div");
    back.className = "mini-reveal-face mini-reveal-back card-back";
    const front = document.createElement("div");
    front.className = "mini-reveal-face mini-reveal-front";
    const url = resolveArt(
      card.ArtPreview ?? card.artPreview ?? card.ArtDetail ?? card.artDetail,
    );
    if (url) {
      const image = document.createElement("img");
      image.src = url;
      image.alt = "";
      front.append(image);
    }
    const cost = document.createElement("span");
    cost.className = "mini-reveal-cost";
    cost.textContent = String(card.Cost ?? card.cost ?? "?");
    front.append(cost);
    const caption = document.createElement("div");
    caption.className = "mini-reveal-caption";
    const name = document.createElement("strong");
    name.textContent = card.Name ?? card.name ?? "未知卡牌";
    const label = document.createElement("small");
    label.textContent = "公开卡牌";
    caption.append(name, label);
    if (
      String(card.Type ?? card.type)
        .toLowerCase()
        .startsWith("follower")
    ) {
      const stats = document.createElement("div");
      const attack = document.createElement("b"),
        health = document.createElement("b");
      attack.textContent =
        "⚔ " +
        (card.CurrentAttack ??
          card.currentAttack ??
          card.Attack ??
          card.attack ??
          0);
      health.textContent =
        "♥ " +
        (card.CurrentHealth ??
          card.currentHealth ??
          card.Health ??
          card.health ??
          0);
      stats.append(attack, health);
      caption.append(stats);
    }
    front.append(caption);
    flipper.append(back, front);
    return flipper;
  }
  async function run() {
    if (running) return;
    running = true;
    const token = generation;
    try {
      while (queue.length && token === generation && isActive()) {
        const entry = queue.shift(),
          container = getContainer();
        if (!container) break;
        if (!root) {
          root = document.createElement("div");
          root.id = "reveal-overlay-root";
          root.className = "mini-reveal-root";
          root.setAttribute("role", "status");
          container.append(root);
        }
        const shade = document.createElement("div");
        shade.className = "mini-reveal-shade";
        root.append(shade);
        const bounds = container.getBoundingClientRect();
        const geometry = revealGeometry(
          {
            left: bounds.left,
            top: bounds.top,
            width: bounds.width,
            height: bounds.height,
            layoutWidth: container.offsetWidth,
          },
          entry.origin,
        );
        const flight = document.createElement("div");
        flight.className = "mini-reveal-flight";
        flight.setAttribute(
          "aria-label",
          "公开卡牌：" + (entry.card.Name ?? entry.card.name ?? "未知卡牌"),
        );
        for (const [key, value] of Object.entries({
          left: geometry.x,
          top: geometry.y,
          width: geometry.width,
          height: geometry.height,
        }))
          flight.style[key] = value + "px";
        flight.style.setProperty("--reveal-dx", geometry.dx + "px");
        flight.style.setProperty("--reveal-dy", geometry.dy + "px");
        flight.style.setProperty("--reveal-start-scale", geometry.scale);
        flight.style.setProperty("--reveal-unit", geometry.width / 120 + "px");
        flight.append(face(entry.card, entry.hidden));
        root.append(flight);
        if (entry.element && !hiddenSources.has(entry.element)) {
          hiddenSources.set(entry.element, entry.element.style.visibility);
          entry.element.style.visibility = "hidden";
        }
        const reduced = globalThis.matchMedia?.(
          "(prefers-reduced-motion: reduce)",
        ).matches;
        await wait(reduced ? 700 : 1200);
        if (token !== generation) return;
        if (entry.hidden) {
          const id =
            entry.card.InstanceId ?? entry.card.instanceId ?? entry.card.uid;
          returned.get(id)?.remove();
          const slot = document.createElement("div");
          slot.className = "mini-reveal-returned";
          slot.style.cssText = `left:${geometry.x}px;top:${geometry.y}px;width:${geometry.width * geometry.scale}px;height:${geometry.height * geometry.scale}px;--reveal-unit:${(geometry.width * geometry.scale) / 120}px`;
          slot.append(face(entry.card, false));
          root.append(slot);
          returned.set(id, slot);
        }
        flight.remove();
        shade.remove();
        if (!queue.length) {
          await wait(32);
          if (token !== generation) return;
          if (queue.length) continue;
          root.classList.add("turning-back");
          await wait(240);
          if (token !== generation) return;
          restore();
        }
      }
    } finally {
      if (token === generation) {
        restore();
        running = false;
        complete();
      }
    }
  }
  return {
    clear,
    enqueue(card, options = {}) {
      if (!card || !isActive()) return;
      const source = resolveOrigin(card, options);
      if (!source) return;
      const done = new Promise((resolve) => completions.push(resolve));
      queue.push({ card, ...source });
      void run();
      return done;
    },
  };
}
