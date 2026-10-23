jest.mock('../src/components/CardDetailMockup.js', () => ({
  showCardDetailModal: jest.fn(),
}));

import { CardEditor } from '../src/cardEditor.js';

describe('CardEditor art upload errors', () => {
  beforeEach(() => {
    global.alert = jest.fn();
    jest.spyOn(console, 'error').mockImplementation(() => {});
    document.body.innerHTML = '<div id="editor"></div>';
  });

  afterEach(() => {
    console.error.mockRestore();
    delete global.fetch;
    delete global.alert;
  });

  test('includes server-provided bad request details in upload errors', async () => {
    global.fetch = jest.fn(() => Promise.resolve({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      headers: {
        get: () => 'text/plain',
      },
      text: () => Promise.resolve('Unsupported image content.'),
    }));

    const editor = new CardEditor({ containerId: 'editor' });
    editor.container = document.getElementById('editor');
    editor.currentCard = { id: 42, name: 'Card' };
    editor.getCurrentCardId = () => 42;

    await expect(
      editor.uploadCardArt('detail', new Blob(['not-image'], { type: 'image/webp' }), 'detail.webp', { silent: true })
    ).rejects.toThrow('HTTP 400: Unsupported image content.');
  });
});
