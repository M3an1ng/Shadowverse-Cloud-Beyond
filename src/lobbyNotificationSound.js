const GUEST_JOINED_DURATION_SECONDS = 0.18;
const GUEST_JOINED_FREQUENCY_HZ = 880;
const GUEST_JOINED_VOLUME = 0.08;

export function createLobbyNotificationSoundPlayer(options = {}) {
    const AudioContextCtor = options.AudioContextCtor
        || (typeof window !== 'undefined' && (window.AudioContext || window.webkitAudioContext))
        || null;

    let audioContext = null;

    function getAudioContext() {
        if (!AudioContextCtor) {
            return null;
        }

        if (!audioContext) {
            audioContext = new AudioContextCtor();
        }

        return audioContext;
    }

    function unlock() {
        const context = getAudioContext();
        if (!context || typeof context.resume !== 'function') {
            return false;
        }

        const result = context.resume();
        if (result && typeof result.catch === 'function') {
            result.catch(() => {});
        }
        return true;
    }

    function playGuestJoined() {
        const context = getAudioContext();
        if (!context) {
            return false;
        }

        if (context.state === 'suspended') {
            unlock();
        }

        const startAt = context.currentTime;
        const endAt = startAt + GUEST_JOINED_DURATION_SECONDS;
        const oscillator = context.createOscillator();
        const gain = context.createGain();

        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(GUEST_JOINED_FREQUENCY_HZ, startAt);
        gain.gain.setValueAtTime(0, startAt);
        gain.gain.linearRampToValueAtTime(GUEST_JOINED_VOLUME, startAt + 0.02);
        gain.gain.linearRampToValueAtTime(0, endAt);

        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.start(startAt);
        oscillator.stop(endAt);

        return true;
    }

    return {
        get audioContext() {
            return audioContext;
        },
        playGuestJoined,
        unlock
    };
}
