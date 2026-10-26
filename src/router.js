// src/router.js - 简单的前端路由系统
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
//


/**
 * 简单的单页应用路由管理器
 * 支持页面切换和状态管理
 */
class Router {
    constructor() {
        this.currentRoute = 'navigation';
        this.failedRoute = null;
        this.routes = new Map();
        this.beforeRouteChange = null;
        this.afterRouteChange = null;
        this.pendingNavigation = Promise.resolve();

        // 初始化路由配置
        this.initializeRoutes();

        // 绑定事件监听器
        this.bindEventListeners();
    }

    /**
     * 初始化路由配置
     */
    initializeRoutes() {
        this.routes.set('navigation', {
            element: document.getElementById('navigation-page'),
            title: '影之诗 星云',
            init: null,
            cleanup: null
        });

        this.routes.set('game', {
            element: document.getElementById('game-page'),
            title: '游戏对战 - 影之诗 星云',
            init: null,
            cleanup: null
        });

        this.routes.set('singleplayer', {
            element: document.getElementById('navigation-page'),
            title: '单人模式 - 影之诗 星云',
            init: this.initSingleplayer.bind(this),
            cleanup: this.cleanupSingleplayer.bind(this)
        });

        this.routes.set('card-editor', {
            element: document.getElementById('card-editor-page'),
            title: '卡牌工坊 - 影之诗 星云',
            init: this.initCardEditorPage.bind(this),
            cleanup: this.cleanupCardEditorPage.bind(this)
        });

        this.routes.set('deck-builder', {
            element: document.getElementById('deck-builder-page'),
            title: '牌组编辑 - 影之诗 星云',
            init: this.initDeckBuilderPage.bind(this),
            cleanup: null
        });

        this.routes.set('multiplayer', {
            element: document.getElementById('navigation-page'), // reuse navigation container for now
            title: '多人对战 - 影之诗 星云',
            init: this.initMultiplayer.bind(this),
            cleanup: null
        });

        this.routes.set('dev-tools', {
            element: document.getElementById('dev-tools-page'),
            title: '系统设置 - 影之诗 星云',
            init: this.initDevToolsPage.bind(this),
            cleanup: this.cleanupDevToolsPage.bind(this)
        });

        this.routes.set('dev-tools/match-lab', {
            element: document.getElementById('dev-tools-match-lab-page'),
            title: '对战实验室 - 影之诗 星云',
            init: this.initDevToolsMatchLabPage.bind(this),
            cleanup: this.cleanupDevToolsMatchLabPage.bind(this)
        });

        this.routes.set('dsl-editor-v2', {
            element: document.getElementById('dsl-editor-v2-page'),
            title: 'DSL Editor V2 - 影之诗 星云',
            init: this.initDslEditorV2Page.bind(this),
            cleanup: null
        });

    }

    /**
     * 绑定事件监听器
     */
    bindEventListeners() {
        const navigateSafely = (route, pushState = true, onBlocked = null, routeButton = null) => {
            this.navigateTo(route, pushState, routeButton).then((navigated) => {
                if (navigated === false && typeof onBlocked === 'function') {
                    onBlocked();
                }
            }).catch((error) => {
                if (onBlocked) onBlocked();
                console.error(`Router: Navigation to "${route}" failed:`, error);
            });
        };

        // 监听导航按钮点击
        document.addEventListener('click', async (event) => {
            const routeButton = event.target.closest('[data-route]');
            if (routeButton) {
                event.preventDefault();
                const route = routeButton.dataset.route;
                navigateSafely(route, true, null, routeButton);
            }
        });

        // 监听浏览器前进后退
        window.addEventListener('popstate', async (event) => {
            const route = event.state?.route || 'navigation';
            if (route === this.currentRoute) {
                return;
            }
            navigateSafely(route, false, () => {
                window.history.pushState({ route: this.currentRoute }, '', `#${this.currentRoute}`);
            }, { classList: { contains: className => className === 'back-nav-btn' } }); // 不推送历史记录
        });
    }

    /**
     * 导航到指定路由
     * @param {string} route - 路由名称
     * @param {boolean} pushState - 是否推送到浏览器历史记录
     */
    async navigateTo(route, pushState = true, routeButton = null) {
        const previousNavigation = this.pendingNavigation;
        let releaseNavigation;
        this.pendingNavigation = new Promise(resolve => { releaseNavigation = resolve; });
        let oldRoute;
        try {
            // 页面初始化、清理和连接释放属于同一次切换；后续切换必须等待。
            await previousNavigation;
            if (!this.routes.has(route)) {
                console.error(`Router: Unknown route "${route}"`);
                return false;
            }
            if (route === this.currentRoute && this.failedRoute !== route) return true;
            oldRoute = this.currentRoute;
            if (this.beforeRouteChange && !await this.beforeRouteChange(oldRoute, route, routeButton)) {
                return false;
            }
            const currentRouteConfig = this.routes.get(oldRoute);
            if (oldRoute !== route && currentRouteConfig?.beforeLeave && !await currentRouteConfig.beforeLeave()) {
                return false;
            }
            const newRouteConfig = this.routes.get(route);
            try {
                if (currentRouteConfig?.cleanup) await currentRouteConfig.cleanup();
            } catch (error) {
                this.showRouteFailure(oldRoute, error, route);
                throw error;
            }
            currentRouteConfig?.element?.querySelector('[data-route-failure]')?.remove();
            currentRouteConfig?.element?.classList.remove('active');
            newRouteConfig.element?.classList.add('active');
            if (newRouteConfig.title) document.title = newRouteConfig.title;
            try {
                if (newRouteConfig.init) await newRouteConfig.init();
            } catch (error) {
                let failure = error;
                try {
                    if (newRouteConfig.cleanup) await newRouteConfig.cleanup();
                } catch (cleanupError) {
                    failure = new AggregateError([error, cleanupError], '页面初始化和清理均失败。');
                }
                this.showRouteFailure(route, failure, route);
                if (pushState) window.history.pushState({ route }, '', `#${route}`);
                throw failure;
            }
            this.currentRoute = route;
            this.failedRoute = null;
            if (pushState) window.history.pushState({ route }, '', `#${route}`);
        } finally {
            releaseNavigation();
        }
        // 后置钩子允许“返回对战”发起下一次导航，不能占住切换锁。
        if (this.afterRouteChange) await this.afterRouteChange(oldRoute, route);
        return true;
    }

    showRouteFailure(route, error, retryRoute) {
        const config = this.routes.get(route);
        this.currentRoute = route;
        this.failedRoute = route;
        config.element?.querySelector('[data-route-failure]')?.remove();
        const panel = document.createElement('section');
        panel.className = 'error-message';
        panel.dataset.routeFailure = route;
        panel.setAttribute('role', 'alert');
        const message = document.createElement('p');
        message.textContent = `页面切换失败：${error.message}`;
        const retry = document.createElement('button');
        retry.type = 'button';
        retry.dataset.route = retryRoute;
        retry.textContent = '重试';
        panel.append(message, retry);
        config.element?.prepend(panel);
        config.element?.classList.add('active');
        document.title = `${config.title || document.title}（加载失败）`;
    }

    setRouteLifecycle(route, lifecycle) {
        Object.assign(this.routes.get(route), lifecycle);
    }

    /**
     * 初始化卡牌编辑器页面
     */
    async initCardEditorPage() {

        try {
            // 动态导入卡牌编辑器模块
            const editorModule = await import('./cardEditor.js');
            await editorModule.initializeCardEditor();
        } catch (error) {
            console.error('Router: Failed to initialize card editor:', error);

            // 显示错误信息
            const container = document.getElementById('card-editor-container');
            if (container) {
                container.innerHTML = `
                    <div class="error-message">
                        <h3>加载失败</h3>
                        <p>无法加载卡牌编辑器: ${error.message}</p>
                        <button onclick="location.reload()">重新加载</button>
                    </div>
                `;
            }
            throw error;
        }
    }

    async cleanupCardEditorPage() {
        try {
            const editorModule = await import('./cardEditor.js');
            if (typeof editorModule.cleanupCardEditor === 'function') {
                editorModule.cleanupCardEditor();
            }
        } catch (error) {
            console.error('Router: Failed to cleanup card editor:', error);
            throw error;
        }
    }


    /**
     * 初始化牌组编辑器页面
     */
    async initDeckBuilderPage() {

        try {
            // 动态导入牌组编辑器模块
            const deckBuilderModule = await import('./deckBuilder.js');
            this.setRouteLifecycle('deck-builder', { beforeLeave: deckBuilderModule.beforeLeaveDeckBuilder });
            await deckBuilderModule.initializeDeckBuilder({
                getCurrentRoute: () => this.getCurrentRoute()
            });
        } catch (error) {
            console.error('Router: Failed to initialize deck builder:', error);

            // 显示错误信息
            const container = document.getElementById('deck-builder-container');
            if (container) {
                container.innerHTML = `
                    <div class="error-message">
                        <h3>加载失败</h3>
                        <p>无法加载牌组编辑器: ${error.message}</p>
                        <button onclick="location.reload()">重新加载</button>
                    </div>
                `;
            }
            throw error;
        }
    }

    /**
     * 初始化多人对战（展示多人面板）
     */
    async initMultiplayer() {
        document.body.classList.add('show-multiplayer');
        const panel = document.getElementById('multiplayer-panel');
        if (panel) {
            panel.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    /**
     * 初始化单人对战（展示单人面板）
     */
    async initSingleplayer() {
        document.body.classList.add('show-singleplayer');
        const panel = document.getElementById('singleplayer-panel');
        if (panel) {
            panel.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    async cleanupSingleplayer() {
        document.body.classList.remove('show-singleplayer');
    }

    /**
     * 初始化开发者工具页面
     */
    async initDevToolsPage() {

        try {
            const [logModule, userModule, winrateModule, copyLimitModule, cardSetEnvModule] = await Promise.all([
                import('./devToolsLogViewer.js'),
                import('./devToolsUserManagement.js'),
                import('./devToolsWinrateStats.js'),
                import('./devToolsCopyLimitPolicies.js'),
                import('./devToolsCardSetEnvironmentMembership.js')
            ]);
            const outcomes = await Promise.allSettled([
                logModule.initializeDevToolsLogViewer(),
                userModule.initializeDevToolsUserManagement(),
                winrateModule.initializeDevToolsWinrateStats(),
                copyLimitModule.initializeDevToolsCopyLimitPolicies(),
                cardSetEnvModule.initializeDevToolsCardSetEnvironmentMembership()
            ]);
            const failures = outcomes.filter(outcome => outcome.status === 'rejected').map(outcome => outcome.reason);
            if (failures.length) throw new AggregateError(failures, '开发工具生命周期未完成。');
        } catch (error) {
            console.error('Router: Failed to initialize dev tools:', error);
            throw error;
        }
    }

    async cleanupDevToolsPage() {
        try {
            const [logModule, userModule, winrateModule, copyLimitModule, cardSetEnvModule] = await Promise.all([
                import('./devToolsLogViewer.js'),
                import('./devToolsUserManagement.js'),
                import('./devToolsWinrateStats.js'),
                import('./devToolsCopyLimitPolicies.js'),
                import('./devToolsCardSetEnvironmentMembership.js')
            ]);
            const outcomes = await Promise.allSettled([
                logModule.cleanupDevToolsLogViewer(),
                userModule.cleanupDevToolsUserManagement(),
                winrateModule.cleanupDevToolsWinrateStats(),
                copyLimitModule.cleanupDevToolsCopyLimitPolicies(),
                cardSetEnvModule.cleanupDevToolsCardSetEnvironmentMembership()
            ]);
            const failures = outcomes.filter(outcome => outcome.status === 'rejected').map(outcome => outcome.reason);
            if (failures.length) throw new AggregateError(failures, '开发工具生命周期未完成。');
        } catch (error) {
            console.error('Router: Failed to cleanup dev tools:', error);
            throw error;
        }
    }

    async initDevToolsMatchLabPage() {

        try {
            const module = await import('./devToolsMatchLab.js');
            await module.initializeDevToolsMatchLab({ navigateTo: route => this.navigateTo(route) });
        } catch (error) {
            console.error('Router: Failed to initialize match lab:', error);
            throw error;
        }
    }

    async cleanupDevToolsMatchLabPage() {
        try {
            const module = await import('./devToolsMatchLab.js');
            await module.cleanupDevToolsMatchLab();
        } catch (error) {
            console.error('Router: Failed to cleanup match lab:', error);
            throw error;
        }
    }

    /**
     * 初始化 DSL Editor V2 页面
     */
    async initDslEditorV2Page() {

        try {
            const { DslEditorV2 } = await import('./components/DslEditorV2.js');
            const container = document.getElementById('dsl-editor-v2-container');
            if (container) {
                const editor = new DslEditorV2();
                await editor.render(container);
            }
        } catch (error) {
            console.error('Router: Failed to initialize DSL Editor V2:', error);
            throw error;
        }
    }

    /**
     * 获取当前路由
     */
    getCurrentRoute() {
        return this.currentRoute;
    }

    /**
     * 设置路由变化钩子
     */
    setBeforeRouteChange(callback) {
        this.beforeRouteChange = callback;
    }

    setAfterRouteChange(callback) {
        this.afterRouteChange = callback;
    }
}

// 创建全局路由实例
export const router = new Router();

// 全局函数，供HTML中的按钮调用
window.testMetadataAPI = async function () {
    try {
        const response = await fetch('/api/AbilityEditorMetadata');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const data = await response.json();
        console.info('元数据API响应:', data);
        alert('API测试成功！请查看控制台输出。');
    } catch (error) {
        console.error('API测试失败:', error);
        alert(`API测试失败: ${error.message}`);
    }
};



// 初始化路由
export function initializeRouter() {

    // 根据URL hash确定初始路由
    const hash = window.location.hash.slice(1);

    const initialRoute = hash && router.routes.has(hash) ? hash : 'navigation';

    return router.navigateTo(initialRoute, false);
}
