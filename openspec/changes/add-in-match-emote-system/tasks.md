## 1. Static Asset Convention

- [x] 1.1 Create the fixed emote asset directory convention under `assets/emotes/<pack-name>/<file-name>`.
- [x] 1.2 Add a minimal sample pack for development/testing, using only supported image formats.
- [x] 1.3 Verify the chosen emote asset path is served by the existing static file middleware in development and production modes.

## 2. Backend Emote Catalog

- [x] 2.1 Add emote catalog DTOs for catalog, pack, and item responses.
- [x] 2.2 Add `IEmoteCatalogService` with methods to load the catalog and resolve an emote by ID.
- [x] 2.3 Implement directory scanning for first-level pack folders, supported image extension filtering, empty folder filtering, and stable natural ordering.
- [x] 2.4 Implement strict path normalization so scanned files and requested emote IDs cannot escape the fixed emote root.
- [x] 2.5 Add short TTL metadata caching for the catalog without caching image binary content.
- [x] 2.6 Add `GET /api/emotes` to return catalog metadata only, including stable IDs and static URLs.

## 3. Backend Chat Protocol Integration

- [x] 3.1 Extend `SendChatMessageActionDto` to represent text and emote message types while preserving existing text-message compatibility.
- [x] 3.2 Extend `ChatMessageEventDto` to include `messageType`, `emoteId`, `emoteName`, and `emoteUrl` fields.
- [x] 3.3 Update chat message cloning/history storage so emote fields are preserved in live broadcasts and history sync.
- [x] 3.4 Update `GameHub.SendChatMessage` to route text messages through existing validation and emote messages through catalog validation.
- [x] 3.5 Ensure emote messages reuse existing participant context, room-closed checks, room broadcast group, and sender metadata.
- [x] 3.6 Ensure text and emote messages share the same per-participant chat rate limit.
- [x] 3.7 Return clear caller errors for missing, invalid, removed, or unauthorized emote IDs without broadcasting failed messages.

## 4. Frontend Catalog Loading

- [x] 4.1 Add `src/emoteCatalog.js` to fetch `/api/emotes`, normalize the response, and cache successful results for the page lifecycle.
- [x] 4.2 Add frontend validation helpers for empty catalogs, empty packs, item lookup by ID, and failed catalog loads.
- [x] 4.3 Ensure the frontend submits only `emoteId` for emote sends and does not trust or submit custom image URLs.

## 5. Frontend Chat State and SignalR Handling

- [x] 5.1 Add `sendEmoteMessage(emoteId)` in `src/gameManager.js`, reusing the active SignalR connection and current chat error handling pattern.
- [x] 5.2 Update chat message normalization to preserve message type and emote fields from both live `ChatMessage` and `ChatHistory` payloads.
- [x] 5.3 Ensure duplicate detection and pending client-message handling continue to work for text and emote messages.
- [x] 5.4 Ensure leaving the game route clears open emote picker UI state and any queued emote barrage state.

## 6. Chat UI Picker

- [x] 6.1 Add an emote button to the `game-chat-content` input form without losing existing text draft behavior.
- [x] 6.2 Add a collapsible emote panel inside `game-chat-content` with pack tabs and a fixed-size emote grid.
- [x] 6.3 Load the emote catalog the first time the picker opens and display loading, failure, and empty states.
- [x] 6.4 Select the first non-empty pack by default and support switching packs.
- [x] 6.5 Send an emote immediately when a user clicks an emote grid item.
- [x] 6.6 Disable or guard emote clicks while a send is in progress enough to prevent accidental duplicate sends.
- [x] 6.7 Keep text input usable when the emote catalog fails or is empty.

## 7. Chat and Barrage Rendering

- [x] 7.1 Render text messages with the existing safe text rendering path.
- [x] 7.2 Render emote messages in the chat list as fixed-size image messages using the server-provided emote URL.
- [x] 7.3 Add image alt text and a fixed-size fallback placeholder for failed emote image loads.
- [x] 7.4 Render emote messages in the existing top barrage flow without overlapping or bypassing route lifecycle cleanup.
- [x] 7.5 Ensure emote barrage cleanup runs when the user leaves the game page.

## 8. Styling and Responsive Behavior

- [x] 8.1 Add `game.css` styles for the emote button, panel, pack tabs, grid items, image messages, and error placeholders.
- [x] 8.2 Keep picker dimensions stable so opening the panel does not resize or shift chat controls unpredictably.
- [x] 8.3 Ensure touch targets for emote button, tabs, and grid items are usable on narrow/mobile viewports.
- [x] 8.4 Ensure large or animated emote images are contained with `object-fit` and do not overflow the chat panel.

## 9. Backend Tests

- [x] 9.1 Add catalog service tests for folder-to-pack grouping and stable item ordering.
- [x] 9.2 Add catalog service tests for unsupported extension filtering, empty folder filtering, and root-level file ignoring.
- [x] 9.3 Add catalog service tests for path traversal and invalid emote ID rejection.
- [ ] 9.4 Add chat hub/service tests proving valid emote messages broadcast and persist to recent history.
- [ ] 9.5 Add chat hub/service tests proving invalid or removed emote IDs are rejected without broadcast.
- [ ] 9.6 Add tests proving text and emote messages share the same chat rate limit.

## 10. Frontend Tests

- [x] 10.1 Add Jest coverage for emote catalog response normalization and failed-load handling.
- [x] 10.2 Add Jest coverage for chat message normalization of live and historical emote messages.
- [x] 10.3 Add DOM-level tests for rendering pack tabs, switching packs, and clicking an emote to send its ID.
- [x] 10.4 Add DOM-level tests for emote image load failure fallback.
- [x] 10.5 Add regression coverage that text chat still sends and renders safely after emote UI is introduced.

## 11. Manual Verification

- [ ] 11.1 Verify a player can send an emote and both players plus spectators receive it in the chat list.
- [ ] 11.2 Verify emote messages appear in recent chat history after reconnecting or joining as a spectator.
- [ ] 11.3 Verify emotes remain isolated to the current room and do not appear in another active room.
- [ ] 11.4 Verify adding/removing files under `assets/emotes` is reflected after the catalog cache expires.
- [ ] 11.5 Verify invalid image files, missing images, and failed image loads show stable fallbacks.
- [ ] 11.6 Verify the emote picker remains usable at desktop and narrow viewport sizes without overlapping chat controls.
