import { createLobbyNotificationSoundPlayer } from '../lobbyNotificationSound.js';

class FakeAudioParam {
    constructor() {
        this.value = 0;
        this.calls = [];
    }

    setValueAtTime(value, time) {
        this.calls.push(['setValueAtTime', value, time]);
        this.value = value;
    }

    linearRampToValueAtTime(value, time) {
        this.calls.push(['linearRampToValueAtTime', value, time]);
        this.value = value;
    }
}

class FakeAudioContext {
    constructor() {
        this.currentTime = 10;
        this.destination = {};
        this.createdOscillators = [];
    }

    createOscillator() {
        const oscillator = {
            frequency: new FakeAudioParam(),
            connect: jest.fn(),
            start: jest.fn(),
            stop: jest.fn()
        };
        this.createdOscillators.push(oscillator);
        return oscillator;
    }

    createGain() {
        return {
            gain: new FakeAudioParam(),
            connect: jest.fn()
        };
    }
}

describe('lobby notification sound player', () => {
    test('plays a short guest-joined tone through Web Audio', () => {
        const player = createLobbyNotificationSoundPlayer({
            AudioContextCtor: FakeAudioContext
        });

        const played = player.playGuestJoined();

        expect(played).toBe(true);
        expect(player.audioContext.createdOscillators).toHaveLength(1);
        expect(player.audioContext.createdOscillators[0].start).toHaveBeenCalledWith(10);
        expect(player.audioContext.createdOscillators[0].stop).toHaveBeenCalledWith(10.18);
    });
});
