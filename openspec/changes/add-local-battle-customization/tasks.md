## 1. Browser Storage Foundation

- [x] 1.1 Add a dedicated IndexedDB storage module for battle customization assets and preferences.
- [x] 1.2 Define record shapes for `background` and `bgm`, including blob, mime type, file name, size, and update timestamp.
- [ ] 1.3 Add unit tests for save, load, replace, remove, and broken-record cleanup behavior.

## 2. Battle Background Customization

- [x] 2.1 Add a dedicated custom-background layer to the game page without disturbing existing battlefield layout.
- [x] 2.2 Implement local background file selection with MIME and size validation.
- [x] 2.3 Persist the selected background in IndexedDB and restore it when the game page becomes active.
- [x] 2.4 Support replacing and clearing the custom background, including object URL cleanup and fallback to default visuals.

## 3. Battle BGM Customization

- [x] 3.1 Add a lightweight local BGM controller based on a singleton `HTMLAudioElement`.
- [x] 3.2 Implement local BGM file selection with MIME and size validation.
- [x] 3.3 Persist the selected BGM in IndexedDB and restore it when the game page becomes active.
- [x] 3.4 Handle autoplay rejection gracefully by keeping the saved asset, surfacing retry state, and retrying after user interaction.
- [x] 3.5 Support replacing and clearing the custom BGM, including stop/pause logic and object URL cleanup.

## 4. Settings UI and User Feedback

- [x] 4.1 Add a minimal battle customization entry point and settings panel in the game page UI.
- [x] 4.2 Expose current asset names/status, select actions, clear actions, and non-blocking validation/error messages.
- [x] 4.3 Ensure the UI clearly communicates that customization is local-only and current-browser-only.

## 5. Verification

- [x] 5.1 Add Jest coverage for asset validation rules and autoplay failure downgrade behavior where feasible.
- [ ] 5.2 Manually verify refresh persistence, browser restart persistence, clear/reset behavior, and corrupted-record fallback.
- [ ] 5.3 Manually verify multiplayer sessions do not upload, sync, or expose local customization assets to other participants.
