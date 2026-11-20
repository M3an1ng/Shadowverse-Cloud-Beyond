const { join } = require('node:path');
const { chromium, expect } = require(process.env.TOURNAMENT_TEST_BROWSER_MODULE || '@playwright/test');
const { base } = require('./fixtures.cjs');

const assertUi = expect.configure({ timeout: 18000 });
const launchBrowser = () => chromium.launch({ executablePath: process.env.TOURNAMENT_TEST_BROWSER_EXECUTABLE });

async function loginPage(browser, player, onPage = () => {}) {
    const context = await browser.newContext({
        viewport: { width: 1390, height: 880 }, serviceWorkers: 'block'
    });
    await context.route('**/*', route => route.request().url().startsWith(base + '/') ? route.continue() : route.abort());
    const page = await context.newPage();
    onPage(page);
    await page.goto(base + '/');
    await page.locator('#user-info-toggle').click();
    await page.locator('#user-auth-username').fill(player.username);
    await page.locator('#user-auth-password').fill(player.password);
    await page.locator('#user-auth-login').click();
    await assertUi(page.locator('#user-info-name')).toHaveText(player.username);
    return page;
}

async function screenshot(page, name) {
    const directory = process.env.TOURNAMENT_TEST_SCREENSHOTS;
    if (directory) await page.screenshot({ animations: 'disabled', path: join(directory, name) });
}

module.exports = { assertUi, launchBrowser, loginPage, screenshot };
