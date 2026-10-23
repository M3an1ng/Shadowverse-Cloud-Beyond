// 游戏内开发者控制台
// 在游戏界面中提供调试和测试功能
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - 配置管理系统: 文档/05-游戏系统/09-配置管理系统.md
//

import { gameState } from "./gameManager.js";
import { getUserSession } from "./userSession.js";

const TRACE_ENDPOINT = "/api/devtools/condition-trace";

class DevConsole {
  constructor(getConnection, autoShow = false) {
    // 入口持有连接；按需读取以跟随重连，不再反向导入整个启动模块。
    this.getConnection = getConnection;
    this.isVisible = false;
    this.commandHistory = [];
    this.historyIndex = -1;
    this.commands = new Map();
    this.eventMonitoring = false;
    this.eventHandlers = new Map();
    this.autoShow = autoShow;
    this.traceStates = {
      condition: false,
      target: false,
      effect: false,
    };
    this.traceToggleBtns = {};
    this.traceListener = null;
    this.traceListenerConnection = null;
    this.traceMonitorTimer = null;
    this.traceListEl = null;
    this.traceClearBtn = null;
    this.lastGameId = null;
    this.gameStateListener = null;
    this.gameStateListenerConnection = null;
    this.debugCommandSchemas = {
      damage: { required: ["target", "amount"], optional: ["source"] },
      heal: { required: ["target", "amount"], optional: ["source"] },
      "damage-target": { required: ["target", "amount"], optional: [] },
      destroy: { required: ["target"], optional: ["source"] },
      vanish: { required: ["target"], optional: [] },
      return: { required: ["target"], optional: [] },
      draw: { required: ["player", "count"], optional: [] },
      mana: { required: ["player", "delta"], optional: [] },
      pp: { required: ["player", "current"], optional: ["max"] },
      ep: { required: ["player", "current"], optional: ["max"] },
      "add-card": { required: ["player", "card", "location"], optional: [] },
      event: { required: ["name"], optional: ["payload"] },
    };
    this.initCommands();
    this.createConsoleUI();
    this.bindEvents();
    this.initTraceToggle();
    this.setupEventMonitoring();
  }

  canShowConsole() {
    const route = window.location.hash.replace("#", "");
    return route === "game" || route === "dev-tools";
  }

  // 初始化命令
  initCommands() {
    const legacy = (handler) => (ctx) => handler(ctx.positional || []);

    this.commands.set("help", {
      description: "显示所有可用命令",
      execute: () => this.showHelp(),
    });
    this.commands.set("clear", {
      description: "清空控制台",
      execute: () => this.clearOutput(),
    });
    this.commands.set("gamestate", {
      description: "显示当前游戏状态",
      execute: () => this.showGameState(),
    });
    this.commands.set("status", {
      description: "显示状态摘要 (status [player=p1|p2])",
      execute: (ctx) => this.showStatus(ctx),
    });
    this.commands.set("connection", {
      description: "检查SignalR连接状态",
      execute: () => this.checkConnectionStatus(),
    });

    // 游戏操作（沿用旧接口）
    this.commands.set("play", {
      description: "出牌 - 用法: play <cardId> [targetUid]",
      execute: legacy((args) => this.playCard(args)),
    });
    this.commands.set("attack", {
      description: "攻击 - 用法: attack <attackerUid> <targetUid>",
      execute: legacy((args) => this.attack(args)),
    });
    this.commands.set("evolve", {
      description: "进化 - 用法: evolve <unitUid>",
      execute: legacy((args) => this.evolve(args)),
    });
    this.commands.set("endturn", {
      description: "结束回合",
      execute: () => this.endTurn(),
    });

    // 事件注入/调试指令（KV 语法）
    this.commands.set("damage", {
      description:
        "damage target=<cardId> amount=<int> [source=<cardId|p1|p2>]",
      execute: (ctx) => this.sendDebugCommand("damage", ctx),
    });
    this.commands.set("heal", {
      description: "heal target=<cardId> amount=<int> [source=<cardId|p1|p2>]",
      execute: (ctx) => this.sendDebugCommand("heal", ctx),
    });
    this.commands.set("kill", {
      description: "kill target=<cardId>",
      execute: (ctx) => this.sendDebugCommand("kill", ctx),
    });
    this.commands.set("destroy", {
      description: "destroy target=<cardId>",
      execute: (ctx) => this.sendDebugCommand("destroy", ctx),
    });
    this.commands.set("vanish", {
      description: "vanish target=<cardId>",
      execute: (ctx) => this.sendDebugCommand("vanish", ctx),
    });
    this.commands.set("return", {
      description: "return target=<cardId>",
      execute: (ctx) => this.sendDebugCommand("return", ctx),
    });
    this.commands.set("draw", {
      description: "draw player=<p1|p2> count=<int>",
      execute: (ctx) => this.sendDebugCommand("draw", ctx),
    });
    this.commands.set("mana", {
      description: "mana player=<p1|p2> delta=<int>",
      execute: (ctx) => this.sendDebugCommand("mana", ctx),
    });
    this.commands.set("add-card", {
      description:
        "add-card player=<p1|p2> card=<cardDefId> location=hand|board",
      execute: (ctx) => this.sendDebugCommand("add-card", ctx),
    });
    this.commands.set("event", {
      description: "event name=<EventType> payload=<json>",
      execute: (ctx) => this.sendDebugCommand("event", ctx),
    });

    // 事件监听
    this.commands.set("events", {
      description: "显示事件监听状态",
      execute: () => this.showEventStatus(),
    });
    this.commands.set("monitor", {
      description: "开启/关闭事件监听 - 用法: monitor <on|off>",
      execute: (ctx) => this.toggleEventMonitoring(ctx.positional || []),
    });
  }

  // 创建控制台UI
  createConsoleUI() {
    const consoleHTML = `
            <div id="dev-console" class="dev-console hidden">
                <div class="console-header">
                    <div class="console-title">开发者控制台</div>
                    <div class="console-header-actions">
                        <div class="console-trace-toggle-group">
                            <button id="console-trace-toggle-condition" class="console-trace-toggle toggle-switch" type="button" data-kind="condition" aria-pressed="false">
                                <span class="toggle-label">条件追踪</span>
                                <span class="toggle-state">已关闭</span>
                                <span class="toggle-track"><span class="toggle-thumb"></span></span>
                            </button>
                            <button id="console-trace-toggle-target" class="console-trace-toggle toggle-switch" type="button" data-kind="target" aria-pressed="false">
                                <span class="toggle-label">目标追踪</span>
                                <span class="toggle-state">已关闭</span>
                                <span class="toggle-track"><span class="toggle-thumb"></span></span>
                            </button>
                            <button id="console-trace-toggle-effect" class="console-trace-toggle toggle-switch" type="button" data-kind="effect" aria-pressed="false">
                                <span class="toggle-label">效果追踪</span>
                                <span class="toggle-state">已关闭</span>
                                <span class="toggle-track"><span class="toggle-thumb"></span></span>
                            </button>
                        </div>
                        <button id="console-close" class="console-close" aria-label="关闭控制台">×</button>
                    </div>
                </div>
                <div class="console-body">
                    <section class="console-panel console-panel-trace">
                        <div class="console-panel-header">
                            <div class="console-panel-title">能力解析日志</div>
                            <button id="console-trace-clear" class="console-panel-action" type="button">清空</button>
                        </div>
                        <div id="console-trace-list" class="console-trace-list">
                            <div class="console-trace-empty">暂无解析日志</div>
                        </div>
                    </section>
                    <section class="console-panel console-panel-output">
                        <div class="console-panel-header">
                            <div class="console-panel-title">控制台输出</div>
                        </div>
                        <div class="console-output" id="console-output">
                            <div class="console-welcome">
                                欢迎使用开发者控制台！输入 'help' 查看可用命令。
                            </div>
                        </div>
                        <div class="console-input-container">
                            <span class="console-prompt">></span>
                            <input type="text" id="console-input" class="console-input" placeholder="输入命令...">
                        </div>
                    </section>
                </div>
            </div>
        `;

    document.body.insertAdjacentHTML("beforeend", consoleHTML);
    this.traceListEl = document.getElementById("console-trace-list");
    this.traceClearBtn = document.getElementById("console-trace-clear");

    // 添加CSS样式
    const style = document.createElement("style");
    style.textContent = `
            /* Override global devtools.css (#dev-console) to keep a compact centered console */
            #dev-console {
                position: fixed;
                left: 50%;
                top: 50%;
                transform: translate(-50%, -50%);
                width: min(1200px, calc(100vw - 24px));
                height: min(560px, calc(100vh - 140px));
                background: linear-gradient(150deg, rgba(10, 14, 22, 0.96), rgba(18, 24, 32, 0.98));
                border: 1px solid rgba(255, 255, 255, 0.08);
                border-radius: 14px;
                color: rgba(248, 250, 252, 0.95);
                font-family: var(--font-sans, "Inter", "Segoe UI", sans-serif);
                font-size: 13px;
                z-index: 10000;
                display: flex;
                flex-direction: column;
                box-shadow: 0 24px 60px rgba(0, 0, 0, 0.65),
                    inset 0 0 0 1px rgba(255, 255, 255, 0.02),
                    0 0 24px rgba(59, 130, 246, 0.18);
                backdrop-filter: blur(12px);
            }

            #dev-console.hidden {
                display: none;
            }

            .dev-console {
                /* keep class for other selectors; layout is driven by #dev-console above */
            }

            .dev-console.hidden {
                display: none;
            }

            .console-header {
                background: linear-gradient(180deg, rgba(20, 26, 36, 0.92), rgba(12, 16, 24, 0.98));
                padding: 12px 16px;
                border-bottom: 1px solid rgba(255, 255, 255, 0.08);
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-radius: 14px 14px 0 0;
            }

            .console-title {
                font-size: 16px;
                font-weight: 700;
                letter-spacing: 0.08em;
                text-transform: uppercase;
                color: rgba(248, 250, 252, 0.96);
            }

            .console-header-actions {
                display: flex;
                align-items: center;
                gap: 10px;
            }

            .console-trace-toggle-group {
                display: flex;
                align-items: center;
                gap: 8px;
                flex-wrap: wrap;
            }

            .console-trace-toggle {
                background: rgba(12, 16, 24, 0.94);
                border: 1px solid rgba(148, 163, 184, 0.25);
                color: rgba(226, 232, 240, 0.9);
                font-size: 12px;
                padding: 6px 10px;
                border-radius: 999px;
                cursor: pointer;
                transition: background 0.2s ease, border-color 0.2s ease, color 0.2s ease, box-shadow 0.2s ease;
            }

            .console-trace-toggle:hover {
                background: rgba(18, 24, 32, 0.98);
                border-color: rgba(96, 165, 250, 0.65);
                box-shadow: 0 0 12px rgba(59, 130, 246, 0.2);
            }

            .console-trace-toggle:disabled {
                cursor: not-allowed;
                opacity: 0.5;
            }

            .toggle-switch {
                display: inline-flex;
                align-items: center;
                gap: 8px;
            }

            .toggle-label {
                font-weight: 600;
                color: rgba(226, 232, 240, 0.95);
            }

            .toggle-state {
                color: rgba(148, 163, 184, 0.9);
                font-size: 11px;
            }

            .toggle-track {
                width: 38px;
                height: 18px;
                border-radius: 999px;
                background: rgba(15, 23, 42, 0.85);
                border: 1px solid rgba(148, 163, 184, 0.35);
                position: relative;
                transition: background 0.2s ease, border-color 0.2s ease, box-shadow 0.2s ease;
            }

            .toggle-thumb {
                position: absolute;
                top: 50%;
                left: 3px;
                width: 12px;
                height: 12px;
                border-radius: 50%;
                background: #e2e8f0;
                transform: translateY(-50%);
                transition: transform 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
                box-shadow: 0 0 6px rgba(0, 0, 0, 0.6);
            }

            .console-trace-toggle.is-active .toggle-track {
                background: rgba(59, 130, 246, 0.35);
                border-color: rgba(59, 130, 246, 0.75);
                box-shadow: inset 0 0 8px rgba(56, 189, 248, 0.4);
            }

            .console-trace-toggle.is-active .toggle-thumb {
                transform: translate(18px, -50%);
                background: #7dd3fc;
                box-shadow: 0 0 8px rgba(56, 189, 248, 0.6);
            }

            .console-close {
                background: rgba(12, 16, 24, 0.9);
                border: 1px solid rgba(148, 163, 184, 0.35);
                color: rgba(226, 232, 240, 0.85);
                font-size: 18px;
                cursor: pointer;
                padding: 0;
                width: 28px;
                height: 28px;
                border-radius: 8px;
                transition: border-color 0.2s ease, color 0.2s ease, background 0.2s ease;
            }

            .console-close:hover {
                border-color: rgba(96, 165, 250, 0.7);
                color: #ffffff;
                background: rgba(59, 130, 246, 0.2);
            }

            .console-body {
                display: grid;
                grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
                gap: 12px;
                padding: 14px;
                flex: 1;
                overflow: hidden;
            }

            .console-panel {
                display: flex;
                flex-direction: column;
                background: linear-gradient(180deg, rgba(16, 20, 30, 0.94), rgba(10, 12, 18, 0.98));
                border: 1px solid rgba(255, 255, 255, 0.08);
                border-radius: 12px;
                overflow: hidden;
                min-height: 0;
                box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.02);
            }

            .console-panel-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 10px 12px;
                border-bottom: 1px solid rgba(255, 255, 255, 0.08);
                background: rgba(14, 18, 26, 0.95);
            }

            .console-panel-title {
                font-size: 13px;
                font-weight: 600;
                color: rgba(241, 245, 249, 0.9);
            }

            .console-panel-action {
                background: rgba(12, 16, 24, 0.9);
                border: 1px solid rgba(59, 130, 246, 0.35);
                color: rgba(125, 211, 252, 0.9);
                font-size: 12px;
                padding: 4px 10px;
                border-radius: 6px;
                cursor: pointer;
                transition: border-color 0.2s ease, background 0.2s ease, color 0.2s ease;
            }

            .console-panel-action:hover {
                border-color: rgba(96, 165, 250, 0.7);
                background: rgba(59, 130, 246, 0.2);
                color: #e0f2fe;
            }

            .console-output {
                flex: 1;
                padding: 10px 12px;
                overflow-y: auto;
                background: rgba(10, 12, 18, 0.9);
            }

            .console-output::-webkit-scrollbar {
                width: 6px;
            }

            .console-output::-webkit-scrollbar-track {
                background: rgba(15, 23, 42, 0.6);
            }

            .console-output::-webkit-scrollbar-thumb {
                background: rgba(148, 163, 184, 0.6);
                border-radius: 3px;
            }

            .console-input-container {
                display: flex;
                align-items: center;
                padding: 10px 12px;
                background: rgba(12, 16, 24, 0.98);
                border-top: 1px solid rgba(255, 255, 255, 0.08);
            }

            .console-prompt {
                color: #7dd3fc;
                margin-right: 8px;
            }

            .console-input {
                flex: 1;
                background: transparent;
                border: none;
                color: rgba(241, 245, 249, 0.92);
                font-family: var(--font-mono, "JetBrains Mono", "Courier New", monospace);
                font-size: 12px;
                outline: none;
            }

            .console-toggle {
                position: fixed;
                top: 10px;
                right: 10px;
                background: rgba(10, 12, 18, 0.8);
                border: 1px solid rgba(59, 130, 246, 0.4);
                color: #7dd3fc;
                padding: 8px 12px;
                border-radius: 4px;
                cursor: pointer;
                z-index: 9999;
                font-size: 14px;
            }

            .console-toggle:hover {
                background: rgba(15, 23, 42, 0.9);
            }

            .console-line {
                margin: 2px 0;
                word-wrap: break-word;
            }

            .console-command {
                color: #7dd3fc;
            }

            .console-output-text {
                color: rgba(226, 232, 240, 0.9);
            }

            .console-error {
                color: #fca5a5;
            }

            .console-success {
                color: #86efac;
            }

            .console-info {
                color: #93c5fd;
            }

            .console-welcome {
                color: rgba(148, 163, 184, 0.95);
                font-style: italic;
            }

            .console-trace-list {
                flex: 1;
                overflow-y: auto;
                padding: 12px;
                display: flex;
                flex-direction: column;
                gap: 10px;
                background: rgba(10, 12, 18, 0.9);
            }

            .console-trace-empty {
                color: rgba(148, 163, 184, 0.7);
                font-size: 12px;
                text-align: center;
                padding: 24px 0;
            }

            .trace-entry {
                border: 1px solid rgba(255, 255, 255, 0.08);
                border-radius: 10px;
                background: rgba(14, 18, 26, 0.95);
                padding: 10px 12px;
                cursor: pointer;
                transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.2s ease;
            }

            .trace-entry:hover {
                border-color: rgba(96, 165, 250, 0.6);
                box-shadow: 0 0 12px rgba(59, 130, 246, 0.2);
                transform: translateY(-1px);
            }

            .trace-entry.is-success {
                border-color: rgba(34, 197, 94, 0.45);
            }

            .trace-entry.is-failure {
                border-color: rgba(248, 113, 113, 0.55);
            }

            .trace-summary {
                display: flex;
                justify-content: space-between;
                align-items: center;
                gap: 10px;
                font-weight: 600;
                color: rgba(248, 250, 252, 0.95);
            }

            .trace-summary-main {
                display: flex;
                align-items: center;
                gap: 8px;
                flex-wrap: wrap;
            }

            .trace-card-name {
                color: #7dd3fc;
                text-shadow: 0 0 10px rgba(59, 130, 246, 0.4);
                background: rgba(59, 130, 246, 0.15);
                border: 1px solid rgba(59, 130, 246, 0.35);
                padding: 2px 6px;
                border-radius: 6px;
            }

            .trace-ability-name {
                color: rgba(241, 245, 249, 0.96);
                text-shadow: 0 0 8px rgba(148, 163, 184, 0.3);
                background: rgba(15, 23, 42, 0.6);
                border: 1px solid rgba(148, 163, 184, 0.25);
                padding: 2px 6px;
                border-radius: 6px;
            }

            .trace-symbol {
                color: rgba(148, 163, 184, 0.8);
                font-size: 12px;
                letter-spacing: 1px;
                text-transform: uppercase;
            }

            .trace-kind {
                font-size: 11px;
                padding: 2px 8px;
                border-radius: 999px;
                border: 1px solid rgba(59, 130, 246, 0.5);
                color: #7dd3fc;
                background: rgba(59, 130, 246, 0.16);
                letter-spacing: 0.5px;
                text-transform: none;
            }

            .trace-kind-target {
                border-color: rgba(56, 189, 248, 0.6);
                color: #bae6fd;
                background: rgba(56, 189, 248, 0.16);
            }

            .trace-kind-effect {
                border-color: rgba(14, 165, 233, 0.5);
                color: #e0f2fe;
                background: rgba(14, 165, 233, 0.16);
            }

            .trace-summary .trace-result {
                font-size: 12px;
                padding: 3px 8px;
                border-radius: 999px;
                background: rgba(34, 197, 94, 0.18);
                color: #bbf7d0;
                border: 1px solid rgba(34, 197, 94, 0.45);
            }

            .trace-entry.is-failure .trace-summary .trace-result {
                background: rgba(248, 113, 113, 0.18);
                color: #fecaca;
                border-color: rgba(248, 113, 113, 0.55);
            }

            .trace-meta {
                margin-top: 6px;
                font-size: 12px;
                color: rgba(148, 163, 184, 0.85);
                display: flex;
                flex-direction: column;
                gap: 4px;
            }

            .trace-details {
                margin-top: 10px;
                border-top: 1px dashed rgba(148, 163, 184, 0.3);
                padding-top: 10px;
                display: none;
                flex-direction: column;
                gap: 8px;
            }

            .trace-entry.is-open .trace-details {
                display: flex;
            }

            .trace-expression {
                background: rgba(12, 16, 24, 0.9);
                border: 1px solid rgba(148, 163, 184, 0.2);
                border-radius: 8px;
                padding: 8px 10px;
                color: rgba(241, 245, 249, 0.92);
            }

            .trace-expression-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                font-size: 12px;
                color: rgba(148, 163, 184, 0.85);
                margin-bottom: 6px;
            }

            .trace-expression-code {
                font-family: var(--font-mono, "JetBrains Mono", "Courier New", monospace);
                font-size: 12px;
                cursor: pointer;
                color: rgba(241, 245, 249, 0.92);
                background: rgba(15, 23, 42, 0.8);
                border-radius: 6px;
                padding: 6px 8px;
                border: 1px solid rgba(148, 163, 184, 0.25);
                transition: border-color 0.2s ease, color 0.2s ease;
            }

            .trace-expression-code:hover {
                border-color: rgba(96, 165, 250, 0.7);
                color: #e0f2fe;
            }

            .trace-expression-trace {
                display: none;
                margin-top: 6px;
                font-family: var(--font-mono, "JetBrains Mono", "Courier New", monospace);
                font-size: 12px;
                color: #bae6fd;
                background: rgba(10, 12, 18, 0.95);
                border-radius: 6px;
                padding: 6px 8px;
                border: 1px solid rgba(59, 130, 246, 0.35);
            }

            .trace-expression-values {
                list-style: none;
                padding: 0;
                margin: 0;
                display: flex;
                gap: 6px;
            }

            .trace-expression-values li {
                display: flex;
                justify-content: space-between;
                gap: 8px;
                color: rgba(226, 232, 240, 0.92);
                background: rgba(12, 16, 24, 0.92);
                border: 1px solid rgba(148, 163, 184, 0.2);
                border-radius: 6px;
                padding: 6px 8px;
            }

            .trace-expression-values .value-key {
                color: rgba(241, 245, 249, 0.95);
            }

            .trace-expression-values .value-arrow {
                color: rgba(148, 163, 184, 0.75);
            }

            .trace-expression-values .value-result {
                color: #7dd3fc;
                font-weight: 600;
            }

            .trace-expression.is-open .trace-expression-trace {
                display: block;
            }

            /* 响应式设计 */
            @media (max-width: 768px) {
                #dev-console {
                    width: calc(100vw - 16px);
                    height: calc(100vh - 120px);
                    top: 50%;
                }

                .console-body {
                    grid-template-columns: 1fr;
                }
            }
        `;
    document.head.appendChild(style);
  }

  getCommandUsage(commandName) {
    const cmd = this.commands.get(commandName);
    return cmd?.description ? `用法: ${cmd.description}` : null;
  }

  extractTraceValues(trace) {
    if (typeof trace !== "string" || trace.trim() === "") {
      return [];
    }

    const results = [];
    const seen = new Set();
    const regex = /([A-Za-z0-9_.,()]+)\s*->\(([^)]+)\)/g;
    let match;
    while ((match = regex.exec(trace)) !== null) {
      const key = match[1];
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);
      results.push({ key, value: match[2] });
    }

    return results;
  }

  validateDebugCommand(commandName, ctx) {
    const schema = this.debugCommandSchemas[commandName];
    if (!schema) {
      return { ok: false, message: `未支持的调试指令: ${commandName}` };
    }

    const positional = ctx?.positional || [];
    if (positional.length > 0) {
      return { ok: false, message: `不支持位置参数: ${positional.join(" ")}` };
    }

    const kv = ctx?.kv || {};
    const keys = Object.keys(kv);
    const allowed = new Set([
      ...(schema.required || []),
      ...(schema.optional || []),
    ]);
    const unknown = keys.find((k) => !allowed.has(k));
    if (unknown) {
      return { ok: false, message: `不支持参数: ${unknown}` };
    }

    const missing = (schema.required || []).find(
      (k) => !kv[k] || String(kv[k]).trim() === "",
    );
    if (missing) {
      return { ok: false, message: `缺少参数: ${missing}` };
    }

    const validateGuid = (value) =>
      this.isValidGuid(String(value || "").trim());
    const validateInt = (value) =>
      /^[-+]?\d+$/.test(String(value || "").trim());
    const validatePlayerToken = (value) => {
      const v = String(value || "")
        .trim()
        .toLowerCase();
      return v === "p1" || v === "p2";
    };
    const validateSourceToken = (value) => {
      const v = String(value || "")
        .trim()
        .toLowerCase();
      return v === "p1" || v === "p2" || validateGuid(v);
    };

    if (kv.target && !validateGuid(kv.target))
      return { ok: false, message: "target 必须是有效 GUID" };
    if (kv.amount && !validateInt(kv.amount))
      return { ok: false, message: "amount 必须是整数" };
    if (kv.count && !validateInt(kv.count))
      return { ok: false, message: "count 必须是整数" };
    if (kv.delta && !validateInt(kv.delta))
      return { ok: false, message: "delta 必须是整数" };
    if (kv.current && !validateInt(kv.current))
      return { ok: false, message: "current 必须是整数" };
    if (kv.max && !validateInt(kv.max))
      return { ok: false, message: "max 必须是整数" };
    if (kv.player && !validatePlayerToken(kv.player))
      return { ok: false, message: "player 仅支持 p1 或 p2" };
    if (kv.source && !validateSourceToken(kv.source))
      return { ok: false, message: "source 仅支持 p1/p2 或 GUID" };
    if (kv.card && !validateInt(kv.card))
      return { ok: false, message: "card 必须是整数卡牌定义ID" };
    if (kv.location) {
      const loc = String(kv.location || "")
        .trim()
        .toLowerCase();
      if (!["hand", "board", "field", "deck"].includes(loc))
        return { ok: false, message: "location 仅支持 hand/board/field/deck" };
    }

    return { ok: true };
  }

  // 绑定事件
  bindEvents() {
    const closeBtn = document.getElementById("console-close");
    const input = document.getElementById("console-input");
    const traceClearBtn = this.traceClearBtn;

    if (closeBtn) {
      closeBtn.addEventListener("click", () => this.hide());
    }

    input.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        this.executeCommand(input.value.trim());
        input.value = "";
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        this.navigateHistory(-1);
      } else if (e.key === "ArrowDown") {
        e.preventDefault();
        this.navigateHistory(1);
      }
    });

    // 全局快捷键 (Ctrl + `)
    document.addEventListener("keydown", (e) => {
      if (e.ctrlKey && e.key === "`") {
        if (!this.canShowConsole()) {
          return;
        }
        e.preventDefault();
        this.toggle();
      }
    });

    if (traceClearBtn) {
      traceClearBtn.addEventListener("click", () => this.clearTraceLogs());
    }
  }

  initTraceToggle() {
    this.traceToggleBtns = {
      condition: document.getElementById("console-trace-toggle-condition"),
      target: document.getElementById("console-trace-toggle-target"),
      effect: document.getElementById("console-trace-toggle-effect"),
    };

    const hasToggle = Object.values(this.traceToggleBtns).some(Boolean);
    if (!hasToggle) {
      return;
    }

    Object.entries(this.traceToggleBtns).forEach(([kind, button]) => {
      if (!button) return;
      button.addEventListener("click", () => {
        void this.toggleTrace(kind);
      });
    });

    void this.refreshTraceState();
  }

  // 显示/隐藏控制台
  toggle() {
    if (!this.isVisible && !this.canShowConsole()) {
      return;
    }
    this.isVisible = !this.isVisible;
    const console = document.getElementById("dev-console");

    if (this.isVisible) {
      console.classList.remove("hidden");
      const input = document.getElementById("console-input");
      if (input) input.focus();
    } else {
      console.classList.add("hidden");
    }
  }

  hide() {
    this.isVisible = false;
    const console = document.getElementById("dev-console");
    if (console) console.classList.add("hidden");
  }

  show() {
    if (!this.canShowConsole()) {
      return;
    }
    this.isVisible = true;
    const consoleEl = document.getElementById("dev-console");
    if (consoleEl) {
      consoleEl.classList.remove("hidden");
    }
    const input = document.getElementById("console-input");
    if (input) input.focus();
    void this.refreshTraceState();
  }

  // 输出文本到控制台
  output(text, type = "output") {
    const outputDiv = document.getElementById("console-output");
    const line = document.createElement("div");
    line.className = `console-line console-${type}`;
    line.textContent = text;
    outputDiv.appendChild(line);
    outputDiv.scrollTop = outputDiv.scrollHeight;
  }

  // 清空输出
  clearOutput() {
    document.getElementById("console-output").innerHTML = "";
    this.output("控制台已清空", "info");
  }

  async refreshTraceState() {
    const buttons = Object.values(this.traceToggleBtns);
    if (!buttons.some(Boolean)) {
      return;
    }

    const session = getUserSession();
    const isUser = Boolean(session.userId && session.mode === "user");
    if (!isUser) {
      buttons.forEach((btn) => {
        if (btn) btn.disabled = true;
      });
      this.setTraceState("condition", false, "需登录");
      this.setTraceState("target", false, "需登录");
      this.setTraceState("effect", false, "需登录");
      return;
    }

    buttons.forEach((btn) => {
      if (btn) btn.disabled = false;
    });
    try {
      const response = await fetch(
        `${TRACE_ENDPOINT}?userId=${encodeURIComponent(session.userId)}`,
      );
      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`);
      }
      const payload = await response.json();
      this.traceStates.condition = Boolean(
        payload?.conditionEnabled ?? payload?.ConditionEnabled,
      );
      this.traceStates.target = Boolean(
        payload?.targetEnabled ?? payload?.TargetEnabled,
      );
      this.traceStates.effect = Boolean(
        payload?.effectEnabled ?? payload?.EffectEnabled,
      );
      this.setTraceState("condition", this.traceStates.condition);
      this.setTraceState("target", this.traceStates.target);
      this.setTraceState("effect", this.traceStates.effect);
    } catch (error) {
      console.warn("Failed to load condition trace state:", error);
      this.setTraceState("condition", false);
      this.setTraceState("target", false);
      this.setTraceState("effect", false);
    }
  }

  setTraceState(kind, enabled, labelOverride = null) {
    if (!this.traceToggleBtns || !this.traceToggleBtns[kind]) return;
    this.traceStates[kind] = Boolean(enabled);
    const button = this.traceToggleBtns[kind];
    const labelMap = {
      condition: "条件追踪",
      target: "目标追踪",
      effect: "效果追踪",
    };
    const label = labelMap[kind] || "追踪";
    button.classList.toggle("is-active", this.traceStates[kind]);
    button.setAttribute("aria-pressed", String(this.traceStates[kind]));
    const stateEl = button.querySelector(".toggle-state");
    const labelEl = button.querySelector(".toggle-label");
    if (labelEl) {
      labelEl.textContent = label;
    }
    if (stateEl) {
      if (labelOverride) {
        stateEl.textContent = labelOverride;
      } else {
        stateEl.textContent = this.traceStates[kind] ? "已开启" : "已关闭";
      }
    }
  }

  async toggleTrace(kind) {
    const session = getUserSession();
    if (!session.userId || session.mode !== "user") {
      Object.values(this.traceToggleBtns).forEach((btn) => {
        if (btn) btn.disabled = true;
      });
      this.setTraceState("condition", false, "需登录");
      this.setTraceState("target", false, "需登录");
      this.setTraceState("effect", false, "需登录");
      return;
    }

    const nextState = !Boolean(this.traceStates[kind]);
    const nextStates = {
      condition: this.traceStates.condition,
      target: this.traceStates.target,
      effect: this.traceStates.effect,
      [kind]: nextState,
    };
    this.traceStates = { ...nextStates };
    Object.values(this.traceToggleBtns).forEach((btn) => {
      if (btn) btn.disabled = true;
    });

    try {
      const response = await fetch(TRACE_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            userId: session.userId,
            conditionEnabled: Boolean(nextStates.condition),
            targetEnabled: Boolean(nextStates.target),
            effectEnabled: Boolean(nextStates.effect),
          }),
        });
      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`);
      }
      const payload = await response.json();
      this.traceStates.condition = Boolean(
        payload?.conditionEnabled ?? payload?.ConditionEnabled,
      );
      this.traceStates.target = Boolean(
        payload?.targetEnabled ?? payload?.TargetEnabled,
      );
      this.traceStates.effect = Boolean(
        payload?.effectEnabled ?? payload?.EffectEnabled,
      );
      this.setTraceState("condition", this.traceStates.condition);
      this.setTraceState("target", this.traceStates.target);
      this.setTraceState("effect", this.traceStates.effect);
    } catch (error) {
      console.warn("Failed to update condition trace state:", error);
      this.traceStates[kind] = !nextState;
      this.setTraceState(kind, this.traceStates[kind]);
    } finally {
      Object.values(this.traceToggleBtns).forEach((btn) => {
        if (btn) btn.disabled = false;
      });
    }
  }

  // 执行命令
  executeCommand(commandLine) {
    if (!commandLine) return;

    this.commandHistory.push(commandLine);
    this.historyIndex = this.commandHistory.length;

    this.output(`> ${commandLine}`, "command");

    const parsed = this.parseCommandLine(commandLine);
    const cmd = parsed && parsed.name ? this.commands.get(parsed.name) : null;

    if (cmd) {
      try {
        cmd.execute(parsed);
      } catch (error) {
        this.output(`错误: ${error.message}`, "error");
      }
    } else {
      this.output(
        `未知命令: ${parsed.name || "(空)"}。输入 'help' 查看可用命令。`,
        "error",
      );
    }
  }

  parseCommandLine(commandLine) {
    const tokens = [];
    let current = "";
    let inQuotes = false;
    for (let i = 0; i < commandLine.length; i++) {
      const ch = commandLine[i];
      if (ch === '"') {
        inQuotes = !inQuotes;
        continue;
      }
      if (ch === " " && !inQuotes) {
        if (current.length > 0) {
          tokens.push(current);
          current = "";
        }
      } else {
        current += ch;
      }
    }
    if (current.length > 0) tokens.push(current);

    if (tokens.length === 0) {
      return { name: "", kv: {}, positional: [], raw: [] };
    }

    const name = tokens.shift().toLowerCase();
    const kv = {};
    const positional = [];

    tokens.forEach((tok) => {
      const eq = tok.indexOf("=");
      if (eq > 0) {
        const key = tok.slice(0, eq).toLowerCase();
        let value = tok.slice(eq + 1);
        if (value.startsWith('"') && value.endsWith('"') && value.length >= 2) {
          value = value.slice(1, -1);
        }
        kv[key] = value;
      } else {
        positional.push(tok);
      }
    });

    return { name, kv, positional, raw: tokens };
  }

  // 命令历史导航
  navigateHistory(direction) {
    const input = document.getElementById("console-input");

    if (direction === -1 && this.historyIndex > 0) {
      this.historyIndex--;
      input.value = this.commandHistory[this.historyIndex];
    } else if (
      direction === 1 &&
      this.historyIndex < this.commandHistory.length - 1
    ) {
      this.historyIndex++;
      input.value = this.commandHistory[this.historyIndex];
    } else if (
      direction === 1 &&
      this.historyIndex === this.commandHistory.length - 1
    ) {
      this.historyIndex = this.commandHistory.length;
      input.value = "";
    }
  }

  // 显示帮助
  showHelp() {
    this.output("=== 开发者控制台帮助 ===", "info");
    this.output("", "output");

    // 分类显示命令
    this.output("基础命令:", "info");
    this.output("  help - 显示此帮助信息", "output");
    this.output("  clear - 清空控制台输出", "output");
    this.output("  connection - 检查SignalR连接状态", "output");
    this.output("  gamestate/status - 显示当前游戏状态", "output");
    this.output("", "output");

    this.output("游戏操作 (旧接口):", "info");
    this.output("  play <cardUid> [targetUid] - 出牌", "output");
    this.output("  attack <attackerUid> <targetUid> - 攻击", "output");
    this.output("  evolve <unitUid> - 进化单位", "output");
    this.output("  endturn - 结束当前回合", "output");
    this.output("", "output");

    this.output("事件调试 (KV 语法):", "info");
    this.output(
      "  damage target=<cardId> amount=<int> [source=<cardId|p1|p2>]",
      "output",
    );
    this.output(
      "  heal target=<cardId> amount=<int> [source=<cardId|p1|p2>]",
      "output",
    );
    this.output("  kill|destroy target=<cardId>", "output");
    this.output("  vanish target=<cardId>", "output");
    this.output("  return target=<cardId>", "output");
    this.output("  draw player=<p1|p2> count=<int>", "output");
    this.output("  mana player=<p1|p2> delta=<int>", "output");
    this.output(
      "  add-card player=<p1|p2> card=<cardDefId> location=hand|board",
      "output",
    );
    this.output("  event name=<EventType> payload=<json>", "output");
    this.output("", "output");

    this.output("调试工具:", "info");
    this.output("  events - 显示事件监听状态", "output");
    this.output("  monitor <on|off> - 开启/关闭事件监听", "output");
    this.output("", "output");

    this.output("⌨️ 快捷键:", "info");
    this.output("  Ctrl + ` - 切换控制台显示/隐藏", "output");
    this.output("  ↑↓ 键 - 浏览命令历史", "output");
    this.output("", "output");

    this.output("使用提示:", "info");
    this.output('  1. 首先运行 "connection" 检查连接状态', "output");
    this.output('  2. 使用 "gamestate" 或 "status" 查看当前游戏信息', "output");
    this.output('  3. 使用 "monitor on" 开启事件监听以调试', "output");
  }

  // 显示游戏状态
  showGameState() {
    this.output("当前游戏状态:", "info");
    this.output(`游戏ID: ${gameState.gameId || "未设置"}`, "output");
    this.output(`玩家ID: ${gameState.localPlayerId || "未设置"}`, "output");
    this.output(`游戏阶段: ${gameState.gamePhase}`, "output");
    this.output(`回合数: ${gameState.turnNumber}`, "output");
    this.output(`当前玩家: ${gameState.currentPlayerId || "未设置"}`, "output");

    if (gameState.activePlayer) {
      this.output(
        `手牌数量: ${gameState.activePlayer.hand?.length || 0}`,
        "output",
      );
      this.output(
        `场上单位: ${gameState.activePlayer.field?.length || 0}`,
        "output",
      );
      this.output(
        `当前PP: ${gameState.activePlayer.currentPP || 0}/${gameState.activePlayer.maxPP || 0}`,
        "output",
      );
      this.output(
        `当前EP: ${gameState.activePlayer.currentEP || 0}/${gameState.activePlayer.maxEP || 0}`,
        "output",
      );
    }
  }

  showStatus(ctx) {
    const player = ctx.kv.player;
    this.output("=== 状态摘要 ===", "info");
    const players = gameState.players || [];
    const maybePrint = (p, label) => {
      if (!p) {
        this.output(`${label}: 未知`, "output");
        return;
      }
      const hand = Array.isArray(p.hand) ? p.hand.length : 0;
      const field = Array.isArray(p.field) ? p.field.length : 0;
      const pp = `${p.currentPP ?? "-"} / ${p.maxPP ?? "-"}`;
      const ep = `${p.currentEP ?? "-"} / ${p.maxEP ?? "-"}`;
      this.output(
        `${label}: 手牌=${hand}, 场上=${field}, PP=${pp}, EP=${ep}`,
        "output",
      );
    };

    if (!player || player === "p1") {
      maybePrint(players[0], "[P1]");
    }
    if (!player || player === "p2") {
      maybePrint(players[1], "[P2]");
    }
  }

  // 出牌命令
  async playCard(args) {
    const connection = this.getConnection();
    if (args.length < 1) {
      this.output("用法: play <cardUid> [targetUid]", "error");
      return;
    }

    const cardUid = args[0];
    const targetUid = args[1] || null;

    // Validate GUID format
    if (!this.isValidGuid(cardUid)) {
      this.output(`无效的卡牌UID格式: ${cardUid}`, "error");
      return;
    }

    try {
      if (connection && connection.state === "Connected") {
        // Use current PlayCardActionDto structure
        const actionDto = {
          CardUid: cardUid,
          TargetUid: targetUid ? targetUid : null,
        };

        await connection.invoke("PlayCard", actionDto);
        this.output(`出牌命令已发送: 卡牌UID ${cardUid}`, "success");
      } else {
        this.output("未连接到服务器", "error");
      }
    } catch (error) {
      this.output(`出牌失败: ${error.message}`, "error");
    }
  }

  // 攻击命令
  async attack(args) {
    const connection = this.getConnection();
    if (args.length < 2) {
      this.output("用法: attack <attackerUid> <targetUid>", "error");
      return;
    }

    const attackerUid = args[0];
    const targetUid = args[1];

    // Validate GUID formats
    if (!this.isValidGuid(attackerUid) || !this.isValidGuid(targetUid)) {
      this.output(
        `无效的UID格式 - 攻击者: ${attackerUid}, 目标: ${targetUid}`,
        "error",
      );
      return;
    }

    try {
      if (connection && connection.state === "Connected") {
        // Use current AttackActionDto structure
        const actionDto = {
          AttackerUid: attackerUid,
          TargetUid: targetUid,
        };

        await connection.invoke("PerformAttack", actionDto);
        this.output(
          `攻击命令已发送: ${attackerUid} -> ${targetUid}`,
          "success",
        );
      } else {
        this.output("未连接到服务器", "error");
      }
    } catch (error) {
      this.output(`攻击失败: ${error.message}`, "error");
    }
  }

  // 进化命令
  async evolve(args) {
    const connection = this.getConnection();
    if (args.length < 1) {
      this.output("用法: evolve <unitUid>", "error");
      return;
    }

    const unitUid = args[0];

    // Validate GUID format
    if (!this.isValidGuid(unitUid)) {
      this.output(`无效的单位UID格式: ${unitUid}`, "error");
      return;
    }

    try {
      if (connection && connection.state === "Connected") {
        // Use current EvolveActionDto structure
        const actionDto = {
          UnitUid: unitUid,
        };

        await connection.invoke("PerformEvolve", actionDto);
        this.output(`进化命令已发送: ${unitUid}`, "success");
      } else {
        this.output("未连接到服务器", "error");
      }
    } catch (error) {
      this.output(`进化失败: ${error.message}`, "error");
    }
  }

  // 结束回合
  async endTurn() {
    const connection = this.getConnection();
    try {
      if (connection && connection.state === "Connected") {
        // Generate unique request ID for idempotency
        const requestId = this.generateRequestId();
        await connection.invoke("EndTurn", requestId);
        this.output(`回合结束命令已发送 (请求ID: ${requestId})`, "success");
      } else {
        this.output("未连接到服务器", "error");
      }
    } catch (error) {
      this.output(`结束回合失败: ${error.message}`, "error");
    }
  }

  // 调试指令统一入口（需要后端支持 ExecuteDebugCommand）
  async sendDebugCommand(commandName, ctx) {
    const connection = this.getConnection();
    if (!ctx || !ctx.kv) {
      this.output("无效的命令参数", "error");
      return;
    }

    const validation = this.validateDebugCommand(commandName, ctx);
    if (!validation.ok) {
      this.output(`参数错误: ${validation.message}`, "error");
      const usage = this.getCommandUsage(commandName);
      if (usage) this.output(usage, "info");
      return;
    }

    if (!connection || connection.state !== "Connected") {
      this.output("未连接到服务器，无法发送调试指令", "error");
      return;
    }

    const payload = { Command: commandName, Args: ctx.kv };
    try {
      await connection.invoke("ExecuteDebugCommand", payload);
      this.output(
        `已发送调试指令: ${commandName} ${JSON.stringify(ctx.kv)}`,
        "success",
      );
    } catch (err) {
      this.output(`调试指令执行失败或未实现: ${err.message}`, "error");
    }
  }

  // Helper functions
  isValidGuid(str) {
    if (!str || typeof str !== "string") {
      return false;
    }
    const guidRegex =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return guidRegex.test(str);
  }

  generateRequestId() {
    return "req_" + Date.now() + "_" + Math.random().toString(36).substr(2, 9);
  }

  // Event monitoring setup - 延迟初始化以确保connection可用
  setupEventMonitoring() {
    // 定义事件类型和处理器，但不立即注册
    const eventTypes = [
      "ReceiveGameState",
      "ReceiveError",
      "CardPlayed",
      "CardSummoned",
      "TurnStarted",
      "TurnEnded",
      "AttackDeclared",
      "DamageDealt",
      "UnitDestroyed",
      "CardVanished",
      "UnitEvolved",
      "GameOver",
      "PpUpdate",
      "EpUpdate",
      "HandUpdated",
      "DeckUpdate",
      "GraveyardUpdate",
      "HealthChanged",
      "StatChange",
    ];

    eventTypes.forEach((eventType) => {
      const handler = (eventData) => {
        if (this.eventMonitoring) {
          this.output(
            `[事件] ${eventType}: ${JSON.stringify(eventData, null, 2)}`,
            "info",
          );
        }
      };
      this.eventHandlers.set(eventType, handler);
    });

    // 添加连接状态检查方法
    this.checkConnectionAndSetupEvents();
  }

  // 检查连接状态并设置事件监听
  checkConnectionAndSetupEvents() {
    const connection = this.getConnection();
    // 如果connection还未准备好，延迟重试
    if (!connection) {
      setTimeout(() => this.checkConnectionAndSetupEvents(), 1000);
      return;
    }

    // 连接可用时的初始化逻辑
    this.output("SignalR连接已就绪，事件监听器已准备", "success");
    this.attachConditionTraceListener();
    this.attachGameStateListener();
    this.ensureTraceMonitor();
  }

  ensureTraceMonitor() {
    if (this.traceMonitorTimer) {
      return;
    }
    this.traceMonitorTimer = window.setInterval(() => {
      this.attachConditionTraceListener();
      this.attachGameStateListener();
    }, 1000);
  }

  attachConditionTraceListener() {
    const connection = this.getConnection();
    if (!connection) {
      return;
    }

    if (
      this.traceListenerConnection &&
      this.traceListenerConnection !== connection &&
      this.traceListener
    ) {
      this.traceListenerConnection.off(
        "ConditionTraceFailed",
        this.traceListener.failed,
      );
      this.traceListenerConnection.off(
        "ConditionTraceSucceeded",
        this.traceListener.succeeded,
      );
    }

    if (this.traceListenerConnection === connection && this.traceListener) {
      return;
    }

    this.traceListener = {
      failed: (payload) => this.renderTraceEntry(payload, false),
      succeeded: (payload) => this.renderTraceEntry(payload, true),
    };
    connection.on("ConditionTraceFailed", this.traceListener.failed);
    connection.on("ConditionTraceSucceeded", this.traceListener.succeeded);
    this.traceListenerConnection = connection;
  }

  attachGameStateListener() {
    const connection = this.getConnection();
    if (!connection) {
      return;
    }

    if (
      this.gameStateListenerConnection &&
      this.gameStateListenerConnection !== connection &&
      this.gameStateListener
    ) {
      this.gameStateListenerConnection.off(
        "ReceiveGameState",
        this.gameStateListener,
      );
    }

    if (this.gameStateListenerConnection === connection && this.gameStateListener) {
      return;
    }

    this.gameStateListener = (payload) => this.handleReceiveGameState(payload);
    connection.on("ReceiveGameState", this.gameStateListener);
    this.gameStateListenerConnection = connection;
  }

  handleReceiveGameState(payload) {
    if (!payload) {
      return;
    }

    const gameId = payload.GameId || payload.gameId || null;
    if (!gameId) {
      return;
    }

    if (!this.lastGameId || this.lastGameId !== gameId) {
      this.resetTraceUiForNewGame();
    }

    this.lastGameId = gameId;
  }

  resetTraceUiForNewGame() {
    this.clearTraceLogs();
    void this.refreshTraceState();
  }

  clearTraceLogs() {
    if (!this.traceListEl) return;
    this.traceListEl.innerHTML =
      '<div class="console-trace-empty">暂无解析日志</div>';
  }

  renderTraceEntry(payload, successOverride = null) {
    if (!this.traceListEl || !payload) {
      return;
    }

    const cardName = payload.CardName || payload.cardName || "未知卡牌";
    const abilityName =
      payload.AbilityName || payload.abilityName || "未知能力";
    const cardId = payload.CardId || payload.cardId || "";
    const traceKindRaw = payload.TraceKind || payload.traceKind || "Condition";
    const traceKind = String(traceKindRaw || "Condition").toLowerCase();
    const success =
      typeof payload.Success === "boolean"
        ? payload.Success
        : successOverride === true;
    const targetCount =
      typeof payload.TargetCount === "number"
        ? payload.TargetCount
        : payload.targetCount;
    const effectType = payload.EffectType || payload.effectType || "";
    const traceKindLabel =
      traceKind === "target"
        ? "目标解析"
        : traceKind === "effect"
          ? "效果参数"
          : "条件解析";
    const conditionsRaw = payload.Conditions || payload.conditions || [];
    const fallbackExpression = payload.Expression || payload.expression || "";
    const fallbackTrace = payload.Trace || payload.trace || "";

    const conditions = Array.isArray(conditionsRaw) ? conditionsRaw : [];
    const entries =
      conditions.length > 0
        ? conditions
        : [
          {
            Expression: fallbackExpression,
            Trace: fallbackTrace,
          },
        ];

    if (this.traceListEl.querySelector(".console-trace-empty")) {
      this.traceListEl.innerHTML = "";
    }

    const entryEl = document.createElement("div");
    entryEl.className = `trace-entry ${success ? "is-success" : "is-failure"}`;

    const summary = document.createElement("div");
    summary.className = "trace-summary";
    summary.innerHTML = `
            <div class="trace-summary-main">
                <span class="trace-card-name">${cardName}</span>
                <span class="trace-symbol">发动</span>
                <span class="trace-ability-name">${abilityName}</span>
                <span class="trace-kind trace-kind-${traceKind}">${traceKindLabel}</span>
                <span class="trace-symbol">${success ? "成功" : "失败"}</span>
            </div>
            <div class="trace-result">${success ? "成功" : "失败"}</div>
        `;

    const meta = document.createElement("div");
    meta.className = "trace-meta";
    const metaLines = [];
    metaLines.push(cardId ? `卡牌ID：${cardId}` : "卡牌ID：未知");
    if (traceKind === "target" && Number.isFinite(targetCount)) {
      metaLines.push(`目标数量：${targetCount}`);
    }
    if (traceKind === "effect" && effectType) {
      metaLines.push(`效果类型：${effectType}`);
    }
    meta.innerHTML = metaLines.map((line) => `<span>${line}</span>`).join("");

    const details = document.createElement("div");
    details.className = "trace-details";

    if (!entries.length || (!entries[0].Expression && !entries[0].Trace)) {
      const placeholder = document.createElement("div");
      placeholder.className = "trace-expression";
      placeholder.textContent = "无解析记录";
      details.appendChild(placeholder);
    } else {
      entries.forEach((entry, index) => {
        const expression = entry.Expression || entry.expression || "(空表达式)";
        const trace = entry.Trace || entry.trace || "";

        const exprBlock = document.createElement("div");
        exprBlock.className = "trace-expression";

        const header = document.createElement("div");
        header.className = "trace-expression-header";
        header.textContent = `表达式 ${index + 1}（点击展开解析值）`;

        const code = document.createElement("div");
        code.className = "trace-expression-code";
        code.textContent = expression;

        const traceEl = document.createElement("div");
        traceEl.className = "trace-expression-trace";
        const values = this.extractTraceValues(trace);
        const valuesList = document.createElement("ul");
        valuesList.className = "trace-expression-values";
        if (values.length === 0) {
          const emptyItem = document.createElement("li");
          emptyItem.textContent = "无动态字段";
          valuesList.appendChild(emptyItem);
        } else {
          values.forEach(({ key, value }) => {
            const item = document.createElement("li");
            const keyEl = document.createElement("span");
            keyEl.className = "value-key";
            keyEl.textContent = key;
            const arrowEl = document.createElement("span");
            arrowEl.className = "value-arrow";
            arrowEl.textContent = "→";
            const valueEl = document.createElement("span");
            valueEl.className = "value-result";
            valueEl.textContent = value;
            item.appendChild(keyEl);
            item.appendChild(arrowEl);
            item.appendChild(valueEl);
            valuesList.appendChild(item);
          });
        }
        traceEl.appendChild(valuesList);

        code.addEventListener("click", (event) => {
          event.stopPropagation();
          exprBlock.classList.toggle("is-open");
        });

        exprBlock.appendChild(header);
        exprBlock.appendChild(code);
        exprBlock.appendChild(traceEl);
        details.appendChild(exprBlock);
      });
    }

    entryEl.appendChild(summary);
    entryEl.appendChild(meta);
    entryEl.appendChild(details);
    entryEl.addEventListener("click", () => {
      entryEl.classList.toggle("is-open");
    });

    this.traceListEl.prepend(entryEl);
  }

  // Show event monitoring status
  showEventStatus() {
    const connection = this.getConnection();
    this.output("事件监听状态:", "info");
    this.output(
      `监听状态: ${this.eventMonitoring ? "开启" : "关闭"}`,
      "output",
    );
    this.output(
      `连接状态: ${connection ? connection.state : "未连接"}`,
      "output",
    );
    this.output(`已注册事件类型: ${this.eventHandlers.size}`, "output");

    if (this.eventHandlers.size > 0) {
      this.output("监听的事件类型:", "output");
      for (const eventType of this.eventHandlers.keys()) {
        this.output(`  - ${eventType}`, "output");
      }
    }
  }

  // 检查连接状态
  checkConnectionStatus() {
    const connection = this.getConnection();
    this.output("=== SignalR连接状态检查 ===", "info");

    if (!connection) {
      this.output("❌ 连接对象: 未初始化", "error");
      this.output("提示: 请等待游戏完全加载后再使用控制台", "info");
      return;
    }

    this.output(`连接对象: 已初始化`, "success");
    this.output(
      `连接状态: ${connection.state}`,
      connection.state === "Connected" ? "success" : "warning",
    );

    if (connection.connectionId) {
      this.output(`🆔 连接ID: ${connection.connectionId}`, "output");
    }

    this.output(
      `游戏状态: ${gameState.gameId ? "已加载" : "未加载"}`,
      gameState.gameId ? "success" : "warning",
    );
    this.output(`玩家ID: ${gameState.localPlayerId || "未设置"}`, "output");
  }

  // Toggle event monitoring
  toggleEventMonitoring(args) {
    const connection = this.getConnection();
    if (args.length < 1) {
      this.output("用法: monitor <on|off>", "error");
      return;
    }

    const action = args[0].toLowerCase();

    if (action === "on") {
      if (!connection) {
        this.output("错误: SignalR连接未就绪", "error");
        this.output('请先运行 "connection" 命令检查连接状态', "info");
        return;
      }

      if (connection.state !== "Connected") {
        this.output(
          `错误: 连接状态为 ${connection.state}，需要 Connected 状态`,
          "error",
        );
        return;
      }

      this.eventMonitoring = true;

      // Register event handlers
      for (const [eventType, handler] of this.eventHandlers) {
        connection.on(eventType, handler);
      }

      this.output("✅ 事件监听已开启", "success");
      this.output(`正在监听 ${this.eventHandlers.size} 种事件类型`, "info");
    } else if (action === "off") {
      this.eventMonitoring = false;

      // Unregister event handlers
      if (connection) {
        for (const [eventType, handler] of this.eventHandlers) {
          connection.off(eventType, handler);
        }
      }

      this.output("⏹️ 事件监听已关闭", "success");
    } else {
      this.output("无效参数。使用 on 或 off", "error");
    }
  }
}

// 创建全局实例
let devConsole = null;

// 初始化开发者控制台
export function initDevConsole(enable = false, autoShow = false, getConnection) {
  if (!enable) {
    return null;
  }

  if (!devConsole) {
    devConsole = new DevConsole(getConnection, autoShow);

    // 添加到全局作用域以便在浏览器控制台中访问
    window.devConsole = devConsole;
  }
  return devConsole;
}

// 导出实例
export { devConsole };
