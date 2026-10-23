import { captureFrontendConsole } from '../src/frontendConsoleCapture.js';
import { DevConsole } from '../src/devConsole.js';

let capture;
let native;
let events;
let view;

beforeEach(() => {
  native = Object.fromEntries(['log', 'info', 'warn', 'error', 'debug'].map(level =>
    [level, jest.fn(function () { return this === native ? 'native-return' : 'wrong-receiver'; })]
  ));
  events = new EventTarget();
  capture = captureFrontendConsole(native, events);
});
afterEach(() => {
  if (view) view.dispose();
  else capture.dispose();
  view = null;
});

test.each(['log', 'info', 'warn', 'error', 'debug'])('%s 记录真实参数，保留原始接收者与返回值', level => {
  const object = { count: 2 };
  expect(native[level]('sample', object)).toBe('native-return');
  expect(capture.getEntries()[0]).toEqual(expect.objectContaining({
    level, message: expect.stringContaining('"count": 2'),
  }));
});

test('参数格式、对象快照、BigInt、循环引用与 Error 堆栈可读', () => {
  const value = { count: 1, large: 4n };
  value.self = value;
  native.info('name=%s count=%d %%', 'sample', 3);
  native.log(value);
  value.count = 99;
  native.error(new Error('broken'));
  const entries = capture.getEntries();
  expect(entries[0].message).toBe('name=sample count=3 %');
  expect(entries[1].message).toContain('"count": 1');
  expect(entries[1].message).toContain('4n');
  expect(entries[1].message).toContain('循环引用');
  expect(entries[2].message).toContain('Error: broken');
});

test('不能序列化的对象不破坏原始 console 调用，并明确记录原因', () => {
  const value = { toJSON() { throw new Error('cannot inspect'); } };
  expect(native.log(value)).toBe('native-return');
  expect(capture.getEntries()[0].message).toContain('cannot inspect');
});

test('仅保留最近 500 条，清空会通知界面，取消订阅后不再通知', () => {
  const listener = jest.fn();
  const unsubscribe = capture.subscribe(listener);
  for (let i = 0; i < 501; i++) native.log(i);
  expect(capture.getEntries()).toHaveLength(500);
  expect(capture.getEntries()[0].message).toBe('1');
  capture.clear();
  expect(capture.getEntries()).toEqual([]);
  expect(listener).toHaveBeenLastCalledWith({ type: 'clear' });
  unsubscribe();
  listener.mockClear();
  native.log('after unsubscribe');
  expect(listener).not.toHaveBeenCalled();
});

test('未捕获异常与 Promise 拒绝被记录，不取消浏览器原有错误事件', () => {
  const error = new ErrorEvent('error', { error: new Error('uncaught'), cancelable: true });
  events.dispatchEvent(error);
  const rejection = new Event('unhandledrejection', { cancelable: true });
  Object.defineProperty(rejection, 'reason', { value: new Error('rejected') });
  events.dispatchEvent(rejection);
  expect(capture.getEntries().map(entry => entry.message).join('\n')).toContain('uncaught');
  expect(capture.getEntries()[1].message).toContain('rejected');
  expect(error.defaultPrevented).toBe(false);
  expect(rejection.defaultPrevented).toBe(false);
});

test('释放恢复原始方法，不覆盖之后安装的其他 console 方法', () => {
  const replacement = jest.fn();
  native.warn = replacement;
  capture.dispose();
  expect(native.warn).toBe(replacement);
  expect(native.log('not captured')).toBe('native-return');
  expect(capture.getEntries()).toHaveLength(0);
});

test('面板可按级别和文字筛选，消息作为文本而非 HTML 呈现', () => {
  native.info('normal');
  native.error('failure <img src=x onerror=alert(1)>');
  native.warn('another failure');
  view = new DevConsole(capture);
  view.show();
  const level = document.querySelector('select');
  level.value = 'error';
  level.dispatchEvent(new Event('change'));
  const search = document.querySelector('input[type="search"]');
  search.value = 'failure';
  search.dispatchEvent(new Event('input'));
  expect(document.querySelectorAll('.frontend-console__entry')).toHaveLength(1);
  expect(document.querySelector('[role="log"]').textContent).toContain('<img');
  expect(document.querySelector('[role="log"] img')).toBeNull();
});

test('暂停只暂停显示，恢复后展示期间实际收到的日志', () => {
  view = new DevConsole(capture);
  view.show();
  const pause = document.querySelector('[data-action="pause"]');
  pause.click();
  native.warn('during pause');
  expect(document.querySelector('[role="log"]').textContent).not.toContain('during pause');
  expect(capture.getEntries()).toHaveLength(1);
  pause.click();
  expect(document.querySelector('[role="log"]').textContent).toContain('during pause');
  document.querySelector('[data-action="clear"]').click();
  expect(document.querySelector('[role="log"]').textContent).toContain('暂无前端日志');
});

test('复制仅包含筛选结果，复制失败在界面明确反馈', async () => {
  const writeText = jest.fn().mockResolvedValue(undefined);
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
  native.info('normal');
  native.error('failure');
  view = new DevConsole(capture);
  view.show();
  const level = document.querySelector('select');
  level.value = 'error';
  await view.copy();
  expect(writeText.mock.calls[0][0]).toContain('failure');
  expect(writeText.mock.calls[0][0]).not.toContain('normal');
  writeText.mockRejectedValueOnce(new Error('denied'));
  await view.copy();
  expect(document.querySelector('[role="status"]').textContent).toContain('复制失败：denied');
});

test('Esc 关闭并恢复触发按钮焦点，重复打开只保留一个面板', () => {
  const trigger = document.createElement('button');
  document.body.appendChild(trigger);
  trigger.focus();
  view = new DevConsole(capture);
  view.show();
  view.show();
  expect(document.querySelectorAll('#dev-console')).toHaveLength(1);
  document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
  expect(document.getElementById('dev-console').hidden).toBe(true);
  expect(document.activeElement).toBe(trigger);
});
