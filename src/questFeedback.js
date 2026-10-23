export const QUEST_PROGRESS_UPDATED_EVENT = 'svb:quest-progress-updated';
export const QUEST_REWARD_CLAIMED_EVENT = 'svb:quest-reward-claimed';

function pickValue(source, camelKey, pascalKey, fallback = null) {
    if (!source || typeof source !== 'object') return fallback;
    if (source[camelKey] !== undefined && source[camelKey] !== null) return source[camelKey];
    if (source[pascalKey] !== undefined && source[pascalKey] !== null) return source[pascalKey];
    return fallback;
}

function normalizeIdList(value) {
    if (!Array.isArray(value)) return [];
    return value
        .map(item => String(item ?? '').trim())
        .filter(Boolean);
}

export function extractQuestProgress(payload) {
    const progress = pickValue(payload, 'questProgress', 'QuestProgress', null);
    if (!progress || typeof progress !== 'object') {
        return null;
    }

    const updatedQuestIds = normalizeIdList(pickValue(progress, 'updatedQuestIds', 'UpdatedQuestIds', []));
    const completedQuestIds = normalizeIdList(pickValue(progress, 'completedQuestIds', 'CompletedQuestIds', []));
    const claimableCountRaw = Number.parseInt(String(pickValue(progress, 'claimableCount', 'ClaimableCount', 0)), 10);

    return {
        updatedQuestIds,
        completedQuestIds,
        claimableCount: Number.isFinite(claimableCountRaw) ? Math.max(0, claimableCountRaw) : 0
    };
}

export function buildActivityQuestMessage(baseMessage, questProgress) {
    if (!questProgress) {
        return baseMessage;
    }

    if (questProgress.completedQuestIds.length > 0) {
        return `${baseMessage} 任务完成，可领取奖励。`;
    }

    if (questProgress.updatedQuestIds.length > 0) {
        return `${baseMessage} 任务进度 +1。`;
    }

    return baseMessage;
}
