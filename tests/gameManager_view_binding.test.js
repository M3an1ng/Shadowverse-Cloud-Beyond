jest.mock('../src/ui.js', () => {
  throw new Error('Game logic must not import the browser view');
});

beforeEach(() => {
  jest.resetModules();
});

test('游戏逻辑可独立加载，不初始化界面模块', () => {
  expect(() => require('../src/gameManager.js')).not.toThrow();
});

test('入口提供的视图接收原有 PP 更新，状态与 DTO 字段保持不变', () => {
  const manager = require('../src/gameManager.js');
  const updatePpDisplay = jest.fn();
  manager.bindGameView({ updatePpDisplay });
  const player = { id: 'player-1', pp: 0, maxPP: 0, hand: [], ppBoostArmed: true };
  manager.gameState.players = [player];
  manager.handlePpUpdate({ SourcePlayerId: 'player-1', CurrentPp: 3, MaxPp: 7 });
  expect(player.pp).toBe(3);
  expect(player.maxPP).toBe(7);
  expect(updatePpDisplay).toHaveBeenCalledWith('player-1', 3, 7, true);
});

test('日志仍使用同一状态和原有回合前缀，交给已连接的视图呈现', () => {
  const manager = require('../src/gameManager.js');
  const renderGameLog = jest.fn();
  manager.bindGameView({ renderGameLog });
  manager.gameState.turnNumber = 2;
  manager.appendGameLog('verification', { Announcement: '状态更新' });
  expect(manager.gameState.gameLog).toEqual(['[Turn 2] > 状态更新']);
  expect(renderGameLog).toHaveBeenCalledWith(manager.gameState.gameLog);
});

test('重新连接视图不重建状态，后续更新只发送给新视图', () => {
  const manager = require('../src/gameManager.js');
  const originalState = manager.gameState;
  const previous = { updatePpDisplay: jest.fn() };
  const current = { updatePpDisplay: jest.fn() };
  manager.bindGameView(previous);
  manager.bindGameView(current);
  manager.gameState.players = [{ id: 'player-1', hand: [] }];
  manager.handlePpUpdate({ SourcePlayerId: 'player-1', CurrentPp: 1, MaxPp: 2 });
  expect(manager.gameState).toBe(originalState);
  expect(previous.updatePpDisplay).not.toHaveBeenCalled();
  expect(current.updatePpDisplay).toHaveBeenCalledTimes(1);
});
