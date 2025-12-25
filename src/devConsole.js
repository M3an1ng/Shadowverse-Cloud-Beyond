// 游戏内开发者控制台
// 在游戏界面中提供调试和测试功能
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - 配置管理系统: 文档/05-游戏系统/09-配置管理系统.md
//

import { gameState } from './gameManager.js';
import { connection } from './main.js';

class DevConsole {
    constructor(autoShow = false) {
        this.isVisible = false;
        this.commandHistory = [];
        this.historyIndex = -1;
        this.commands = new Map();
        this.eventMonitoring = false;
        this.eventHandlers = new Map();
        this.autoShow = autoShow;
        this.debugCommandSchemas = {
            damage: { required: ['target', 'amount'], optional: ['source'] },
            heal: { required: ['target', 'amount'], optional: ['source'] },
            'damage-target': { required: ['target', 'amount'], optional: [] },
            destroy: { required: ['target'], optional: ['source'] },
            vanish: { required: ['target'], optional: [] },
            return: { required: ['target'], optional: [] },
            draw: { required: ['player', 'count'], optional: [] },
            mana: { required: ['player', 'delta'], optional: [] },
            pp: { required: ['player', 'current'], optional: ['max'] },
            ep: { required: ['player', 'current'], optional: ['max'] },
            'add-card': { required: ['player', 'card', 'location'], optional: [] },
            event: { required: ['name'], optional: ['payload'] },
        };
        this.initCommands();
        this.createConsoleUI();
        this.bindEvents();
        this.setupEventMonitoring();
    }

    // 初始化命令
    initCommands() {
        const legacy = (handler) => (ctx) => handler(ctx.positional || []);

        this.commands.set('help', { description: '显示所有可用命令', execute: () => this.showHelp() });
        this.commands.set('clear', { description: '清空控制台', execute: () => this.clearOutput() });
        this.commands.set('gamestate', { description: '显示当前游戏状态', execute: () => this.showGameState() });
        this.commands.set('status', { description: '显示状态摘要 (status [player=p1|p2])', execute: (ctx) => this.showStatus(ctx) });
        this.commands.set('connection', { description: '检查SignalR连接状态', execute: () => this.checkConnectionStatus() });

        // 游戏操作（沿用旧接口）
        this.commands.set('play', { description: '出牌 - 用法: play <cardId> [targetUid]', execute: legacy((args) => this.playCard(args)) });
        this.commands.set('attack', { description: '攻击 - 用法: attack <attackerUid> <targetUid>', execute: legacy((args) => this.attack(args)) });
        this.commands.set('evolve', { description: '进化 - 用法: evolve <unitUid>', execute: legacy((args) => this.evolve(args)) });
        this.commands.set('endturn', { description: '结束回合', execute: () => this.endTurn() });

        // 事件注入/调试指令（KV 语法）
        this.commands.set('damage', { description: 'damage target=<cardId> amount=<int> [source=<cardId|p1|p2>]', execute: (ctx) => this.sendDebugCommand('damage', ctx) });
        this.commands.set('heal', { description: 'heal target=<cardId> amount=<int> [source=<cardId|p1|p2>]', execute: (ctx) => this.sendDebugCommand('heal', ctx) });
        this.commands.set('kill', { description: 'kill target=<cardId>', execute: (ctx) => this.sendDebugCommand('kill', ctx) });
        this.commands.set('destroy', { description: 'destroy target=<cardId>', execute: (ctx) => this.sendDebugCommand('destroy', ctx) });
        this.commands.set('vanish', { description: 'vanish target=<cardId>', execute: (ctx) => this.sendDebugCommand('vanish', ctx) });
        this.commands.set('return', { description: 'return target=<cardId>', execute: (ctx) => this.sendDebugCommand('return', ctx) });
        this.commands.set('draw', { description: 'draw player=<p1|p2> count=<int>', execute: (ctx) => this.sendDebugCommand('draw', ctx) });
        this.commands.set('mana', { description: 'mana player=<p1|p2> delta=<int>', execute: (ctx) => this.sendDebugCommand('mana', ctx) });
        this.commands.set('add-card', { description: 'add-card player=<p1|p2> card=<cardDefId> location=hand|board', execute: (ctx) => this.sendDebugCommand('add-card', ctx) });
        this.commands.set('event', { description: 'event name=<EventType> payload=<json>', execute: (ctx) => this.sendDebugCommand('event', ctx) });

        // 事件监听
        this.commands.set('events', { description: '显示事件监听状态', execute: () => this.showEventStatus() });
        this.commands.set('monitor', { description: '开启/关闭事件监听 - 用法: monitor <on|off>', execute: (ctx) => this.toggleEventMonitoring(ctx.positional || []) });
    }

    // 创建控制台UI
    createConsoleUI() {
        const consoleHTML = `
            <div id="dev-console" class="dev-console hidden">
                <div class="console-header">
                    <span>🛠️ 开发者控制台</span>
                    <button id="console-close" class="console-close">×</button>
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
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', consoleHTML);

        // 添加CSS样式
        const style = document.createElement('style');
        style.textContent = `
            /* Override global devtools.css (#dev-console) to keep a compact centered console */
            #dev-console {
                position: fixed;
                left: 50%;
                top: 50%;
                transform: translate(-50%, -50%);
                width: min(1100px, calc(100vw - 24px));
                height: 320px;
                background: rgba(0, 0, 0, 0.92);
                border: 1px solid #333;
                border-radius: 10px;
                color: #00ff00;
                font-family: 'Courier New', monospace;
                font-size: 12px;
                z-index: 10000;
                display: flex;
                flex-direction: column;
                box-shadow: 0 8px 30px rgba(0, 0, 0, 0.55);
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
                background: #333;
                padding: 8px 12px;
                border-bottom: 1px solid #555;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-radius: 8px 8px 0 0;
            }

            .console-close {
                background: none;
                border: none;
                color: #ff6b6b;
                font-size: 16px;
                cursor: pointer;
                padding: 0;
                width: 20px;
                height: 20px;
            }

            .console-close:hover {
                background: #ff6b6b;
                color: white;
                border-radius: 50%;
            }

            .console-output {
                flex: 1;
                padding: 8px;
                overflow-y: auto;
                background: rgba(0, 0, 0, 0.8);
            }

            .console-output::-webkit-scrollbar {
                width: 6px;
            }

            .console-output::-webkit-scrollbar-track {
                background: #333;
            }

            .console-output::-webkit-scrollbar-thumb {
                background: #666;
                border-radius: 3px;
            }

            .console-input-container {
                display: flex;
                align-items: center;
                padding: 8px;
                background: #222;
                border-radius: 0 0 8px 8px;
            }

            .console-prompt {
                color: #00ff00;
                margin-right: 8px;
            }

            .console-input {
                flex: 1;
                background: transparent;
                border: none;
                color: #00ff00;
                font-family: inherit;
                font-size: inherit;
                outline: none;
            }

            .console-toggle {
                position: fixed;
                top: 10px;
                right: 10px;
                background: rgba(0, 0, 0, 0.7);
                border: 1px solid #333;
                color: #00ff00;
                padding: 8px 12px;
                border-radius: 4px;
                cursor: pointer;
                z-index: 9999;
                font-size: 14px;
            }

            .console-toggle:hover {
                background: rgba(0, 0, 0, 0.9);
            }

            .console-line {
                margin: 2px 0;
                word-wrap: break-word;
            }

            .console-command {
                color: #00ff00;
            }

            .console-output-text {
                color: #ffffff;
            }

            .console-error {
                color: #ff6b6b;
            }

            .console-success {
                color: #51cf66;
            }

            .console-info {
                color: #74c0fc;
            }

            .console-welcome {
                color: #ffd43b;
                font-style: italic;
            }

            /* 响应式设计 */
            @media (max-width: 768px) {
                #dev-console {
                    width: calc(100vw - 16px);
                    height: 280px;
                    top: 50%;
                }
            }
        `;
        document.head.appendChild(style);
    }

    getCommandUsage(commandName) {
        const cmd = this.commands.get(commandName);
        return cmd?.description ? `用法: ${cmd.description}` : null;
    }

    validateDebugCommand(commandName, ctx) {
        const schema = this.debugCommandSchemas[commandName];
        if (!schema) {
            return { ok: false, message: `未支持的调试指令: ${commandName}` };
        }

        const positional = ctx?.positional || [];
        if (positional.length > 0) {
            return { ok: false, message: `不支持位置参数: ${positional.join(' ')}` };
        }

        const kv = ctx?.kv || {};
        const keys = Object.keys(kv);
        const allowed = new Set([...(schema.required || []), ...(schema.optional || [])]);
        const unknown = keys.find(k => !allowed.has(k));
        if (unknown) {
            return { ok: false, message: `不支持参数: ${unknown}` };
        }

        const missing = (schema.required || []).find(k => !kv[k] || String(kv[k]).trim() === '');
        if (missing) {
            return { ok: false, message: `缺少参数: ${missing}` };
        }

        const validateGuid = (value) => this.isValidGuid(String(value || '').trim());
        const validateInt = (value) => /^[-+]?\d+$/.test(String(value || '').trim());
        const validatePlayerToken = (value) => {
            const v = String(value || '').trim().toLowerCase();
            return v === 'p1' || v === 'p2';
        };
        const validateSourceToken = (value) => {
            const v = String(value || '').trim().toLowerCase();
            return v === 'p1' || v === 'p2' || validateGuid(v);
        };

        if (kv.target && !validateGuid(kv.target)) return { ok: false, message: 'target 必须是有效 GUID' };
        if (kv.amount && !validateInt(kv.amount)) return { ok: false, message: 'amount 必须是整数' };
        if (kv.count && !validateInt(kv.count)) return { ok: false, message: 'count 必须是整数' };
        if (kv.delta && !validateInt(kv.delta)) return { ok: false, message: 'delta 必须是整数' };
        if (kv.current && !validateInt(kv.current)) return { ok: false, message: 'current 必须是整数' };
        if (kv.max && !validateInt(kv.max)) return { ok: false, message: 'max 必须是整数' };
        if (kv.player && !validatePlayerToken(kv.player)) return { ok: false, message: 'player 仅支持 p1 或 p2' };
        if (kv.source && !validateSourceToken(kv.source)) return { ok: false, message: 'source 仅支持 p1/p2 或 GUID' };
        if (kv.card && !validateInt(kv.card)) return { ok: false, message: 'card 必须是整数卡牌定义ID' };
        if (kv.location) {
            const loc = String(kv.location || '').trim().toLowerCase();
            if (!['hand', 'board', 'field', 'deck'].includes(loc)) return { ok: false, message: 'location 仅支持 hand/board/field/deck' };
        }

        return { ok: true };
    }

    // 绑定事件
    bindEvents() {
        const closeBtn = document.getElementById('console-close');
        const input = document.getElementById('console-input');

        if (closeBtn) {
            closeBtn.addEventListener('click', () => this.hide());
        }

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                this.executeCommand(input.value.trim());
                input.value = '';
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                this.navigateHistory(-1);
            } else if (e.key === 'ArrowDown') {
                e.preventDefault();
                this.navigateHistory(1);
            }
        });

        // 全局快捷键 (Ctrl + `)
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === '`') {
                e.preventDefault();
                this.toggle();
            }
        });
    }

    // 显示/隐藏控制台
    toggle() {
        this.isVisible = !this.isVisible;
        const console = document.getElementById('dev-console');
        
        if (this.isVisible) {
            console.classList.remove('hidden');
            const input = document.getElementById('console-input');
            if (input) input.focus();
        } else {
            console.classList.add('hidden');
        }
    }

    hide() {
        this.isVisible = false;
        const console = document.getElementById('dev-console');
        if (console) console.classList.add('hidden');
    }

    show() {
        this.isVisible = true;
        const consoleEl = document.getElementById('dev-console');
        if (consoleEl) {
            consoleEl.classList.remove('hidden');
        }
        const input = document.getElementById('console-input');
        if (input) input.focus();
    }

    // 输出文本到控制台
    output(text, type = 'output') {
        const outputDiv = document.getElementById('console-output');
        const line = document.createElement('div');
        line.className = `console-line console-${type}`;
        line.textContent = text;
        outputDiv.appendChild(line);
        outputDiv.scrollTop = outputDiv.scrollHeight;
    }

    // 清空输出
    clearOutput() {
        document.getElementById('console-output').innerHTML = '';
        this.output('控制台已清空', 'info');
    }

    // 执行命令
    executeCommand(commandLine) {
        if (!commandLine) return;

        this.commandHistory.push(commandLine);
        this.historyIndex = this.commandHistory.length;

        this.output(`> ${commandLine}`, 'command');

        const parsed = this.parseCommandLine(commandLine);
        const cmd = parsed && parsed.name ? this.commands.get(parsed.name) : null;

        if (cmd) {
            try {
                cmd.execute(parsed);
            } catch (error) {
                this.output(`错误: ${error.message}`, 'error');
            }
        } else {
            this.output(`未知命令: ${parsed.name || '(空)'}。输入 'help' 查看可用命令。`, 'error');
        }
    }

    parseCommandLine(commandLine) {
        const tokens = [];
        let current = '';
        let inQuotes = false;
        for (let i = 0; i < commandLine.length; i++) {
            const ch = commandLine[i];
            if (ch === '"') {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch === ' ' && !inQuotes) {
                if (current.length > 0) {
                    tokens.push(current);
                    current = '';
                }
            } else {
                current += ch;
            }
        }
        if (current.length > 0) tokens.push(current);

        if (tokens.length === 0) {
            return { name: '', kv: {}, positional: [], raw: [] };
        }

        const name = tokens.shift().toLowerCase();
        const kv = {};
        const positional = [];

        tokens.forEach(tok => {
            const eq = tok.indexOf('=');
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
        const input = document.getElementById('console-input');
        
        if (direction === -1 && this.historyIndex > 0) {
            this.historyIndex--;
            input.value = this.commandHistory[this.historyIndex];
        } else if (direction === 1 && this.historyIndex < this.commandHistory.length - 1) {
            this.historyIndex++;
            input.value = this.commandHistory[this.historyIndex];
        } else if (direction === 1 && this.historyIndex === this.commandHistory.length - 1) {
            this.historyIndex = this.commandHistory.length;
            input.value = '';
        }
    }

    // 显示帮助
    showHelp() {
        this.output('=== 🛠️ 开发者控制台帮助 ===', 'info');
        this.output('', 'output');

        // 分类显示命令
        this.output('📋 基础命令:', 'info');
        this.output('  help - 显示此帮助信息', 'output');
        this.output('  clear - 清空控制台输出', 'output');
        this.output('  connection - 检查SignalR连接状态', 'output');
        this.output('  gamestate/status - 显示当前游戏状态', 'output');
        this.output('', 'output');

        this.output('🎮 游戏操作 (旧接口):', 'info');
        this.output('  play <cardUid> [targetUid] - 出牌', 'output');
        this.output('  attack <attackerUid> <targetUid> - 攻击', 'output');
        this.output('  evolve <unitUid> - 进化单位', 'output');
        this.output('  endturn - 结束当前回合', 'output');
        this.output('', 'output');

        this.output('🧪 事件调试 (KV 语法):', 'info');
        this.output('  damage target=<cardId> amount=<int> [source=<cardId|p1|p2>]', 'output');
        this.output('  heal target=<cardId> amount=<int> [source=<cardId|p1|p2>]', 'output');
        this.output('  kill|destroy target=<cardId>', 'output');
        this.output('  vanish target=<cardId>', 'output');
        this.output('  return target=<cardId>', 'output');
        this.output('  draw player=<p1|p2> count=<int>', 'output');
        this.output('  mana player=<p1|p2> delta=<int>', 'output');
        this.output('  add-card player=<p1|p2> card=<cardDefId> location=hand|board', 'output');
        this.output('  event name=<EventType> payload=<json>', 'output');
        this.output('', 'output');

        this.output('🔍 调试工具:', 'info');
        this.output('  events - 显示事件监听状态', 'output');
        this.output('  monitor <on|off> - 开启/关闭事件监听', 'output');
        this.output('', 'output');

        this.output('⌨️ 快捷键:', 'info');
        this.output('  Ctrl + ` - 切换控制台显示/隐藏', 'output');
        this.output('  ↑↓ 键 - 浏览命令历史', 'output');
        this.output('', 'output');

        this.output('💡 使用提示:', 'info');
        this.output('  1. 首先运行 "connection" 检查连接状态', 'output');
        this.output('  2. 使用 "gamestate" 或 "status" 查看当前游戏信息', 'output');
        this.output('  3. 使用 "monitor on" 开启事件监听以调试', 'output');
    }

    // 显示游戏状态
    showGameState() {
        this.output('当前游戏状态:', 'info');
        this.output(`游戏ID: ${gameState.gameId || '未设置'}`, 'output');
        this.output(`玩家ID: ${gameState.localPlayerId || '未设置'}`, 'output');
        this.output(`游戏阶段: ${gameState.gamePhase}`, 'output');
        this.output(`回合数: ${gameState.turnNumber}`, 'output');
        this.output(`当前玩家: ${gameState.currentPlayerId || '未设置'}`, 'output');
        
        if (gameState.activePlayer) {
            this.output(`手牌数量: ${gameState.activePlayer.hand?.length || 0}`, 'output');
            this.output(`场上单位: ${gameState.activePlayer.field?.length || 0}`, 'output');
            this.output(`当前PP: ${gameState.activePlayer.currentPP || 0}/${gameState.activePlayer.maxPP || 0}`, 'output');
            this.output(`当前EP: ${gameState.activePlayer.currentEP || 0}/${gameState.activePlayer.maxEP || 0}`, 'output');
        }
    }

    showStatus(ctx) {
        const player = ctx.kv.player;
        this.output('=== 状态摘要 ===', 'info');
        const players = gameState.players || [];
        const maybePrint = (p, label) => {
            if (!p) {
                this.output(`${label}: 未知`, 'output');
                return;
            }
            const hand = Array.isArray(p.hand) ? p.hand.length : 0;
            const field = Array.isArray(p.field) ? p.field.length : 0;
            const pp = `${p.currentPP ?? '-'} / ${p.maxPP ?? '-'}`;
            const ep = `${p.currentEP ?? '-'} / ${p.maxEP ?? '-'}`;
            this.output(`${label}: 手牌=${hand}, 场上=${field}, PP=${pp}, EP=${ep}`, 'output');
        };

        if (!player || player === 'p1') {
            maybePrint(players[0], '[P1]');
        }
        if (!player || player === 'p2') {
            maybePrint(players[1], '[P2]');
        }
    }

    // 出牌命令
    async playCard(args) {
        if (args.length < 1) {
            this.output('用法: play <cardUid> [targetUid]', 'error');
            return;
        }

        const cardUid = args[0];
        const targetUid = args[1] || null;

        // Validate GUID format
        if (!this.isValidGuid(cardUid)) {
            this.output(`无效的卡牌UID格式: ${cardUid}`, 'error');
            return;
        }

        try {
            if (connection && connection.state === 'Connected') {
                // Use current PlayCardActionDto structure
                const actionDto = {
                    CardUid: cardUid,
                    TargetUid: targetUid ? targetUid : null
                };

                await connection.invoke('PlayCard', actionDto);
                this.output(`出牌命令已发送: 卡牌UID ${cardUid}`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`出牌失败: ${error.message}`, 'error');
        }
    }

    // 攻击命令
    async attack(args) {
        if (args.length < 2) {
            this.output('用法: attack <attackerUid> <targetUid>', 'error');
            return;
        }

        const attackerUid = args[0];
        const targetUid = args[1];

        // Validate GUID formats
        if (!this.isValidGuid(attackerUid) || !this.isValidGuid(targetUid)) {
            this.output(`无效的UID格式 - 攻击者: ${attackerUid}, 目标: ${targetUid}`, 'error');
            return;
        }

        try {
            if (connection && connection.state === 'Connected') {
                // Use current AttackActionDto structure
                const actionDto = {
                    AttackerUid: attackerUid,
                    TargetUid: targetUid
                };

                await connection.invoke('PerformAttack', actionDto);
                this.output(`攻击命令已发送: ${attackerUid} -> ${targetUid}`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`攻击失败: ${error.message}`, 'error');
        }
    }

    // 进化命令
    async evolve(args) {
        if (args.length < 1) {
            this.output('用法: evolve <unitUid>', 'error');
            return;
        }

        const unitUid = args[0];

        // Validate GUID format
        if (!this.isValidGuid(unitUid)) {
            this.output(`无效的单位UID格式: ${unitUid}`, 'error');
            return;
        }

        try {
            if (connection && connection.state === 'Connected') {
                // Use current EvolveActionDto structure
                const actionDto = {
                    UnitUid: unitUid
                };

                await connection.invoke('PerformEvolve', actionDto);
                this.output(`进化命令已发送: ${unitUid}`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`进化失败: ${error.message}`, 'error');
        }
    }

    // 结束回合
    async endTurn() {
        try {
            if (connection && connection.state === 'Connected') {
                // Generate unique request ID for idempotency
                const requestId = this.generateRequestId();
                await connection.invoke('EndTurn', requestId);
                this.output(`回合结束命令已发送 (请求ID: ${requestId})`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`结束回合失败: ${error.message}`, 'error');
        }
    }

    // 调试指令统一入口（需要后端支持 ExecuteDebugCommand）
    async sendDebugCommand(commandName, ctx) {
        if (!ctx || !ctx.kv) {
            this.output('无效的命令参数', 'error');
            return;
        }

        const validation = this.validateDebugCommand(commandName, ctx);
        if (!validation.ok) {
            this.output(`参数错误: ${validation.message}`, 'error');
            const usage = this.getCommandUsage(commandName);
            if (usage) this.output(usage, 'info');
            return;
        }

        if (!connection || connection.state !== 'Connected') {
            this.output('未连接到服务器，无法发送调试指令', 'error');
            return;
        }

        const payload = { Command: commandName, Args: ctx.kv };
        try {
            await connection.invoke('ExecuteDebugCommand', payload);
            this.output(`已发送调试指令: ${commandName} ${JSON.stringify(ctx.kv)}`, 'success');
        } catch (err) {
            this.output(`调试指令执行失败或未实现: ${err.message}`, 'error');
        }
    }

    // Helper functions
    isValidGuid(str) {
        if (!str || typeof str !== 'string') {
            return false;
        }
        const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
        return guidRegex.test(str);
    }

    generateRequestId() {
        return 'req_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }

    // Event monitoring setup - 延迟初始化以确保connection可用
    setupEventMonitoring() {
        // 定义事件类型和处理器，但不立即注册
        const eventTypes = [
            'ReceiveGameState',
            'ReceiveError',
            'CardPlayed',
            'CardSummoned',
            'TurnStarted',
            'TurnEnded',
            'AttackDeclared',
            'DamageDealt',
            'UnitDestroyed',
            'UnitEvolved',
            'GameOver',
            'PpUpdate',
            'EpUpdate',
            'HandUpdated',
            'DeckUpdate',
            'GraveyardUpdate',
            'HealthChanged',
            'StatChange'
        ];

        eventTypes.forEach(eventType => {
            const handler = (eventData) => {
                if (this.eventMonitoring) {
                    this.output(`[事件] ${eventType}: ${JSON.stringify(eventData, null, 2)}`, 'info');
                }
            };
            this.eventHandlers.set(eventType, handler);
        });

        // 添加连接状态检查方法
        this.checkConnectionAndSetupEvents();
    }

    // 检查连接状态并设置事件监听
    checkConnectionAndSetupEvents() {
        // 如果connection还未准备好，延迟重试
        if (!connection) {
            setTimeout(() => this.checkConnectionAndSetupEvents(), 1000);
            return;
        }

        // 连接可用时的初始化逻辑
        this.output('🔗 SignalR连接已就绪，事件监听器已准备', 'success');
    }

    // Show event monitoring status
    showEventStatus() {
        this.output('事件监听状态:', 'info');
        this.output(`监听状态: ${this.eventMonitoring ? '开启' : '关闭'}`, 'output');
        this.output(`连接状态: ${connection ? connection.state : '未连接'}`, 'output');
        this.output(`已注册事件类型: ${this.eventHandlers.size}`, 'output');

        if (this.eventHandlers.size > 0) {
            this.output('监听的事件类型:', 'output');
            for (const eventType of this.eventHandlers.keys()) {
                this.output(`  - ${eventType}`, 'output');
            }
        }
    }

    // 检查连接状态
    checkConnectionStatus() {
        this.output('=== SignalR连接状态检查 ===', 'info');

        if (!connection) {
            this.output('❌ 连接对象: 未初始化', 'error');
            this.output('💡 提示: 请等待游戏完全加载后再使用控制台', 'info');
            return;
        }

        this.output(`🔗 连接对象: 已初始化`, 'success');
        this.output(`📡 连接状态: ${connection.state}`, connection.state === 'Connected' ? 'success' : 'warning');

        if (connection.connectionId) {
            this.output(`🆔 连接ID: ${connection.connectionId}`, 'output');
        }

        this.output(`🎮 游戏状态: ${gameState.gameId ? '已加载' : '未加载'}`, gameState.gameId ? 'success' : 'warning');
        this.output(`👤 玩家ID: ${gameState.localPlayerId || '未设置'}`, 'output');
    }

    // Toggle event monitoring
    toggleEventMonitoring(args) {
        if (args.length < 1) {
            this.output('用法: monitor <on|off>', 'error');
            return;
        }

        const action = args[0].toLowerCase();

        if (action === 'on') {
            if (!connection) {
                this.output('错误: SignalR连接未就绪', 'error');
                this.output('💡 请先运行 "connection" 命令检查连接状态', 'info');
                return;
            }

            if (connection.state !== 'Connected') {
                this.output(`错误: 连接状态为 ${connection.state}，需要 Connected 状态`, 'error');
                return;
            }

            this.eventMonitoring = true;

            // Register event handlers
            for (const [eventType, handler] of this.eventHandlers) {
                connection.on(eventType, handler);
            }

            this.output('✅ 事件监听已开启', 'success');
            this.output(`📡 正在监听 ${this.eventHandlers.size} 种事件类型`, 'info');
        } else if (action === 'off') {
            this.eventMonitoring = false;

            // Unregister event handlers
            if (connection) {
                for (const [eventType, handler] of this.eventHandlers) {
                    connection.off(eventType, handler);
                }
            }

            this.output('⏹️ 事件监听已关闭', 'success');
        } else {
            this.output('无效参数。使用 on 或 off', 'error');
        }
    }
}

// 创建全局实例
let devConsole = null;

// 初始化开发者控制台
export function initDevConsole(enable = false, autoShow = false) {
    if (!enable) {
        console.log('Dev console init skipped (DEBUG disabled).');
        return null;
    }

    if (!devConsole) {
        devConsole = new DevConsole(autoShow);
        
        // 添加到全局作用域以便在浏览器控制台中访问
        window.devConsole = devConsole;
        
        console.log('🛠️ 开发者控制台已加载! 按 Ctrl + ` 打开控制台');
    }
    return devConsole;
}

// 导出实例
export { devConsole };
