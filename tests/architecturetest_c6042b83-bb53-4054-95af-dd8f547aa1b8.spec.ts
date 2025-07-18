
import { test } from '@playwright/test';
import { expect } from '@playwright/test';

test('ArchitectureTest_2025-06-14', async ({ page, context }) => {
  
    // Navigate to URL
    await page.goto('http://localhost:5173');

    // Take screenshot
    await page.screenshot({ path: 'initial_game_state.png' });

    // Click element
    await page.click('button:has-text("确认调度")');
});