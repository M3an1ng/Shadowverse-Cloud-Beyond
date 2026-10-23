import { captureFrontendConsole } from './frontendConsoleCapture.js';

const LEVEL_LABELS = { log: '日志', info: '信息', debug: '调试', warn: '警告', error: '错误' };

export class DevConsole {
    constructor(capture) {
        this.capture = capture;
        this.paused = false;
        this.root = null;
        this.unsubscribe = capture.subscribe(() => {
            if (this.root && !this.root.hidden && !this.paused) this.render();
        });
        this.onKeydown = event => {
            if (event.ctrlKey && event.key === String.fromCharCode(96)) {
                event.preventDefault();
                this.toggle();
            } else if (this.root && !this.root.hidden && event.key === 'Escape') {
                event.preventDefault();
                this.hide();
            }
        };
        document.addEventListener('keydown', this.onKeydown);
    }

    mount() {
        if (this.root) return;
        this.root = document.createElement('section');
        this.root.id = 'dev-console';
        this.root.className = 'frontend-console';
        this.root.hidden = true;
        this.root.setAttribute('role', 'dialog');
        this.root.setAttribute('aria-modal', 'false');
        this.root.setAttribute('aria-label', '前端控制台');
        this.root.innerHTML = `
            <header class="frontend-console__header">
                <div><h2>前端控制台</h2><p>浏览器实时日志 · 保留最近 500 条 · Ctrl + 反引号 开关</p></div>
                <button type="button" data-action="close" aria-label="关闭前端控制台">关闭 <kbd>Esc</kbd></button>
            </header>
            <div class="frontend-console__toolbar">
                <label>级别<select aria-label="日志级别">
                    <option value="">全部级别</option>
                    <option value="error">错误</option><option value="warn">警告</option>
                    <option value="info">信息</option><option value="log">日志</option><option value="debug">调试</option>
                </select></label>
                <label class="frontend-console__search">搜索<input type="search" placeholder="搜索消息、对象或错误堆栈" aria-label="搜索日志"></label>
                <button type="button" data-action="pause" aria-pressed="false">暂停显示</button>
                <button type="button" data-action="copy">复制筛选结果</button>
                <button type="button" data-action="clear">清空记录</button>
                <label class="frontend-console__follow"><input type="checkbox" checked>自动跟随</label>
            </div>
            <div class="frontend-console__list" role="log" aria-label="前端日志" tabindex="0"></div>
            <footer class="frontend-console__footer"><span data-status role="status"></span><span>日志仅保留在本页内存，不上传服务器</span></footer>
        `;
        document.body.appendChild(this.root);
        this.list = this.root.querySelector('[role="log"]');
        this.level = this.root.querySelector('select');
        this.search = this.root.querySelector('input[type="search"]');
        this.follow = this.root.querySelector('input[type="checkbox"]');
        this.status = this.root.querySelector('[data-status]');
        this.root.querySelector('[data-action="close"]').addEventListener('click', () => this.hide());
        this.level.addEventListener('change', () => this.render());
        this.search.addEventListener('input', () => this.render());
        this.root.querySelector('[data-action="pause"]').addEventListener('click', event => {
            this.paused = !this.paused;
            event.currentTarget.textContent = this.paused ? '继续显示' : '暂停显示';
            event.currentTarget.setAttribute('aria-pressed', String(this.paused));
            this.status.textContent = this.paused ? '已暂停显示，日志仍在记录' : '';
            if (!this.paused) this.render();
        });
        this.root.querySelector('[data-action="clear"]').addEventListener('click', () => {
            this.capture.clear();
            this.render();
        });
        this.root.querySelector('[data-action="copy"]').addEventListener('click', () => { void this.copy(); });
    }

    filteredEntries() {
        const search = this.search.value.trim().toLowerCase();
        return this.capture.getEntries().filter(entry =>
            (!this.level.value || entry.level === this.level.value) &&
            (!search || entry.message.toLowerCase().includes(search))
        );
    }

    render() {
        const scrollTop = this.list.scrollTop;
        const entries = this.filteredEntries();
        const fragment = document.createDocumentFragment();
        for (const entry of entries) {
            const row = document.createElement('article');
            row.className = 'frontend-console__entry';
            row.dataset.level = entry.level;
            const time = document.createElement('time');
            time.dateTime = new Date(entry.at).toISOString();
            time.textContent = new Date(entry.at).toLocaleTimeString('zh-CN', { hour12: false });
            const level = document.createElement('span');
            level.className = 'frontend-console__level';
            level.textContent = LEVEL_LABELS[entry.level];
            const message = document.createElement('pre');
            message.textContent = entry.message;
            row.append(time, level, message);
            fragment.appendChild(row);
        }
        if (!entries.length) {
            const empty = document.createElement('p');
            empty.className = 'frontend-console__empty';
            empty.textContent = this.capture.getEntries().length ? '没有匹配的日志，请调整筛选条件。' : '暂无前端日志。操作页面后，日志会显示在这里。';
            fragment.appendChild(empty);
        }
        this.list.replaceChildren(fragment);
        this.status.textContent = entries.length + ' 条显示 / ' + this.capture.getEntries().length + ' 条已记录' + (this.paused ? ' · 已暂停显示' : '');
        this.list.scrollTop = this.follow.checked ? this.list.scrollHeight : scrollTop;
    }

    async copy() {
        const text = this.filteredEntries().map(entry =>
            new Date(entry.at).toISOString() + ' [' + entry.level + '] ' + entry.message
        ).join('\n');
        try {
            await navigator.clipboard.writeText(text);
            this.status.textContent = '已复制筛选结果';
        } catch (error) {
            this.status.textContent = '复制失败：' + error.message;
        }
    }

    show() {
        this.mount();
        if (!this.root.hidden) {
            this.search.focus();
            return;
        }
        this.previousFocus = document.activeElement;
        this.root.hidden = false;
        this.render();
        this.search.focus();
    }

    hide() {
        if (!this.root) return;
        this.root.hidden = true;
        this.previousFocus?.focus();
    }

    toggle() {
        if (!this.root || this.root.hidden) this.show();
        else this.hide();
    }

    dispose() {
        this.unsubscribe();
        this.capture.dispose();
        document.removeEventListener('keydown', this.onKeydown);
        this.root?.remove();
        if (window.devConsole === this) {
            delete window.devConsole;
            devConsole = null;
        }
    }
}

export let devConsole = null;

export function initDevConsole() {
    if (!devConsole) {
        devConsole = new DevConsole(captureFrontendConsole(console, window));
        window.devConsole = devConsole;
    }
    return devConsole;
}
