// Only reveal creates a presentation barrier; ordinary events stay synchronous.
export function createRevealPresentationGate(isCurrent = () => true) {
  let waiting = 0,
    pending = [],
    flushing = false;
  const drain = () => {
    if (waiting || flushing) return;
    flushing = true;
    try {
      while (pending.length && !waiting) {
        const work = pending.shift();
        if (isCurrent()) work();
      }
    } finally {
      flushing = false;
    }
  };
  return {
    dispatch(eventName, work) {
      if (!isCurrent()) return;
      if (eventName === "CardRevealed") {
        const completion = work();
        if (completion && typeof completion.then === "function") {
          waiting++;
          Promise.resolve(completion)
            .catch((error) => console.error("公开动画未完成", error))
            .finally(() => {
              waiting--;
              drain();
            });
        }
        return;
      }
      if (
        waiting &&
        ![
          "ChatMessage",
          "ChatHistory",
          "PlayerConnectionStatusUpdated",
          "ReceiveError",
        ].includes(eventName)
      )
        pending.push(work);
      else work();
    },
    clear() {
      pending = [];
    },
  };
}
