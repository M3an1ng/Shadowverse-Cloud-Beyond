const CONSOLE_LEVELS = ['log', 'info', 'warn', 'error', 'debug'];
const MAX_ENTRIES = 500;

function formatValue(value) {
    if (value instanceof Error) return value.stack || value.name + ': ' + value.message;
    if (typeof value !== 'object' || value === null) return String(value);
    const seen = new WeakSet();
    try {
        return JSON.stringify(value, (_key, entry) => {
            if (typeof entry === 'bigint') return String(entry) + 'n';
            if (entry instanceof Error) return entry.stack || entry.message;
            if (entry && typeof entry === 'object') {
                if (seen.has(entry)) return '[循环引用]';
                seen.add(entry);
            }
            return entry;
        }, 2);
    } catch (error) {
        return '[日志对象无法展开：' + error.message + ']';
    }
}

function formatArguments(args) {
    if (typeof args[0] !== 'string') return args.map(formatValue).join(' ');
    let index = 1;
    const message = args[0].replace(/%[%sdifoOc]/g, token => {
        if (token === '%%') return '%';
        if (index >= args.length) return token;
        const value = args[index++];
        if (token === '%c') return '';
        if (token === '%d' || token === '%i') return String(parseInt(value, 10));
        if (token === '%f') return String(parseFloat(value));
        return formatValue(value);
    });
    return [message, ...args.slice(index).map(formatValue)].join(' ');
}

export function captureFrontendConsole(nativeConsole, browserEvents) {
    const entries = [];
    const listeners = new Set();
    const originals = new Map();
    const wrappers = new Map();
    let nextId = 0;

    function append(level, args) {
        let message;
        try {
            message = formatArguments(args);
        } catch (error) {
            message = '[前端日志无法格式化：' + error.message + ']';
        }
        const entry = { id: ++nextId, at: Date.now(), level, message };
        entries.push(entry);
        if (entries.length > MAX_ENTRIES) entries.shift();
        for (const listener of listeners) listener({ type: 'append', entry });
    }

    for (const level of CONSOLE_LEVELS) {
        const original = nativeConsole[level];
        const wrapped = (...args) => {
            // 原始方法先执行，接入面板不能改变浏览器控制台的输出与返回值。
            const output = Reflect.apply(original, nativeConsole, args);
            append(level, args);
            return output;
        };
        originals.set(level, original);
        wrappers.set(level, wrapped);
        nativeConsole[level] = wrapped;
    }

    const onError = event => append('error', ['[未捕获异常]', event.error || event.message]);
    const onRejection = event => append('error', ['[未处理的 Promise 拒绝]', event.reason]);
    browserEvents.addEventListener('error', onError);
    browserEvents.addEventListener('unhandledrejection', onRejection);

    return {
        getEntries: () => entries.slice(),
        subscribe(listener) {
            listeners.add(listener);
            return () => listeners.delete(listener);
        },
        clear() {
            entries.length = 0;
            for (const listener of listeners) listener({ type: 'clear' });
        },
        dispose() {
            for (const level of CONSOLE_LEVELS) {
                if (nativeConsole[level] === wrappers.get(level)) nativeConsole[level] = originals.get(level);
            }
            browserEvents.removeEventListener('error', onError);
            browserEvents.removeEventListener('unhandledrejection', onRejection);
            listeners.clear();
        },
    };
}
