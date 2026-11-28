// Reports and moderation. Needs a freshly imported database.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const BASE = process.env.BASE_URL || 'http://localhost:5055';
const OUT = path.join(__dirname, 'shots'); fs.mkdirSync(OUT, { recursive: true });
const results = [];
const check = (name, ok, extra = '') => { results.push(ok); console.log(ok ? 'PASS' : 'FAIL', name, extra); };

async function login(page, email) {
    await page.goto(BASE + '/Account/Login');
    await page.fill('#Email', email); await page.fill('#Password', 'Demo@12345');
    await page.locator('.authBox button[type=submit]').click(); await page.waitForLoadState('networkidle');
}
const logout = async page => { await page.goto(BASE + '/notifications'); await page.click('button[title="Log out"]'); await page.waitForLoadState('networkidle'); };
const toast = async page => (await page.textContent('#toast')) || '';

(async () => {
    const browser = await chromium.launch(process.env.BROWSER_CHANNEL ? { channel: process.env.BROWSER_CHANNEL } : {});
    const page = await (await browser.newContext({ viewport: { width: 1366, height: 850 } })).newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('dialog', d => d.accept());

    // student reports the tutor of booking 9
    await login(page, 'aylin.kaya@university.edu');
    await page.goto(BASE + '/bookings/9');
    await page.click('button[data-modal="report-modal"]');
    await page.click('#report-modal button[type=submit]');
    check('report form client validation', (await page.textContent('#report-modal')).includes('Please choose what the problem is.'));
    await page.selectOption('#report-modal-type', 'NoShow'); await page.fill('#report-modal-description', 'short');
    await page.click('#report-modal button[type=submit]');
    check('description length validated', (await page.textContent('#report-modal')).includes('10 to 1000'));
    await page.fill('#report-modal-description', 'The tutor did not join the session at the agreed time.');
    await page.click('#report-modal button[type=submit]'); await page.waitForLoadState('networkidle');
    check('student report sent', (await toast(page)).includes('report was sent'), await toast(page));
    // server-side validation: bad type posted directly
    const token = await page.inputValue('#report-modal input[name="__RequestVerificationToken"]');
    const bad = await page.request.post(BASE + '/bookings/9/report', { form: { __RequestVerificationToken: token, ReportType: 'Hacked', Description: 'x' } });
    check('server rejects invalid report', (await bad.text()).includes('Please choose what the problem is') || (await bad.text()).includes('10 to 1000'));
    const other = await page.request.post(BASE + '/bookings/10/report', { form: { __RequestVerificationToken: token, ReportType: 'Spam', Description: 'Trying to report a booking that is not mine.' } });
    check("cannot report through someone else's booking", other.status() === 404, String(other.status()));
    const forbidden = await page.goto(BASE + '/admin/moderation');
    check('student blocked from moderation', (await page.textContent('body')).includes('Access denied'));
    await logout(page);

    // tutor reports a review
    await login(page, 'elif.demir@university.edu');
    await page.goto(BASE + '/tutor/reviews');
    const before = await page.locator('main .li').count();
    await page.click('button[data-modal="report-review-4"]');
    await page.selectOption('#report-review-4-type', 'InappropriateContent');
    await page.fill('#report-review-4-description', 'This review is unfair and contains wrong statements.');
    await page.click('#report-review-4 button[type=submit]'); await page.waitForLoadState('networkidle');
    check('tutor review report sent', (await toast(page)).includes('report was sent'), await toast(page));
    await logout(page);

    // admin decides
    await login(page, 'dana@peermind.ai');
    check('dashboard shows 5 open reports', (await page.textContent('a[href="/admin/moderation"].card')).includes('5'));
    await page.goto(BASE + '/admin/moderation');
    check('5 open reports listed', (await page.locator('main form[action*="/decide"]').count()) === 5);
    await page.screenshot({ path: path.join(OUT, '22-moderation.png'), fullPage: true });

    const card = text => page.locator('.card', { has: page.locator(`form[action*="/decide"]`) }).filter({ hasText: text }).first();
    // dismiss the new no-show report
    await card('did not join the session').locator('button:has-text("Dismiss")').click(); await page.waitForLoadState('networkidle');
    check('dismiss recorded', (await toast(page)).includes('dismissed'), await toast(page));
    // warn Sam (report 1) with a note
    const c1 = card('offensive language');
    await c1.locator('input[name="Note"]').fill('Please keep messages respectful.');
    await c1.locator('button:has-text("Warn user")').click(); await page.waitForLoadState('networkidle');
    check('warning recorded', (await toast(page)).includes('warning was sent'), await toast(page));
    // suspend Jay Reed for the spam review (report 3) -> review hidden, rating recalculated
    await card('promotional links').locator('button:has-text("Suspend user")').click(); await page.waitForLoadState('networkidle');
    check('suspension recorded', (await toast(page)).includes('suspended'), await toast(page));
    check('2 reports still open', (await page.locator('main form[action*="/decide"]').count()) === 2);
    const log = await page.textContent('table');
    check('decision log shows the three decisions', log.includes('Dismissed') && log.includes('Warning sent') && log.includes('Please keep messages respectful.'));
    await page.screenshot({ path: path.join(OUT, '23-moderation-after.png'), fullPage: true });
    // deciding the same report twice is refused by the database
    const t2 = await page.inputValue('main input[name="__RequestVerificationToken"]');
    const again = await page.request.post(BASE + '/admin/moderation/1/decide', { form: { __RequestVerificationToken: t2, Decision: 'Dismissed' } });
    check('double decision refused', (await again.text()).includes('already decided'));
    const unknown = await page.request.post(BASE + '/admin/moderation/2/decide', { form: { __RequestVerificationToken: t2, Decision: 'Delete' } });
    check('unknown decision refused', (await unknown.text()).includes('Unknown decision'));
    await logout(page);

    // effects
    await login(page, 'jay.reed@university.edu');
    check('suspended user cannot log in', (await page.textContent('.alert-bad')).includes('suspended'));
    await login(page, 'elif.demir@university.edu');
    await page.goto(BASE + '/tutor/reviews');
    const after = await page.locator('main .li').count();
    check('spam review hidden from tutor reviews', after === before - 1, `${before} -> ${after}`);
    check('rating recalculated (4.5 -> 4.7)', (await page.textContent('main')).includes('4.7'));

    check('no browser errors', errors.length === 0, errors.join(' | '));
    await browser.close();
    const failed = results.filter(r => !r).length;
    console.log(`\n${results.length - failed}/${results.length} passed`);
    process.exit(failed ? 1 : 0);
})().catch(e => { console.error('CRASH', e); process.exit(2); });
