const escapeHtml = (value) =>
  String(value).replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
export function formatRoomHandshake(handshake, { copiedValue = null } = {}) {
  const wrap = (label, value) => {
    if (!value) return null;
    const copied = String(value) === copiedValue ? " copied" : "";
    return `${label}: <span class="mp-copy${copied}" role="button" tabindex="0" title="点击复制" data-copy="${escapeHtml(value)}"><strong>${escapeHtml(value)}</strong></span>`;
  };
  return [
    wrap("GameId", handshake.gameId),
    wrap("PlayerId", handshake.playerId),
    wrap("JoinCode", handshake.joinCode),
    handshake.role ? `Role: ${escapeHtml(handshake.role)}` : null,
  ]
    .filter(Boolean)
    .join("<br>");
}
export function draftRoomHandshake(room) {
  const id = String(room.id),
    gameId = /^[0-9a-f]{32}$/i.test(id)
      ? `${id.slice(0, 8)}-${id.slice(8, 12)}-${id.slice(12, 16)}-${id.slice(16, 20)}-${id.slice(20)}`
      : id;
  return {
    gameId,
    playerId: room.player.playerId,
    joinCode: room.code,
    role: room.role || "host",
  };
}
