const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { base, eventsApi, adminApi, account, read, sqlite, standardClasses, publish, setCutoff } = require('./fixtures.cjs');
const { assertUi, launchBrowser, loginPage, screenshot } = require('./browser.cjs');

const eventName = 'EOT 星云大赛 · 报名演示';
const beijingInput = offset => new Date(Date.now() + offset + 8 * 3600000).toISOString().slice(0, 16);

async function createEvent(page, admin) {
    await page.locator('.nav-item[data-route="dev-tools"]').click();
    await assertUi(page.locator('#dev-tools-page')).toHaveClass(/active/);
    await page.getByRole('button', { name: '管理赛事', exact: true }).click();
    const field = name => page.locator('#tournament-admin-form [name="' + name + '"]');
    await field('name').fill(eventName);
    await field('cardVersion').fill('EOT');
    await field('format').selectOption('major');
    await field('stageMode').selectOption('swiss');
    await field('swissMatchFormat').selectOption('bo3Ban1');
    await field('swissResubmission').check();
    await screenshot(page, 'management-create.png');
    await page.getByRole('button', { name: '发布赛事', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: '确认', exact: true }).click();
    await assertUi(page.locator('#tournament-admin-status')).toHaveText('赛事已发布。');
    await assertUi(page.getByRole('button', { name: '立即截止报名' })).toBeEnabled();
    await assertUi(field('registrationDeadline')).toHaveValue('');
    await page.locator('#tournament-admin-registration-controls').scrollIntoViewIfNeeded();
    await screenshot(page, 'management-published.png');
    return (await read(adminApi, { player: admin })).find(event => event.name === eventName);
}

async function pick(page, slot, deckId, afterOpen = async () => {}) {
    await page.locator('[data-choose-deck="' + slot + '"]').click();
    const modal = page.locator('.deck-picker-modal.open');
    await assertUi(modal).toHaveCount(1);
    await assertUi(modal.locator('#deck-picker-modal-title')).toHaveText('选择第 ' + (slot + 1) + ' 套卡组');
    await afterOpen(modal);
    const card = modal.locator('.deck-picker-card[data-deck-id="' + deckId + '"]');
    await card.hover();
    await card.getByRole('button', { name: '选择', exact: true }).click();
    await assertUi(modal).toBeHidden();
}

async function enrollFromPicker(page, event, player) {
    await page.goto(base + '/?tournament=' + event.id + '#tournaments');
    const choices = page.locator('#tournament-registration-fields [data-choose-deck]');
    const submit = page.getByRole('button', { name: '确认报名' });
    await assertUi(page.locator('#tournament-event-end')).toHaveText('手动截止');
    await assertUi(choices).toHaveCount(3);
    await assertUi(submit).toBeDisabled();
    await screenshot(page, 'management-registration.png');
    await pick(page, 0, player.decks[0].deckId, async modal => {
        await assertUi(modal.locator('.deck-picker-card')).toHaveCount(4);
        await screenshot(page, 'management-picker.png');
        const card = modal.locator('.deck-picker-card[data-deck-id="' + player.decks[0].deckId + '"]');
        await card.hover();
        await card.getByRole('button', { name: '预览' }).click();
        await assertUi(modal.locator('#deck-picker-preview-title')).toHaveText(player.decks[0].name);
        await screenshot(page, 'management-preview.png');
        await modal.getByRole('button', { name: '关闭预览' }).click();
    });
    assert.equal(await choices.nth(0).evaluate(element => element === document.activeElement), true);

    await page.setViewportSize({ width: 390, height: 844 });
    await pick(page, 1, player.decks[1].deckId, async modal => {
        await assertUi(modal.locator('.deck-picker-card')).toHaveCount(2);
        assert.equal(await modal.locator('.deck-picker-card[data-deck-id="' + player.decks[3].deckId + '"]').count(), 0);
        assert.equal(await modal.locator('.deck-picker-selection-view').evaluate(
            element => element.scrollWidth > element.clientWidth + 1), false);
        await screenshot(page, 'management-picker-mobile.png');
    });
    await page.setViewportSize({ width: 1390, height: 880 });
    await pick(page, 2, player.decks[2].deckId);
    await assertUi(submit).toBeEnabled();
    await page.locator('[data-clear-deck="1"]').click();
    await assertUi(submit).toBeDisabled();
    await pick(page, 1, player.decks[1].deckId);
    await submit.click();
    await assertUi(page.locator('#tournament-registration-status')).toHaveText('报名成功。');
    const result = await read(eventsApi + '/' + event.id + '/registration', { player });
    assert.deepEqual(result.submittedDecks.map(deck => deck.deckId), player.decks.slice(0, 3).map(deck => deck.deckId));
    await screenshot(page, 'management-registered.png');
    return result;
}

async function manageCutoff(page, playerPage, admin, player, event, entry) {
    const eventPath = eventsApi + '/' + event.id;
    const deadlineApi = adminApi + '/' + event.id + '/registration/cutoff';
    const payload = { revision: event.revision, closeNow: true };
    const avatarFile = readFileSync(join(__dirname, '../../public/mse-beyond/Emblem_Forestcraft.png'));
    const avatarForm = new FormData();
    avatarForm.set('file', new Blob([avatarFile], { type: 'image/png' }), 'avatar.png');
    const uploaded = await fetch(base + '/api/users/' + player.userId + '/avatar', {
        method: 'POST', headers: { Cookie: player.cookie }, body: avatarForm
    });
    assert.equal(uploaded.status, 200);
    const { avatarUrl } = await uploaded.json();
    const opened = await read(eventPath);
    assert.equal(opened.tournament.status, 'published');
    assert.equal(opened.tournament.registrationStatus, 'open');
    assert.equal(opened.tournament.registrationClosesAtUtc, null);
    assert.ok(Date.parse(opened.tournament.registrationOpensAtUtc) <= Date.parse(opened.serverTimeUtc));
    await read(deadlineApi, { method: 'PUT', payload, status: 401 });
    await read(deadlineApi, { method: 'PUT', player, payload, status: 403 });

    const field = page.locator('[name="registrationDeadline"]');
    await field.fill(beijingInput(-120000));
    await page.getByRole('button', { name: '保存截止时间' }).click();
    await assertUi(page.locator('#tournament-admin-status')).toContainText('必须晚于');
    const newEnd = beijingInput(5400000);
    await field.fill(newEnd);
    await page.getByRole('button', { name: '保存截止时间' }).click();
    await assertUi(page.locator('#tournament-admin-status')).toHaveText('报名截止时间已更新。');
    const updated = await read(eventPath);
    assert.equal(updated.tournament.registrationClosesAtUtc, new Date(newEnd + ':00+08:00').toISOString().replace('.000Z', 'Z'));
    assert.equal(updated.entrants, null);
    await read(deadlineApi, { method: 'PUT', player: admin, payload, status: 409 });
    await read(eventPath + '/entries/' + entry.entryId, { status: 404 });

    await page.getByRole('button', { name: '立即截止报名' }).click();
    await screenshot(page, 'management-close.png');
    await page.getByRole('button', { name: '确认截止', exact: true }).click();
    await assertUi(page.locator('#tournament-admin-status')).toHaveText('报名已截止。');
    await assertUi(page.locator('#tournament-admin-registration-controls')).toBeHidden();
    await playerPage.bringToFront();
    await assertUi(playerPage.locator('#tournament-registration-form')).toBeHidden();
    const cutoff = await read(eventPath);
    assert.equal(cutoff.tournament.registrationStatus, 'closed');
    assert.equal(cutoff.entrants[0].entryId, entry.entryId);
    const lineup = await read(eventPath + '/entries/' + entry.entryId);
    assert.equal(lineup.decks.length, 3);
    assert.equal(lineup.avatarUrl, avatarUrl);
    await read(eventPath + '/registration', {
        method: 'PUT', player, status: 409, payload: { revision: entry.revision, deckIds: player.decks.slice(0, 3).map(deck => deck.deckId) }
    });
    const spectator = await page.context().newPage();
    try {
        await spectator.goto(base + '/?tournament=' + event.id + '#tournaments');
        const entrant = spectator.locator('.tournament-standings').getByRole('button', { name: player.username });
        await assertUi(entrant).toBeVisible();
        const privateDeckRequests = [];
        spectator.on('request', request => {
            if (new URL(request.url()).pathname.startsWith('/api/decks/')) privateDeckRequests.push(request.url());
        });
        await entrant.click();
        const profile = spectator.locator('.deck-picker-modal[data-player-profile]');
        await assertUi(profile.getByRole('heading', { name: player.username, exact: true })).toBeVisible();
        const avatar = profile.locator('.deck-picker-profile-avatar img');
        await assertUi(avatar).toHaveAttribute('src', base + avatarUrl);
        await assertUi.poll(() => avatar.evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
        await assertUi(profile.locator('.deck-picker-profile-record')).toContainText('本赛事战绩');
        const headerLayout = await profile.locator('.view-header').evaluate(header => {
            const name = header.querySelector('h2').getBoundingClientRect();
            const record = header.querySelector('.deck-picker-profile-record').getBoundingClientRect();
            const close = header.querySelector('.deck-picker-close-btn').getBoundingClientRect();
            const digit = header.querySelector('.deck-picker-profile-wins');
            return {
                recordBelowName: record.top >= name.bottom && Math.abs(record.left - name.left) <= 1,
                closeAtRight: header.getBoundingClientRect().right - close.right <= 12,
                prominentDigits: parseFloat(getComputedStyle(digit).fontSize) >= 32
            };
        });
        assert.deepEqual(headerLayout, { recordBelowName: true, closeAtRight: true, prominentDigits: true });
        await assertUi(profile.locator('.tournament-player-deck')).toHaveCount(3);
        await assertUi(profile.locator('.tournament-player-deck-stats > span')).toHaveText(['暂无小局', '暂无小局', '暂无小局']);
        await assertUi(profile.locator('.tournament-player-deck-ban strong')).toHaveText(['0', '0', '0']);
        await screenshot(spectator, 'management-player-profile.png');
        const firstDeck = profile.getByRole('button', { name: '预览卡组', exact: true }).first();
        await firstDeck.click();
        const preview = spectator.locator('#deck-picker-preview-modal');
        const switcher = preview.getByRole('combobox', { name: '切换卡组' });
        await assertUi(preview).toBeVisible();
        await assertUi(preview.locator('#deck-picker-preview-title')).toHaveText(lineup.decks[0].name);
        await assertUi(preview.locator('.deck-preview-total')).toHaveText('40 / 40');
        await assertUi(preview.locator('.deck-preview-curve-col')).toHaveCount(9);
        await assertUi(preview.locator('.deck-preview-card')).toHaveCount(lineup.decks[0].cards.length);
        await assertUi(preview.locator('#deck-picker-preview-toolbar-stats')).toContainText('总对局 0');
        await assertUi(spectator.locator('[data-picker-action="select"]')).toHaveCount(0);
        await assertUi(switcher.locator('option')).toHaveCount(3);
        await switcher.selectOption(lineup.decks[1].id);
        await assertUi(preview.locator('#deck-picker-preview-title')).toHaveText(lineup.decks[1].name);
        await assertUi(preview.locator('.deck-preview-total')).toHaveText('40 / 40');
        await screenshot(spectator, 'management-open-lineups.png');
        await spectator.setViewportSize({ width: 390, height: 844 });
        const overflows = await preview.locator('.deck-preview-dialog').evaluate(
            dialog => dialog.scrollWidth > dialog.clientWidth + 1);
        assert.equal(overflows, false);
        await preview.getByRole('button', { name: '关闭预览' }).click();
        await assertUi(firstDeck).toBeFocused();
        assert.equal(await profile.locator('.deck-picker-selection-view').evaluate(
            dialog => dialog.scrollWidth > dialog.clientWidth + 1), false);
        await profile.getByRole('button', { name: '关闭选手资料', exact: true }).click();
        await assertUi(spectator.locator('.deck-picker-modal')).toBeHidden();
        await assertUi(entrant).toBeFocused();
        assert.deepEqual(privateDeckRequests, []);
    } finally {
        await spectator.close();
    }
}

async function deleteEvent(page, admin, player, event, entry) {
    const path = adminApi + '/' + event.id + '?revision=' + event.revision;
    await read(path, { method: 'DELETE', status: 401 });
    await read(path, { method: 'DELETE', player, status: 403 });
    await read(path, { method: 'DELETE', player: admin, status: 409 });
    await page.getByRole('button', { name: '删除赛事', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await assertUi(dialog).toContainText(eventName);
    await screenshot(page, 'management-delete.png');
    await dialog.getByRole('button', { name: '取消', exact: true }).click();
    await assertUi(page.getByRole('button', { name: '删除赛事', exact: true })).toBeEnabled();
    await read(eventsApi + '/' + event.id);
    await page.getByRole('button', { name: '删除赛事', exact: true }).click();
    await dialog.getByRole('button', { name: '确认删除', exact: true }).click();
    await assertUi(page.locator('#tournament-admin-status')).toHaveText('赛事已删除。');
    await read(eventsApi + '/' + event.id, { status: 404 });
    assert.equal((await read(eventsApi + '/visibility')).available, false);
    sqlite("import sqlite3,sys\nwith sqlite3.connect(sys.argv[1]) as db:\n"
        + " assert not db.execute('SELECT 1 FROM TournamentEntries WHERE Id=?', (sys.argv[2],)).fetchone()\n"
        + " assert not db.execute('SELECT 1 FROM TournamentEntryDecks WHERE EntryId=?', (sys.argv[2],)).fetchone()",
    entry.entryId.toUpperCase());
    await read('/api/decks/' + player.decks[0].deckId + '?userId=' + player.userId, { player });
}

async function checkGrandFinalSetting(page, admin) {
    await page.getByRole('button', { name: '新建赛事', exact: true }).click();
    await page.locator('[name="name"]').fill('双败总决赛设置验证');
    await page.locator('[name="cardVersion"]').fill('演练');
    await page.locator('[name="format"]').selectOption('major');
    await page.locator('[name="stageMode"]').selectOption('elimination');
    const elimination = page.locator('[name="elimination"]');
    const reset = page.locator('[name="allowGrandFinalReset"]');
    await elimination.selectOption('singleElimination');
    await assertUi(reset).toBeHidden();
    await elimination.selectOption('doubleElimination');
    await assertUi(reset).toBeVisible();
    await assertUi(reset).not.toBeChecked();
    await reset.check();
    await page.locator('[name="stageMode"]').selectOption('both');
    await page.locator('[name="eliminationResubmission"]').check();
    const minutes = page.locator('[name="deckSubmissionMinutes"]');
    await assertUi(minutes).toBeVisible();
    await assertUi(minutes).toHaveValue('');
    await page.getByRole('button', { name: '保存草稿', exact: true }).click();
    await assertUi(page.locator('#tournament-admin-status')).toHaveText('草稿已保存。');
    const drafts = await read(adminApi, { player: admin });
    const eliminationStage = drafts.find(draft => draft.name === '双败总决赛设置验证').stages[1];
    assert.equal(eliminationStage.allowGrandFinalReset, true);
    assert.equal(eliminationStage.deckSubmissionMinutes, null);
    await assertUi(minutes).toHaveValue('');
    await assertUi(reset).toBeChecked();
}

async function main() {
    const admin = await account('cup-organizer', { admin: true });
    const player = await account('cup-player', { classes: [...standardClasses, standardClasses[0]] });
    const browser = await launchBrowser();
    try {
        const [page, playerPage] = await Promise.all([loginPage(browser, admin), loginPage(browser, player)]);
        const entrance = playerPage.getByRole('button', { name: '大赛', exact: true });
        await assertUi(entrance).toBeVisible();
        assert.ok(await entrance.evaluate(button => button.previousElementSibling.id === 'user-info-widget'
            && button.nextElementSibling.id === 'daily-quest-widget'));
        await entrance.click();
        await assertUi(playerPage.getByText('目前没有赛事。', { exact: true })).toBeVisible();
        const event = await createEvent(page, admin);
        const entry = await enrollFromPicker(playerPage, event, player);
        await manageCutoff(page, playerPage, admin, player, event, entry);
        await deleteEvent(page, admin, player, event, entry);
        const config = { format: 'plaza', stages: [{ kind: 'plaza', matchFormat: 'bo1' }], registrationClosesAt: null };
        const scheduledId = await publish(admin, {
            ...config, name: 'Scheduled registration without cutoff', registrationOpensAt: new Date(Date.now() + 3600000).toISOString()
        });
        const scheduled = await read(eventsApi + '/' + scheduledId);
        assert.equal(scheduled.tournament.registrationStatus, 'scheduled');
        assert.equal(scheduled.tournament.registrationClosesAtUtc, null);
        const manualId = await publish(admin, { ...config, name: 'Manual cutoff', registrationOpensAt: null });
        await setCutoff(manualId, admin);
        assert.equal((await read(eventsApi + '/' + manualId)).tournament.registrationStatus, 'closed');
        await checkGrandFinalSetting(page, admin);
        console.log('Shared picker and public deck preview, distinct classes, optional dates, manual cutoff and deletion passed.');
    } finally {
        await browser.close();
    }
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
