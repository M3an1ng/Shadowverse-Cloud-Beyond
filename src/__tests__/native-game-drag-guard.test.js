import { preventNativeGameDrag } from "../nativeGameDragGuard.js";

describe("native game drag guard", () => {
    beforeEach(() => {
        document.body.innerHTML = `
            <div id="game-container">
                <div class="hand-card" data-uid="card-1"></div>
            </div>
            <div id="outside"></div>
        `;
    });

    test("prevents native drag events inside the game container", () => {
        const target = document.querySelector(".hand-card");
        const event = {
            target,
            preventDefault: jest.fn()
        };

        preventNativeGameDrag(event);

        expect(event.preventDefault).toHaveBeenCalledTimes(1);
    });

    test("does not prevent native drag events outside the game container", () => {
        const target = document.getElementById("outside");
        const event = {
            target,
            preventDefault: jest.fn()
        };

        preventNativeGameDrag(event);

        expect(event.preventDefault).not.toHaveBeenCalled();
    });
});
