// Presentation gate: paces key showcase/lunge barriers (CardRevealed, AttackDeclared,
// CardPlayed, CardMoved hover-land, UnitEvolved) while flushing same-wave resolution
// events (DamageDealt, HealthChanged, UnitDestroyed, StatChange, etc.) synchronously.

const BYPASS_EVENTS = new Set([
  "ChatMessage",
  "ChatHistory",
  "PlayerConnectionStatusUpdated",
  "ReceiveError",
]);

let latestPendingDepth = 0;

export function isPresentationFastForward() {
  return latestPendingDepth >= 12;
}

export function createRevealPresentationGate(isCurrent = () => true) {
  let waiting = 0,
    pending = [],
    flushing = false;

  const syncDepth = () => {
    latestPendingDepth = pending.length;
  };

  const execute = (work) => {
    const completion = work();
    if (completion && typeof completion.then === "function") {
      waiting++;
      Promise.resolve(completion)
        .catch((error) => console.error("演出门禁等待异常", error))
        .finally(() => {
          waiting--;
          drain();
        });
    }
  };

  const drain = () => {
    if (waiting || flushing) return;
    flushing = true;
    try {
      while (pending.length && !waiting) {
        const work = pending.shift();
        syncDepth();
        if (isCurrent()) execute(work);
      }
    } finally {
      flushing = false;
      syncDepth();
    }
  };

  return {
    dispatch(eventName, work) {
      if (!isCurrent()) return;
      if (BYPASS_EVENTS.has(eventName)) {
        work();
        return;
      }
      if (eventName === "ReceiveGameState") {
        pending = [];
        syncDepth();
        execute(work);
        return;
      }
      if (waiting || pending.length > 0) {
        pending.push(work);
        syncDepth();
      } else {
        execute(work);
      }
    },
    clear() {
      pending = [];
      syncDepth();
    },
  };
}
