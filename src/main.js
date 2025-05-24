// src/main.js - 主入口模块
console.log("main.js loaded");

// 导入核心模块
import { initGame, gameState } from './gameManager.js'; // 需要导入 gameState 以传递给 renderGame
import { loadCards } from './cardManager.js';     // [修正] 修正导入的函数名 (原为 loadCardData)
import { renderGame } from './ui.js';
// import { endTurn } from './turnManager.js';         // 回合结束功能待实现，保持注释

let isInitializing = false; // ADDED: Initialization guard flag

// 初始化游戏流程 (简化版占位符)
async function initializeApp() {
    if (isInitializing) { // ADDED: Check guard flag
        console.warn("MAIN: Initialization already in progress. Skipping duplicate call.");
        return;
    }
    isInitializing = true; // ADDED: Set guard flag
    console.log("MAIN: Initializing application...");
    try {
        // 1. 加载卡牌数据 (异步)
        console.log("MAIN: Loading card data...");
        await loadCards(); // [修正] 修正函数调用名称 (原为 loadCardData)
        console.log("MAIN: Card data loaded successfully.");

        // 2. 初始化游戏状态
        console.log("MAIN: Initializing game state...");
        initGame(); // 调用 gameManager 的初始化函数
        console.log("MAIN: Game state initialized.");

        // 3. 初始 UI 渲染
        console.log("MAIN: Performing initial UI render...");
        renderGame(gameState); // 调用 ui 的渲染函数，传入初始化后的 gameState
        console.log("MAIN: Initial UI render complete.");

        // 4. 设置核心事件监听器 (例如结束回合按钮)
        // 注意: ui.js 中的 initializeUI 会处理其内部的 DOM 缓存和卡牌点击等监听
        

        console.log("MAIN: Application initialized successfully!");

    } catch (error) {
        console.error("initializeApp: CATCH BLOCK triggered!"); // ADDED
        console.error("MAIN: Failed to initialize application:", error);
        // 可以在 UI 中向用户显示错误信息
        // 例如: document.body.innerHTML = '<h1>游戏初始化失败，请刷新页面重试。</h1>';
    } finally { // ADDED: Ensure flag is reset even on error
        isInitializing = false;
    }
}

// 绑定核心事件监听器
function setupEventListeners() {
    console.log("Setting up event listeners...");
    const endTurnButton = document.getElementById('end-turn-button');
    if (endTurnButton) {
        // 初始时按钮可能是禁用的，但监听器可以先加上
        endTurnButton.addEventListener('click', () => {
            console.log("End Turn button clicked - TODO: Call actual endTurn function");
            // endTurn(); // 在 turnManager 实现并导入后调用
            alert("结束回合按钮被点击！(功能待实现)"); // 临时反馈
        });
        console.log("End Turn button listener attached.");
    } else {
        console.error("Element with ID 'end-turn-button' not found!");
    }

    // TODO: 在 ui.js 中实现卡牌点击等更复杂的事件处理和委托
    // 例如，监听手牌区域的点击事件，由 ui.js 处理并调用 gameManager 的方法
    const playerHandArea = document.getElementById('player1-hand');
    if(playerHandArea) {
        // playerHandArea.addEventListener('click', handleHandCardClick); // handleHandCardClick 在 ui.js 定义
        console.log("Placeholder for hand card click listener.");
    }
}

// 启动应用
initializeApp();