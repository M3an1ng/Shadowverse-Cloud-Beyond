// src/metadataManager.js - 元数据管理器
//
// 相关文档:
// - 元数据驱动UI: 文档/06-UI设计/04-元数据驱动UI架构设计.md
// - 开发实践指南: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

/**
 * 元数据管理器
 * 负责获取、缓存和管理来自后端的元数据
 */
export class MetadataManager {
    constructor() {
        this.metadata = null;
        this.isLoaded = false;
        this.loadPromise = null;
    }

    /**
     * 初始化元数据管理器
     */
    async initialize() {
        if (this.isLoaded) {
            return this.metadata;
        }

        if (this.loadPromise) {
            return await this.loadPromise;
        }

        this.loadPromise = this.loadMetadata();
        return await this.loadPromise;
    }

    /**
     * 从后端加载元数据
     */
    async loadMetadata() {
        try {
            console.log('MetadataManager: Loading metadata from API...');
            
            const response = await fetch('/api/AbilityEditorMetadata');
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            this.metadata = await response.json();
            this.isLoaded = true;
            
            console.log('MetadataManager: Metadata loaded successfully:', this.metadata);
            
            // 验证元数据结构
            this.validateMetadata();
            
            return this.metadata;
        } catch (error) {
            console.error('MetadataManager: Failed to load metadata:', error);
            throw new Error(`无法加载元数据: ${error.message}`);
        }
    }

    /**
     * 验证元数据结构
     */
    validateMetadata() {
        if (!this.metadata) {
            throw new Error('元数据为空');
        }

        const requiredFields = ['effects', 'events', 'dsl', 'queryOperations'];
        for (const field of requiredFields) {
            if (!(field in this.metadata)) {
                console.warn(`MetadataManager: Missing field "${field}" in metadata`);
            }
        }

        console.log('MetadataManager: Metadata validation passed');
    }

    /**
     * 获取效果元数据
     */
    getEffects() {
        this.ensureLoaded();
        return this.metadata.effects || [];
    }

    /**
     * 获取效果类型列表（卡牌编辑器使用的简化结构）
     */
    getEffectTypes() {
        const effects = this.getEffects();
        return (effects || []).map(effect => ({
            typeName: effect.effectTypeName || effect.typeName || effect.name,
            displayName: effect.displayName || effect.effectTypeName || effect.typeName || effect.name,
            description: effect.description || '',
            parameters: effect.parameters || []
        }));
    }

    /**
     * 获取事件元数据
     */
    getEvents() {
        this.ensureLoaded();
        return this.metadata.events || [];
    }

    /**
     * 获取DSL元数据
     */
    getDsl() {
        this.ensureLoaded();
        return this.metadata.dsl || { contextObjects: [], functions: [] };
    }

    /**
     * 获取查询操作元数据
     */
    getQueryOperations() {
        this.ensureLoaded();
        return this.metadata.queryOperations || [];
    }

    /**
     * 根据类型名称获取效果元数据
     */
    getEffectByType(typeName) {
        const effects = this.getEffects();
        return effects.find(effect => effect.effectTypeName === typeName);
    }

    /**
     * 根据事件类型获取事件元数据
     */
    getEventByType(eventType) {
        const events = this.getEvents();
        return events.find(event => event.eventType === eventType);
    }

    /**
     * 获取DSL上下文对象
     */
    getDslContextObjects() {
        const dsl = this.getDsl();
        return dsl.contextObjects || [];
    }

    /**
     * 获取DSL函数
     */
    getDslFunctions() {
        const dsl = this.getDsl();
        return dsl.functions || [];
    }

    /**
     * 根据名称获取DSL上下文对象
     */
    getDslContextObjectByName(name) {
        const contextObjects = this.getDslContextObjects();
        return contextObjects.find(obj => obj.name === name);
    }

    /**
     * 根据名称获取DSL函数
     */
    getDslFunctionByName(name) {
        const functions = this.getDslFunctions();
        return functions.find(func => func.name === name);
    }

    /**
     * 获取所有可用的枚举选项（用于下拉菜单）
     */
    async getEnumOptions(enumName) {
        try {
            const response = await fetch(`/api/localization/enums/${enumName}`);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const enumData = await response.json();
            
            // 转换为选项数组格式
            return Object.entries(enumData).map(([value, label]) => ({
                value,
                label
            }));
        } catch (error) {
            console.error(`MetadataManager: Failed to load enum ${enumName}:`, error);
            return [];
        }
    }

    /**
     * 获取指定枚举的原始本地化映射（key -> label）
     */
    async getEnum(enumName) {
        const response = await fetch(`/api/localization/enums/${enumName}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        return await response.json();
    }

    /**
     * 获取卡牌类型选项
     */
    async getCardTypeOptions() {
        return await this.getEnumOptions('CardType');
    }

    /**
     * 获取卡牌稀有度选项
     */
    async getCardRarityOptions() {
        return await this.getEnumOptions('CardRarity');
    }

    /**
     * 获取卡牌职业选项
     */
    async getCardClassOptions() {
        return await this.getEnumOptions('CardClass');
    }

    /**
     * 获取能力类型选项
     */
    async getAbilityTypeOptions() {
        return await this.getEnumOptions('AbilityType');
    }

    /**
     * 获取能力标签选项
     */
    async getAbilityTagOptions() {
        return await this.getEnumOptions('AbilityKeyword');
    }

    /**
     * 确保元数据已加载
     */
    ensureLoaded() {
        if (!this.isLoaded) {
            throw new Error('元数据尚未加载，请先调用 initialize()');
        }
    }

    /**
     * 重新加载元数据
     */
    async reload() {
        this.isLoaded = false;
        this.loadPromise = null;
        this.metadata = null;
        return await this.initialize();
    }

    /**
     * 获取完整的元数据对象
     */
    getMetadata() {
        this.ensureLoaded();
        return this.metadata;
    }

    /**
     * 检查是否已加载
     */
    isMetadataLoaded() {
        return this.isLoaded;
    }

    /**
     * 清理资源
     */
    cleanup() {
        this.metadata = null;
        this.isLoaded = false;
        this.loadPromise = null;
        console.log('MetadataManager: Cleaned up');
    }
}

// 创建全局实例
export const metadataManager = new MetadataManager();
