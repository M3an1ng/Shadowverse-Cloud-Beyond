// src/router.js - 简单的前端路由系统
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
//
import { BATTLE_EXIT_CONFIRM_MESSAGE, shouldConfirmBattleExit } from './battleExitConfirmPolicy.js';

/**
 * 简单的单页应用路由管理器
 * 支持页面切换和状态管理
 */
class Router {
    constructor() {
        this.currentRoute = 'navigation';
        this.routes = new Map();
        this.beforeRouteChange = null;
        this.afterRouteChange = null;

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
            init: this.initGamePage.bind(this),
            cleanup: this.cleanupGamePage.bind(this)
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
            cleanup: this.cleanupMultiplayer.bind(this)
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
        // 监听导航按钮点击
        document.addEventListener('click', (event) => {
            const routeButton = event.target.closest('[data-route]');
            if (routeButton) {
                event.preventDefault();
                const route = routeButton.dataset.route;
                if (shouldConfirmBattleExit(this.currentRoute, route, routeButton)) {
                    const confirmed = window.confirm(BATTLE_EXIT_CONFIRM_MESSAGE);
                    if (!confirmed) {
                        return;
                    }
                }
                this.navigateTo(route);
            }
        });

        // 监听浏览器前进后退
        window.addEventListener('popstate', (event) => {
            const route = event.state?.route || 'navigation';
            this.navigateTo(route, false); // 不推送历史记录
        });
    }

    /**
     * 导航到指定路由
     * @param {string} route - 路由名称
     * @param {boolean} pushState - 是否推送到浏览器历史记录
     */
    async navigateTo(route, pushState = true) {
        if (!this.routes.has(route)) {
            console.error(`Router: Unknown route "${route}"`);
            return;
        }

        // 执行路由前钩子
        if (this.beforeRouteChange) {
            const shouldContinue = await this.beforeRouteChange(this.currentRoute, route);
            if (!shouldContinue) {
                return;
            }
        }

        // 清理当前页面
        const currentRouteConfig = this.routes.get(this.currentRoute);
        if (currentRouteConfig?.cleanup) {
            await currentRouteConfig.cleanup();
        }

        // 隐藏当前页面
        if (currentRouteConfig?.element) {
            currentRouteConfig.element.classList.remove('active');
        }

        // 显示新页面
        const newRouteConfig = this.routes.get(route);
        if (newRouteConfig?.element) {
            newRouteConfig.element.classList.add('active');
        }

        // 更新页面标题
        if (newRouteConfig?.title) {
            document.title = newRouteConfig.title;
        }

        // 初始化新页面
        if (newRouteConfig?.init) {
            await newRouteConfig.init();
        }

        // 更新当前路由
        const oldRoute = this.currentRoute;
        this.currentRoute = route;

        // 更新浏览器历史记录
        if (pushState) {
            window.history.pushState({ route }, '', `#${route}`);
        }

        // 执行路由后钩子
        if (this.afterRouteChange) {
            await this.afterRouteChange(oldRoute, route);
        }

    }

    /**
     * 初始化游戏页面
     */
    async initGamePage() {

        // 动态导入游戏相关模块
        try {
            // 这里可以延迟加载游戏模块
            // const gameModule = await import('./gameManager.js');

            // 如果游戏还未初始化，则初始化
            if (window.gameInitialized !== true) {
                // 触发游戏初始化
                const event = new CustomEvent('initializeGame');
                document.dispatchEvent(event);
            }
        } catch (error) {
            console.error('Router: Failed to initialize game page:', error);
        }
    }

    /**
     * 清理游戏页面
     */
    async cleanupGamePage() {

        // 这里可以清理游戏相关资源
        // 例如断开SignalR连接等
        try {
            // 触发游戏清理事件
            const event = new CustomEvent('cleanupGame');
            document.dispatchEvent(event);
        } catch (error) {
            console.error('Router: Failed to cleanup game page:', error);
        }
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
        }
    }


    /**
     * 初始化牌组编辑器页面
     */
    async initDeckBuilderPage() {

        try {
            // 动态导入牌组编辑器模块
            const deckBuilderModule = await import('./deckBuilder.js');
            await deckBuilderModule.initializeDeckBuilder();
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

    async cleanupMultiplayer() {
        document.body.classList.remove('show-multiplayer');
        try {
            const event = new CustomEvent('cleanupMultiplayer');
            document.dispatchEvent(event);
        } catch (error) {
            console.error('Router: Failed to cleanup multiplayer:', error);
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
            const [logModule, userModule, winrateModule] = await Promise.all([
                import('./devToolsLogViewer.js'),
                import('./devToolsUserManagement.js'),
                import('./devToolsWinrateStats.js')
            ]);
            await Promise.all([
                logModule.initializeDevToolsLogViewer(),
                userModule.initializeDevToolsUserManagement(),
                winrateModule.initializeDevToolsWinrateStats()
            ]);
        } catch (error) {
            console.error('Router: Failed to initialize dev tools:', error);
        }
    }

    async cleanupDevToolsPage() {
        try {
            const [logModule, userModule, winrateModule] = await Promise.all([
                import('./devToolsLogViewer.js'),
                import('./devToolsUserManagement.js'),
                import('./devToolsWinrateStats.js')
            ]);
            await Promise.all([
                logModule.cleanupDevToolsLogViewer(),
                userModule.cleanupDevToolsUserManagement(),
                winrateModule.cleanupDevToolsWinrateStats()
            ]);
        } catch (error) {
            console.error('Router: Failed to cleanup dev tools:', error);
        }
    }

    async initDevToolsMatchLabPage() {

        try {
            const module = await import('./devToolsMatchLab.js');
            await module.initializeDevToolsMatchLab();
        } catch (error) {
            console.error('Router: Failed to initialize match lab:', error);
        }
    }

    async cleanupDevToolsMatchLabPage() {
        try {
            const module = await import('./devToolsMatchLab.js');
            await module.cleanupDevToolsMatchLab();
        } catch (error) {
            console.error('Router: Failed to cleanup match lab:', error);
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
        const data = await response.json();
        alert('API测试成功！请查看控制台输出。');
    } catch (error) {
        console.error('API测试失败:', error);
        alert(`API测试失败: ${error.message}`);
    }
};

window.openDataViewer = function () {
    alert('数据库查看器功能正在开发中...');
};

// 初始化路由
const initRouter = () => {

    // 根据URL hash确定初始路由
    const hash = window.location.hash.slice(1);

    const initialRoute = hash && router.routes.has(hash) ? hash : 'navigation';

    router.navigateTo(initialRoute, false);
};

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initRouter);
} else {
    initRouter();
}
