export function createGuestJoinSoundTracker(options = {}) {
    const playGuestJoined = typeof options.playGuestJoined === 'function'
        ? options.playGuestJoined
        : () => {};

    let guestWasPresent = false;

    function observe(room) {
        const guestIsPresent = Boolean(room?.guestPlayerId);
        if (guestIsPresent && !guestWasPresent) {
            playGuestJoined(room);
        }
        guestWasPresent = guestIsPresent;
    }

    return {
        observe
    };
}
