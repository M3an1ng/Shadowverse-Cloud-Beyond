const defaultClasses = {
    overlay: 'wizard-loading-overlay',
    card: 'wizard-loading-card',
    spinner: 'wizard-loading-spinner',
    text: 'wizard-loading-text',
    progress: 'wizard-loading-progress'
};

const loaderMap = new WeakMap();

class BlockingLoader {
    constructor(host, options = {}) {
        this.host = host;
        this.options = {
            classes: { ...defaultClasses, ...(options.classes || {}) },
            ensureRelative: options.ensureRelative !== false
        };
        this.overlay = null;
        this.timer = null;
        this.percent = 0;
        this.originalPosition = null;
    }

    ensureHostPosition() {
        if (!this.options.ensureRelative || !this.host) return;
        const computed = window.getComputedStyle(this.host);
        if (computed.position === 'static') {
            if (this.originalPosition === null) {
                this.originalPosition = this.host.style.position || '';
            }
            this.host.style.position = 'relative';
        }
    }

    getOrCreateOverlay() {
        if (!this.host) return null;
        this.ensureHostPosition();
        if (this.overlay) return this.overlay;

        const overlay = document.createElement('div');
        overlay.className = this.options.classes.overlay;
        overlay.innerHTML = `
            <div class="${this.options.classes.card}">
                <div class="${this.options.classes.spinner}"></div>
                <div class="${this.options.classes.text}"></div>
                <div class="${this.options.classes.progress}">0%</div>
            </div>
        `;
        this.host.appendChild(overlay);
        this.overlay = overlay;
        return overlay;
    }

    show(message = '正在处理...') {
        const overlay = this.getOrCreateOverlay();
        if (!overlay) return;

        const textEl = overlay.querySelector(`.${this.options.classes.text}`);
        if (textEl) {
            textEl.textContent = message;
        }

        const progressEl = overlay.querySelector(`.${this.options.classes.progress}`);
        this.percent = 0;
        if (progressEl) {
            progressEl.textContent = '0%';
        }

        overlay.style.display = 'flex';

        if (this.timer) {
            clearInterval(this.timer);
        }

        this.timer = setInterval(() => {
            const increment = Math.max(1, Math.round((100 - this.percent) * 0.08));
            this.percent = Math.min(96, this.percent + increment);
            if (progressEl) {
                progressEl.textContent = `${this.percent}%`;
            }
        }, 120);
    }

    hide() {
        if (!this.overlay) return;

        const progressEl = this.overlay.querySelector(`.${this.options.classes.progress}`);
        if (progressEl) {
            progressEl.textContent = '100%';
        }

        if (this.timer) {
            clearInterval(this.timer);
            this.timer = null;
        }

        this.overlay.style.display = 'none';
    }

    cleanup() {
        if (this.timer) {
            clearInterval(this.timer);
            this.timer = null;
        }
        if (this.overlay) {
            this.overlay.remove();
            this.overlay = null;
        }
        if (this.host && this.originalPosition !== null) {
            this.host.style.position = this.originalPosition;
            this.originalPosition = null;
        }
    }
}

export function getBlockingLoader(host, options = {}) {
    if (!host) return null;
    let loader = loaderMap.get(host);
    if (!loader) {
        loader = new BlockingLoader(host, options);
        loaderMap.set(host, loader);
    }
    return loader;
}
