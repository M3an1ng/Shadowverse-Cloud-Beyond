export function isInsideGameContainer(target) {
    return Boolean(target?.closest?.("#game-container"));
}

export function preventNativeGameDrag(event) {
    if (!isInsideGameContainer(event?.target)) {
        return;
    }

    event.preventDefault();
}
