let mockConnection;
let mockBootstrapLoads;

jest.mock('../src/main.js', () => {
  mockBootstrapLoads += 1;
  return { get connection() { return mockConnection; } };
});
jest.mock('../src/gameManager.js', () => ({
  gameState: { gameId: 'match-a', localPlayerId: 'player-a', players: [] },
}));
jest.mock('../src/userSession.js', () => ({
  getUserSession: () => ({ role: 'admin', permissionLevel: 100 }),
}));

function createConnection() {
  return {
    state: 'Connected',
    on: jest.fn(),
    off: jest.fn(),
    invoke: jest.fn().mockResolvedValue(undefined),
  };
}

describe('开发控制台的实时连接契约', () => {
  let initDevConsole;
  const cardUid = '11111111-1111-4111-8111-111111111111';
  const targetUid = '22222222-2222-4222-8222-222222222222';

  beforeEach(() => {
    jest.resetModules();
    jest.useFakeTimers();
    mockBootstrapLoads = 0;
    mockConnection = createConnection();
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ enabled: false }),
    });
    ({ initDevConsole } = require('../src/devConsole.js'));
  });

  afterEach(async () => {
    await Promise.resolve();
    await Promise.resolve();
    jest.clearAllTimers();
    jest.useRealTimers();
    delete global.fetch;
    delete window.devConsole;
  });

  test('禁用时不创建控制台、不读取连接', () => {
    const readConnection = jest.fn(() => mockConnection);
    expect(initDevConsole(false, false, readConnection)).toBeNull();
    expect(readConnection).not.toHaveBeenCalled();
    expect(document.getElementById('dev-console')).toBeNull();
  });

  test.each([
    ['playCard', [cardUid, targetUid], 'PlayCard', { CardUid: cardUid, TargetUid: targetUid }],
    ['attack', [cardUid, targetUid], 'PerformAttack', { AttackerUid: cardUid, TargetUid: targetUid }],
    ['evolve', [cardUid], 'PerformEvolve', { UnitUid: cardUid }],
  ])('%s 保留已有 Hub 方法及请求字段', async (method, args, hubMethod, payload) => {
    const console = initDevConsole(true, false, () => mockConnection);
    await console[method](args);
    expect(mockConnection.invoke).toHaveBeenCalledWith(hubMethod, payload);
  });

  test('结束回合保留请求 ID，断线时不发送', async () => {
    const console = initDevConsole(true, false, () => mockConnection);
    await console.endTurn();
    expect(mockConnection.invoke).toHaveBeenCalledWith('EndTurn', expect.any(String));
    mockConnection.invoke.mockClear();
    mockConnection.state = 'Disconnected';
    await console.endTurn();
    expect(mockConnection.invoke).not.toHaveBeenCalled();
  });

  test('初始化后才就绪的连接仍能注册监听', () => {
    mockConnection = null;
    const console = initDevConsole(true, false, () => mockConnection);
    mockConnection = createConnection();
    jest.advanceTimersByTime(1000);
    expect(console.traceListenerConnection).toBe(mockConnection);
    expect(mockConnection.on).toHaveBeenCalledWith('ReceiveGameState', expect.any(Function));
  });

  test('连接替换后移除旧监听并绑定新连接，重复检查不重复注册', () => {
    const console = initDevConsole(true, false, () => mockConnection);
    const previous = mockConnection;
    const oldHandlers = new Map(previous.on.mock.calls);
    mockConnection = createConnection();
    console.attachConditionTraceListener();
    console.attachGameStateListener();
    console.attachConditionTraceListener();
    console.attachGameStateListener();
    for (const name of ['ConditionTraceFailed', 'ConditionTraceSucceeded', 'ReceiveGameState']) {
      expect(previous.off).toHaveBeenCalledWith(name, oldHandlers.get(name));
    }
    expect(mockConnection.on).toHaveBeenCalledTimes(3);
  });

  test('加载控制台不加载应用启动入口', () => {
    expect(mockBootstrapLoads).toBe(0);
  });
});
