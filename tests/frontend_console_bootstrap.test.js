jest.mock('../src/gameManager.js', () => ({ gameState: { players: [] } }));
jest.mock('../src/main.js', () => ({ connection: null }));
jest.mock('../src/userSession.js', () => ({ getUserSession: () => ({ role: 'guest' }) }));

test('页面控制台展示前端实际 console.warn，而不是只展示旧命令输出', () => {
  jest.useFakeTimers();
  window.location.hash = '#dev-tools';
  global.fetch = jest.fn().mockResolvedValue({ ok: true, json: async () => ({}) });
  const nativeWarn = jest.spyOn(console, 'warn').mockImplementation(() => {});
  const { initDevConsole } = require('../src/devConsole.js');
  const view = initDevConsole(true, false, () => null);
  try {
    view.toggle();
    console.warn('frontend-console-real-output-724');
    expect(document.getElementById('dev-console').textContent).toContain('frontend-console-real-output-724');
    expect(nativeWarn).toHaveBeenCalledWith('frontend-console-real-output-724');
  } finally {
    view.dispose?.();
    nativeWarn.mockRestore();
    jest.clearAllTimers();
    jest.useRealTimers();
    delete global.fetch;
  }
});
