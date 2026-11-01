import { historyRequest } from './cardHistory.js';
import { getUserSession } from './userSession.js';
import { createCardStylePresentation } from './cardStylePresentation.js';
let task;
const entries = new Set(), callbacks = new WeakMap();
export function getCardStylePresenter() {
  return task ??= Promise.all([
    historyRequest('/api/card-styles'),
    getUserSession().userId ? historyRequest('/api/card-styles/preferences') : {},
  ]).then(([styles, prefs]) => createCardStylePresentation(styles, prefs)).catch(error => { task = null; throw error; });
}
// Weak references allow detached card tiles to be collected without per-tile listeners.
export function bindCardStyle(node, render) {
  callbacks.set(node, render);
  entries.add(new WeakRef(node));
  getCardStylePresenter().then(present => { if (callbacks.get(node) === render) render(present); }).catch(() => {});
}
document.addEventListener('svb:card-style-changed', () => {
  task = null;
  getCardStylePresenter().then(present => {
    for (const entry of entries) {
      const node = entry.deref();
      if (!node || !node.isConnected) { entries.delete(entry); continue; }
      callbacks.get(node)?.(present);
    }
  }).catch(() => {});
});
