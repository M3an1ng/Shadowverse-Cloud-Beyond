import {
    buildActivityQuestMessage,
    extractQuestProgress
} from '../questFeedback.js';

describe('quest feedback helpers', () => {
    test('builds progress message from updated quest progress', () => {
        const progress = extractQuestProgress({
            questProgress: {
                updatedQuestIds: ['quest-1'],
                completedQuestIds: [],
                claimableCount: 0
            }
        });

        expect(buildActivityQuestMessage('评论已发布。', progress)).toBe('评论已发布。 任务进度 +1。');
    });

    test('builds completion message when a quest became claimable', () => {
        const progress = extractQuestProgress({
            QuestProgress: {
                UpdatedQuestIds: ['quest-1'],
                CompletedQuestIds: ['quest-1'],
                ClaimableCount: 1
            }
        });

        expect(buildActivityQuestMessage('投票已更新。', progress)).toBe('投票已更新。 任务完成，可领取奖励。');
    });

    test('leaves base message unchanged without quest progress', () => {
        const progress = extractQuestProgress({
            rewardGranted: false,
            rewardCoins: 0
        });

        expect(buildActivityQuestMessage('投票已更新。', progress)).toBe('投票已更新。');
    });
});
