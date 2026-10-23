import { normalizeMatchRoomList } from "../matchRoomBrowser.js";

describe("match room browser", () => {
    test("normalizes list payload and marks started rooms as spectatable", () => {
        const rooms = normalizeMatchRoomList({
            items: [
                {
                    roomId: "waiting-room",
                    activeGameId: null,
                    status: "Waiting",
                    hasGuest: false
                },
                {
                    roomId: "started-room",
                    activeGameId: "active-game",
                    status: "Started",
                    hasGuest: true,
                    round: 2
                }
            ]
        });

        expect(rooms).toHaveLength(2);
        expect(rooms[0]).toMatchObject({
            roomId: "started-room",
            status: "Started",
            statusLabel: "对战中",
            canSpectate: true,
            round: 2
        });
        expect(rooms[1]).toMatchObject({
            roomId: "waiting-room",
            status: "Waiting",
            statusLabel: "等待中",
            canSpectate: false
        });
    });

    test("supports PascalCase responses and uses room id for spectate input", () => {
        const rooms = normalizeMatchRoomList({
            Items: [
                {
                    RoomId: "room-id-to-fill",
                    ActiveGameId: "runtime-game-id",
                    Status: "Started",
                    HasGuest: true
                }
            ]
        });

        expect(rooms).toHaveLength(1);
        expect(rooms[0].roomId).toBe("room-id-to-fill");
        expect(rooms[0].activeGameId).toBe("runtime-game-id");
        expect(rooms[0].canSpectate).toBe(true);
    });
});
