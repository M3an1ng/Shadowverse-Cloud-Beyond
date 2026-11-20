import "./styles/reveal-animation.css";

export function revealGeometry(bounds, origin, angle = 0, aspect = 0) {
  const factor = bounds.width / (bounds.layoutWidth || bounds.width) || 1;
  const width = Math.min(
    240,
    (bounds.width / factor) * 0.35,
    ((bounds.height / factor) * 0.52) / 1.4,
  );
  const height = width * 1.4;
  const x = (origin.left + origin.width / 2 - bounds.left) / factor;
  const y = (origin.top + origin.height / 2 - bounds.top) / factor;
  // The hand fans its cards. Recover the unrotated size from their screen bounds.
  const cos = Math.abs(Math.cos(angle));
  const sin = Math.abs(Math.sin(angle));
  return {
    x,
    y,
    width,
    height,
    dx: bounds.width / factor / 2 - x,
    dy: bounds.height / factor / 2 - y,
    angle,
    scale: origin.width / factor / width / (aspect ? cos + sin / aspect : 1),
    scaleY: origin.height / factor / height / (aspect ? cos + sin * aspect : 1),
  };
}

function positionFlight(flight, container, { element, origin }) {
  const bounds = container.getBoundingClientRect();
  const transform = element && getComputedStyle(element).transform;
  const matrix = transform && transform !== "none"
    ? new DOMMatrixReadOnly(transform)
    : null;
  const angle = matrix ? Math.atan2(matrix.b, matrix.a) : 0;
  const aspect = element?.offsetHeight ? element.offsetWidth / element.offsetHeight : 0;
  const geometry = revealGeometry(
    {
      left: bounds.left, top: bounds.top, width: bounds.width, height: bounds.height,
      layoutWidth: container.offsetWidth,
    },
    origin,
    angle,
    aspect,
  );
  const style = flight.style;
  style.left = geometry.x + "px";
  style.top = geometry.y + "px";
  style.width = geometry.width + "px";
  style.height = geometry.height + "px";
  style.setProperty("--reveal-dx", geometry.dx + "px");
  style.setProperty("--reveal-dy", geometry.dy + "px");
  style.setProperty("--reveal-angle", geometry.angle + "rad");
  style.setProperty("--reveal-scale-x", geometry.scale);
  style.setProperty("--reveal-scale-y", geometry.scaleY);
  style.setProperty("--reveal-unit", geometry.width / 120 + "px");
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
  const pendingAnimations = new Set();
  let completions = [];
  const complete = () => {
    const batch = completions;
    completions = [];
    for (const resolve of batch) resolve();
  };
  const hiddenSources = new Map();
  const returned = new Map();
  function waitForAnimation(element) {
    const animation = element.getAnimations()[0];
    if (!animation) return;
    return new Promise((resolve) => {
      const finish = () => {
        clearTimeout(timer);
        pendingAnimations.delete(finish);
        resolve();
      };
      // Background tabs may stop advancing the document timeline.
      const timer = setTimeout(finish, animation.effect.getComputedTiming().endTime + 160);
      pendingAnimations.add(finish);
      animation.finished.then(finish, finish);
    });
  }
  function hideSource(element) {
    if (!element || hiddenSources.has(element)) return;
    hiddenSources.set(element, element.style.visibility);
    element.style.visibility = "hidden";
  }
  function restoreSource(element) {
    if (!hiddenSources.has(element)) return;
    element.style.visibility = hiddenSources.get(element);
    hiddenSources.delete(element);
  }
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
    for (const finish of pendingAnimations) finish();
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
  async function showCard(card, options, source, container, token) {
    const reduced = globalThis.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    const flight = document.createElement("div");
    flight.className = reduced ? "mini-reveal-flight is-static" : "mini-reveal-flight";
    flight.setAttribute("aria-label", "公开卡牌：" + (card.Name ?? card.name ?? "未知卡牌"));
    const id = card.InstanceId ?? card.instanceId ?? card.uid ?? source.element ?? flight;
    const previous = returned.get(id);
    positionFlight(flight, container, source);
    flight.append(face(card, source.hidden && !previous && !reduced));
    const shade = document.createElement("div");
    shade.className = "mini-reveal-shade";
    root.append(shade, flight);
    if (previous) {
      restoreSource(previous.element);
      previous.flight.remove();
      returned.delete(id);
    }
    if (!reduced) hideSource(source.element);
    await waitForAnimation(flight);
    if (token !== generation) return;
    if (reduced) {
      flight.remove();
      shade.remove();
      return;
    }
    // A hand can reflow or replace its elements while the card is in the center.
    const target = resolveOrigin(card, options) ?? source;
    if (target.element !== source.element) {
      restoreSource(source.element);
      hideSource(target.element);
    }
    positionFlight(flight, container, target);
    flight.classList.add("is-returning");
    await waitForAnimation(flight);
    if (token !== generation) return;
    shade.remove();
    if (target.hidden) {
      flight.classList.replace("is-returning", "mini-reveal-returned");
      returned.set(id, { flight, element: target.element });
      return;
    }
    // Keep the real face visible underneath throughout the landing fade.
    restoreSource(target.element);
    flight.classList.replace("is-returning", "is-landing");
    await waitForAnimation(flight);
    flight.remove();
  }
  async function run() {
    if (running) return;
    running = true;
    const token = generation;
    try {
      while (queue.length && token === generation && isActive()) {
        const { card, options } = queue.shift();
        const container = getContainer();
        if (!container) break;
        const source = resolveOrigin(card, options);
        if (!source) continue;
        if (!root) {
          root = document.createElement("div");
          root.id = "reveal-overlay-root";
          root.className = "mini-reveal-root";
          root.setAttribute("role", "status");
          container.append(root);
        }
        await showCard(card, options, source, container, token);
        if (token !== generation) return;
        if (!queue.length && returned.size) {
          root.classList.add("turning-back");
          await Promise.all(Array.from(returned.values(), ({ flight }) =>
            waitForAnimation(flight.firstElementChild),
          ));
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
      const done = new Promise((resolve) => completions.push(resolve));
      queue.push({ card, options });
      void run();
      return done;
    },
  };
}
