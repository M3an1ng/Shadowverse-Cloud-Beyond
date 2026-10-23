describe('router same-route navigation', () => {
    beforeEach(() => {
        jest.resetModules();
        window.gameInitialized = true;
        window.location.hash = '';
        document.body.innerHTML = `
            <section id="navigation-page" class="active"></section>
            <section id="game-page"></section>
            <section id="card-editor-page"></section>
            <section id="deck-builder-page"></section>
            <section id="dev-tools-page"></section>
            <section id="dev-tools-match-lab-page"></section>
            <section id="dsl-editor-v2-page"></section>
        `;
    });

    test('does not cleanup and reinitialize when navigating to the current route', async () => {
        const cleanupListener = jest.fn();
        document.addEventListener('cleanupGame', cleanupListener);

        const { router } = await import('../router.js');
        await router.navigateTo('game', false);
        expect(router.getCurrentRoute()).toBe('game');

        const navigated = await router.navigateTo('game', false);

        expect(navigated).toBe(true);
        expect(cleanupListener).not.toHaveBeenCalled();

        document.removeEventListener('cleanupGame', cleanupListener);
    });
});
