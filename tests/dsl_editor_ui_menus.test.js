import { showAddNodeMenu } from '../src/components/dsl_editor/ui-menus.js';

describe('dsl ui menus', () => {
    afterEach(() => {
        document.body.innerHTML = '';
    });

    test('showAddNodeMenu renders above pipeline modal overlay', () => {
        const overlay = document.createElement('div');
        overlay.className = 'dsl-pipeline-stage-modal-overlay';
        overlay.style.zIndex = '1250';
        document.body.appendChild(overlay);

        const button = document.createElement('button');
        overlay.appendChild(button);

        const editor = {
            getCurrentEditingOptions: () => [],
            groupOptionsByType: () => ({}),
            addNode: jest.fn()
        };

        showAddNodeMenu(editor, button);

        const menu = document.querySelector('.dsl-options-dropdown');
        expect(menu).not.toBeNull();
        expect(menu.style.getPropertyValue('position')).toBe('fixed');
        expect(menu.style.getPropertyPriority('z-index')).toBe('important');
        expect(Number.parseInt(menu.style.zIndex, 10)).toBeGreaterThan(1250);
    });
});

