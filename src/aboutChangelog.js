const CHANGELOG_TYPE_META = Object.freeze({
    feature: { key: 'feature', label: '新增', order: 0 },
    improve: { key: 'improve', label: '优化', order: 1 },
    adjust: { key: 'adjust', label: '调整', order: 2 },
    fix: { key: 'fix', label: '修复', order: 3 }
});

const CHANGELOG_TYPE_ORDER = ['feature', 'improve', 'adjust', 'fix'];

const CHANGELOG_TYPE_ALIASES = Object.freeze({
    feature: 'feature',
    feat: 'feature',
    new: 'feature',
    add: 'feature',
    added: 'feature',
    improve: 'improve',
    improvement: 'improve',
    optimize: 'improve',
    perf: 'improve',
    polish: 'improve',
    ui: 'improve',
    adjust: 'adjust',
    change: 'adjust',
    changed: 'adjust',
    balance: 'adjust',
    rule: 'adjust',
    refactor: 'adjust',
    fix: 'fix',
    bugfix: 'fix',
    bug: 'fix',
    hotfix: 'fix'
});

function normalizeVersion(value) {
    if (typeof value !== 'string') return null;
    const trimmed = value.trim();
    if (!trimmed) return null;
    return trimmed.startsWith('v') ? trimmed : `v${trimmed}`;
}

function parseVersion(value) {
    const normalized = normalizeVersion(value);
    if (!normalized) return [0, 0, 0];
    const parts = normalized
        .slice(1)
        .split('.')
        .map((part) => Number.parseInt(part, 10));
    return [parts[0] || 0, parts[1] || 0, parts[2] || 0];
}

function compareVersions(a, b) {
    const va = parseVersion(a);
    const vb = parseVersion(b);
    const maxLen = Math.max(va.length, vb.length);
    for (let i = 0; i < maxLen; i += 1) {
        const diff = (va[i] || 0) - (vb[i] || 0);
        if (diff !== 0) return diff;
    }
    return 0;
}

export function resolveChangelogType(item) {
    if (item && typeof item === 'object' && typeof item.type === 'string') {
        const mapped = CHANGELOG_TYPE_ALIASES[item.type.trim().toLowerCase()];
        if (mapped) return mapped;
    }

    const text = typeof item === 'string'
        ? item.trim()
        : String(item?.text || '').trim();

    if (!text) return 'feature';
    if (/^(尝试)?修复|失踪|错误.*的问题$|不会.*的问题$/.test(text)) return 'fix';
    if (/^(优化|美化|对于.+进行了.*优化)/.test(text)) return 'improve';
    if (/^(调整|修改|修正|重构|更改|延长)/.test(text)) return 'adjust';
    return 'feature';
}

async function fetchChangelogJson() {
    const candidates = [
        '/data/about-changelog.json',
        new URL('../data/about-changelog.json', import.meta.url).href
    ];

    let lastError = null;
    for (const url of candidates) {
        try {
            const response = await fetch(url, { cache: 'no-store' });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('text/html')) {
                throw new Error('Unexpected HTML response');
            }
            return await response.json();
        } catch (err) {
            lastError = err;
        }
    }
    throw lastError || new Error('Failed to load changelog');
}

function ensureChangelogControls(list) {
    let dateEl = document.getElementById('about-changelog-date');
    let versionSelect = document.getElementById('about-changelog-version');
    let filtersEl = document.getElementById('about-changelog-filters');

    if (versionSelect && filtersEl) {
        return { dateEl, versionSelect, filtersEl };
    }

    const block = list.closest('.about-block');
    if (!block) {
        return { dateEl: null, versionSelect: null, filtersEl: null };
    }

    block.classList.add('about-changelog-block');
    const existingTitle = block.querySelector(':scope > .about-block-title');
    if (existingTitle && !versionSelect) {
        const header = document.createElement('div');
        header.className = 'about-changelog-header';

        const heading = document.createElement('div');
        heading.className = 'about-changelog-heading';
        existingTitle.replaceWith(header);
        heading.appendChild(existingTitle);

        dateEl = document.createElement('span');
        dateEl.id = 'about-changelog-date';
        dateEl.className = 'about-changelog-date';
        dateEl.hidden = true;
        heading.appendChild(dateEl);

        versionSelect = document.createElement('select');
        versionSelect.id = 'about-changelog-version';
        versionSelect.className = 'about-changelog-version-select';
        versionSelect.setAttribute('aria-label', '选择更新版本');
        versionSelect.hidden = true;

        header.append(heading, versionSelect);
    }

    if (!filtersEl) {
        filtersEl = document.createElement('div');
        filtersEl.id = 'about-changelog-filters';
        filtersEl.className = 'about-changelog-filters';
        filtersEl.setAttribute('role', 'tablist');
        filtersEl.setAttribute('aria-label', '按更新类型筛选');
        filtersEl.hidden = true;
        block.insertBefore(filtersEl, list);
    }

    return { dateEl, versionSelect, filtersEl };
}

export async function loadAboutChangelog({ onInteract } = {}) {
    const list = document.getElementById('about-changelog');
    const versionEl = document.getElementById('about-card-version');
    if (!list) return;

    const { dateEl, versionSelect, filtersEl } = ensureChangelogControls(list);

    const renderStatusItem = (message) => {
        list.innerHTML = '';
        if (dateEl) dateEl.hidden = true;
        if (versionSelect) versionSelect.hidden = true;
        if (filtersEl) filtersEl.hidden = true;
        const li = document.createElement('li');
        li.className = 'about-changelog-empty';
        li.textContent = message;
        list.appendChild(li);
    };

    try {
        const rawItems = await fetchChangelogJson();
        const entries = (Array.isArray(rawItems) ? rawItems : [])
            .filter((item) => item && item.planned !== true)
            .map((item, index) => {
                if (typeof item === 'string') {
                    return {
                        version: 'v0.0.0',
                        date: '',
                        type: resolveChangelogType(item),
                        text: item.trim(),
                        index
                    };
                }
                return {
                    version: normalizeVersion(item.version) || 'v0.0.0',
                    date: typeof item.date === 'string' ? item.date.trim() : '',
                    type: resolveChangelogType(item),
                    text: String(item.text || '').trim(),
                    index
                };
            })
            .filter((item) => item.text.length > 0);

        if (!entries.length) {
            renderStatusItem('暂无更新日志');
            return;
        }

        const versionGroups = new Map();
        for (const entry of entries) {
            if (!versionGroups.has(entry.version)) {
                versionGroups.set(entry.version, []);
            }
            versionGroups.get(entry.version).push(entry);
        }

        const sortedVersions = Array.from(versionGroups.keys()).sort((a, b) => compareVersions(b, a));
        const latestVersion = sortedVersions[0];

        if (versionEl && latestVersion) {
            versionEl.textContent = latestVersion;
        }

        const state = {
            version: latestVersion,
            type: 'all'
        };

        const getSortedVersionEntries = (version) => {
            const group = versionGroups.get(version) || [];
            return [...group].sort((a, b) => {
                const orderDiff = (CHANGELOG_TYPE_META[a.type]?.order ?? 99) - (CHANGELOG_TYPE_META[b.type]?.order ?? 99);
                return orderDiff !== 0 ? orderDiff : a.index - b.index;
            });
        };

        const renderList = () => {
            const currentEntries = getSortedVersionEntries(state.version);
            const filtered = state.type === 'all'
                ? currentEntries
                : currentEntries.filter((entry) => entry.type === state.type);

            list.innerHTML = '';
            const fragment = document.createDocumentFragment();

            filtered.forEach((entry) => {
                const meta = CHANGELOG_TYPE_META[entry.type] || CHANGELOG_TYPE_META.feature;
                const li = document.createElement('li');
                li.className = 'about-changelog-item';
                li.dataset.type = meta.key;

                const badge = document.createElement('span');
                badge.className = `about-type-badge about-type-badge--${meta.key}`;
                badge.textContent = meta.label;

                const text = document.createElement('span');
                text.className = 'about-changelog-text';
                text.textContent = entry.text;

                li.append(badge, text);
                fragment.appendChild(li);
            });

            list.appendChild(fragment);
            list.scrollTop = 0;
        };

        const syncFilterActiveState = () => {
            if (!filtersEl) return;
            const buttons = filtersEl.querySelectorAll('.about-filter-pill');
            buttons.forEach((btn) => {
                const isActive = btn.dataset.filterType === state.type;
                btn.classList.toggle('is-active', isActive);
                btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
            });
        };

        const renderFiltersAndMeta = () => {
            const currentEntries = versionGroups.get(state.version) || [];
            const versionDate = currentEntries.find((entry) => entry.date)?.date || '';

            if (dateEl) {
                dateEl.textContent = versionDate;
                dateEl.hidden = !versionDate;
            }

            if (!filtersEl) return;

            const counts = { feature: 0, improve: 0, adjust: 0, fix: 0 };
            currentEntries.forEach((entry) => {
                counts[entry.type] = (counts[entry.type] || 0) + 1;
            });

            const availableTypes = CHANGELOG_TYPE_ORDER.filter((typeKey) => counts[typeKey] > 0);
            if (availableTypes.length <= 1) {
                filtersEl.innerHTML = '';
                filtersEl.hidden = true;
                return;
            }

            filtersEl.innerHTML = '';
            filtersEl.hidden = false;

            const createPill = (typeKey, label, count) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.role = 'tab';
                btn.dataset.filterType = typeKey;
                btn.className = `about-filter-pill about-filter-pill--${typeKey}`;

                const labelSpan = document.createElement('span');
                labelSpan.textContent = label;

                const countSpan = document.createElement('span');
                countSpan.className = 'about-filter-count';
                countSpan.textContent = String(count);

                btn.append(labelSpan, countSpan);
                btn.addEventListener('click', (event) => {
                    event.stopPropagation();
                    onInteract?.();
                    if (state.type === typeKey) return;
                    state.type = typeKey;
                    syncFilterActiveState();
                    renderList();
                });
                return btn;
            };

            filtersEl.appendChild(createPill('all', '全部', currentEntries.length));
            availableTypes.forEach((typeKey) => {
                const meta = CHANGELOG_TYPE_META[typeKey];
                filtersEl.appendChild(createPill(typeKey, meta.label, counts[typeKey]));
            });

            syncFilterActiveState();
        };

        if (versionSelect) {
            versionSelect.innerHTML = '';
            sortedVersions.forEach((ver) => {
                const option = document.createElement('option');
                option.value = ver;
                const count = versionGroups.get(ver)?.length || 0;
                option.textContent = `${ver} (${count})`;
                versionSelect.appendChild(option);
            });
            versionSelect.value = state.version;
            versionSelect.hidden = sortedVersions.length <= 1;

            versionSelect.onchange = () => {
                onInteract?.();
                state.version = versionSelect.value;
                state.type = 'all';
                renderFiltersAndMeta();
                renderList();
            };
            versionSelect.onmousedown = () => {
                onInteract?.();
            };
        }

        renderFiltersAndMeta();
        renderList();
    } catch (err) {
        console.warn('MAIN: Failed to load about changelog:', err);
        renderStatusItem('更新日志加载失败');
    }
}
