import {
    createBattleCustomizationAudioController,
    isAutoplayBlockedError
} from '../battleCustomizationAudio.js';

class FakeAudioElement {
    constructor() {
        this.src = '';
        this.loop = false;
        this.preload = '';
        this.paused = true;
        this.playImpl = () => Promise.resolve();
        this.listeners = new Map();
    }

    addEventListener(type, callback) {
        this.listeners.set(type, callback);
    }

    remove() {
        this.removed = true;
    }

    removeAttribute(name) {
        if (name === 'src') {
            this.src = '';
        }
    }

    load() {
        this.loaded = true;
    }

    play() {
        this.paused = false;
        return this.playImpl();
    }

    pause() {
        this.paused = true;
    }

    emit(type, payload) {
        this.listeners.get(type)?.(payload);
    }
}

describe('battle customization audio controller', () => {
    test('detects browser autoplay blocking errors', () => {
        expect(isAutoplayBlockedError({ name: 'NotAllowedError' })).toBe(true);
        expect(isAutoplayBlockedError({ message: 'play() failed because the user did not interact with the document first' })).toBe(true);
        expect(isAutoplayBlockedError({ name: 'AbortError', message: 'other failure' })).toBe(false);
    });

    test('keeps saved asset state when autoplay is blocked and retries after interaction', async () => {
        const audioElement = new FakeAudioElement();
        const createdUrls = [];
        const revokedUrls = [];
        let playAttempt = 0;

        audioElement.playImpl = () => {
            playAttempt += 1;
            if (playAttempt === 1) {
                return Promise.reject({
                    name: 'NotAllowedError',
                    message: 'user gesture required'
                });
            }
            return Promise.resolve();
        };

        const controller = createBattleCustomizationAudioController({
            audioElement,
            createObjectURL(blob) {
                const url = `blob:${blob.size}`;
                createdUrls.push(url);
                return url;
            },
            revokeObjectURL(url) {
                revokedUrls.push(url);
            }
        });

        const stateAfterApply = await controller.applyAsset({
            blob: new Blob(['music'], { type: 'audio/mpeg' }),
            fileName: 'battle.mp3'
        });

        expect(stateAfterApply.status).toBe('blocked');
        expect(stateAfterApply.pendingInteractionRetry).toBe(true);
        expect(stateAfterApply.assetName).toBe('battle.mp3');

        const stateAfterRetry = await controller.retryAfterInteraction();
        expect(stateAfterRetry.status).toBe('playing');
        expect(stateAfterRetry.pendingInteractionRetry).toBe(false);

        controller.clear();
        expect(revokedUrls).toEqual(createdUrls);
    });
});
