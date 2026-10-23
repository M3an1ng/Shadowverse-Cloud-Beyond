import { createGuestJoinSoundTracker } from '../multiplayerRoomSoundPolicy.js';

describe('multiplayer room sound policy', () => {
    test('plays once when a guest first appears in the room', () => {
        const playGuestJoined = jest.fn();
        const tracker = createGuestJoinSoundTracker({ playGuestJoined });

        tracker.observe({ guestPlayerId: null });
        tracker.observe({ guestPlayerId: 'guest-1' });
        tracker.observe({ guestPlayerId: 'guest-1' });

        expect(playGuestJoined).toHaveBeenCalledTimes(1);
    });
});
