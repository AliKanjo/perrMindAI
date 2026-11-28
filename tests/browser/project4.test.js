// Project #4 additions: passwords, materials, AI assistant, security, small screens. Needs a freshly imported database
// and the app started against fake-claude.js (see README.md in this folder).
const { chromium, devices } = require('playwright');
const fs = require('fs');
const path = require('path');
const BASE = process.env.BASE_URL || 'http://localhost:5055';
const DIR = __dirname;
const FAKE_AI = process.env.FAKE_AI_URL || 'http://localhost:5099';
const OUT = path.join(DIR, 'shots'); fs.mkdirSync(OUT, { recursive: true });
const APP_LOG = process.env.APP_LOG || path.join(DIR, 'app.log');   // the app's terminal output (for the demo e-mail)
const results = [];
const check = (name, ok, extra = '') => { results.push([ok, name, extra]); console.log(ok ? 'PASS' : 'FAIL', name, extra); };
const shot = (page, name, full = true) => page.screenshot({ path: path.join(OUT, name + '.png'), fullPage: full });

async function login(page, email, password = 'Demo@12345') {
    await page.goto(BASE + '/Account/Login');
    await page.fill('#Email', email); await page.fill('#Password', password);
    await page.locator('.authBox button[type=submit]').click(); await page.waitForLoadState('networkidle');
}
async function logout(page) {
    await page.goto(BASE + '/notifications');
    await page.evaluate(() => document.querySelector('button[title="Log out"]').closest('form').submit());
    await page.waitForLoadState('networkidle');
}
const toast = async page => (await page.textContent('#toast')) || '';

(async () => {
    const browser = await chromium.launch();

    // ---- test PDFs made with the browser ----
    const maker = await browser.newPage();
    await maker.setContent(`<h1>Lecture 9 – Hash tables</h1><p>A hash table stores key value pairs. A hash function maps each key to a bucket index.
        When two keys map to the same bucket this is a collision. Collisions are resolved by chaining or by open addressing.</p>
        <div style="page-break-before:always"></div><h1>Load factor</h1><p>The load factor is the number of entries divided by the number of buckets.
        When the load factor grows too high the table is resized, which keeps lookups at O(1) on average.</p>`);
    const goodPdf = path.join(DIR, 'Lecture 9 - Hash tables.pdf'); await maker.pdf({ path: goodPdf });
    await maker.setContent('<div style="width:400px;height:300px;background:#5B4EF0"></div>');
    const noTextPdf = path.join(DIR, 'scanned.pdf'); await maker.pdf({ path: noTextPdf });
    const fakePdf = path.join(DIR, 'notes.pdf'); fs.writeFileSync(fakePdf, 'just text, not a pdf');
    const txt = path.join(DIR, 'notes.txt'); fs.writeFileSync(txt, 'hello');
    await maker.close();

    const ctx = await browser.newContext({ viewport: { width: 1366, height: 850 } });
    const page = await ctx.newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error' && !m.text().startsWith('Failed to load resource')) errors.push(m.text()); });  // deliberate 4xx/5xx answers are expected
    page.on('dialog', d => d.accept());

    // ================= passwords =================
    await login(page, 'aylin.kaya@university.edu');
    await page.goto(BASE + '/Account/Profile');
    await page.locator('#change-password button[type=submit]').click();
    check('change password: client validation', (await page.textContent('#change-password')).includes('Please enter your current password'));
    await page.fill('#Password_CurrentPassword', 'wrong-password'); await page.fill('#Password_NewPassword', 'NewPass@2026x'); await page.fill('#Password_ConfirmPassword', 'NewPass@2026x');
    await page.locator('#change-password button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('change password: wrong current password rejected by server', (await page.textContent('#change-password')).includes('not correct'));
    await page.fill('#Password_CurrentPassword', 'Demo@12345'); await page.fill('#Password_NewPassword', 'weakpassword'); await page.fill('#Password_ConfirmPassword', 'weakpassword');
    await page.locator('#change-password button[type=submit]').click();
    check('change password: weak new password rejected', (await page.textContent('#change-password')).includes('upper and lower case'));
    await page.fill('#Password_NewPassword', 'NewPass@2026x'); await page.fill('#Password_ConfirmPassword', 'NewPass@2026x');
    await page.locator('#change-password button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('change password: success', (await toast(page)).includes('password was changed'), await toast(page));
    await logout(page);
    await login(page, 'aylin.kaya@university.edu');
    check('old password no longer works', (await page.textContent('.alert-bad')).includes('Incorrect'));
    await login(page, 'aylin.kaya@university.edu', 'NewPass@2026x');
    check('new password works', page.url().endsWith('/student'), page.url());
    await logout(page);

    // forgot password
    await page.goto(BASE + '/Account/Login'); await page.click('a:has-text("Forgot password?")');
    await page.locator('.card button[type=submit]').click();
    check('forgot password: client validation', (await page.textContent('.card')).includes('Please enter your e-mail'));
    await page.fill('#Email', 'nobody.here@university.edu'); await page.locator('.card button[type=submit]').click(); await page.waitForLoadState('networkidle');
    const unknownText = (await page.textContent('.card')).replace('nobody.here@university.edu', 'X');
    await page.goto(BASE + '/Account/ForgotPassword');
    await page.fill('#Email', 'aylin.kaya@university.edu'); await page.locator('.card button[type=submit]').click(); await page.waitForLoadState('networkidle');
    const knownText = (await page.textContent('.card')).replace('aylin.kaya@university.edu', 'X');
    check('forgot password: same answer for known and unknown e-mail', unknownText === knownText);
    await shot(page, 'p4-01-forgot-sent');
    await page.waitForTimeout(500);
    const links = [...fs.readFileSync(APP_LOG, 'utf8').matchAll(/http:\/\/localhost:5055\/Account\/ResetPassword\?token=([0-9a-f]+)/g)];
    check('reset link written to the terminal (demo e-mail)', links.length >= 1);
    const resetUrl = links[links.length - 1][0];
    await page.goto(resetUrl);
    await page.fill('#NewPassword', 'Reset@2026xy'); await page.fill('#ConfirmPassword', 'Other@2026xy');
    await page.locator('.card button[type=submit]').click();
    check('reset: passwords must match', (await page.textContent('.card')).includes('do not match'));
    await page.fill('#ConfirmPassword', 'Reset@2026xy'); await page.locator('.card button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('reset: success -> log-in page', page.url().includes('/Account/Login') && (await toast(page)).includes('password was changed'));
    await page.goto(resetUrl);
    check('reset link works only once', (await page.textContent('.card')).includes('not valid'));
    await page.goto(BASE + '/Account/ResetPassword?token=made-up-token');
    check('made-up reset token rejected', (await page.textContent('.card')).includes('not valid'));
    await login(page, 'aylin.kaya@university.edu', 'Reset@2026xy');
    check('log in with the reset password', page.url().endsWith('/student'));

    // ================= materials =================
    await page.goto(BASE + '/materials'); await shot(page, 'p4-02-materials');
    check('seeded materials listed', (await page.locator('tbody tr').count()) === 5);
    await page.locator('form[action="/materials"] button[type=submit]').click();
    check('upload: client validation (course + file)', (await page.textContent('form[action="/materials"]')).includes('Please choose the course'));
    await page.selectOption('#Form_CourseId', { label: 'Data Structures' });
    await page.setInputFiles('#Form_File', txt); await page.locator('form[action="/materials"] button[type=submit]').click();
    check('upload: non-PDF rejected in the browser', (await page.textContent('form[action="/materials"]')).includes('PDF'));
    await page.setInputFiles('#Form_File', fakePdf); await page.locator('form[action="/materials"] button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('upload: renamed non-PDF rejected by the server', (await page.textContent('form[action="/materials"]')).includes('does not look like a real PDF'));
    await page.selectOption('#Form_CourseId', { label: 'Data Structures' });
    await page.setInputFiles('#Form_File', noTextPdf); await page.locator('form[action="/materials"] button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('upload: PDF without text marked as failed', (await toast(page)).includes('No text was found'), await toast(page));
    await page.selectOption('#Form_CourseId', { label: 'Data Structures' });
    await page.setInputFiles('#Form_File', goodPdf); await page.locator('form[action="/materials"] button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('upload: text PDF read (2 pages)', (await toast(page)).includes('2 page(s) were read'), await toast(page));
    await shot(page, 'p4-03-materials-uploaded');
    // toggle off/on as AI source (fetch)
    const syllabusToggle = page.locator('tr:has-text("Syllabus.pdf") [data-source-toggle]');
    await syllabusToggle.uncheck(); await page.waitForFunction(() => document.querySelector('#toast').textContent.includes('ignore'));
    check('toggle AI source off (fetch)', true);
    await syllabusToggle.check(); await page.waitForFunction(() => document.querySelector('#toast').textContent.includes('will use'));
    // delete rules
    await page.locator('tr:has-text("Lecture 5") button[aria-label^="Delete"]').click(); await page.waitForLoadState('networkidle');
    check('delete refused for material used by a quiz', (await toast(page)).includes('used by a quiz'), await toast(page));
    await page.locator('tr:has-text("scanned.pdf") button[aria-label^="Delete"]').click(); await page.waitForLoadState('networkidle');
    check('delete own unused material', (await toast(page)).includes('deleted') && !(await page.textContent('tbody')).includes('scanned.pdf'));

    // ================= assistant =================
    await page.goto(BASE + '/assistant'); await page.waitForLoadState('networkidle');
    check('seeded chat shown with sources', (await page.textContent('#chat')).includes('Sources:'));
    await page.click('button[data-modal="new-chat-modal"]');
    await page.locator('#new-chat-modal button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('new chat created', (await page.textContent('#chat')).includes('Ask me anything'));
    await page.locator('#ask-form button[type=submit]').click();
    check('empty question refused', (await toast(page)).includes('type a question'));
    await page.fill('#question', 'How are collisions resolved in a hash table?');
    await page.locator('#ask-form button[type=submit]').click();
    await page.waitForFunction(() => !document.querySelector('#chat').textContent.includes('Searching your materials'));
    const chatText = await page.textContent('#chat');
    check('grounded answer with page references', chatText.includes('Lecture 9 - Hash tables.pdf, p.1') && chatText.includes('Sources:'), chatText.slice(-200));
    await page.fill('#question', 'What is the best pizza topping?'); await page.locator('#ask-form button[type=submit]').click();
    await page.waitForFunction(() => document.querySelectorAll('#chat .bubble.ungrounded').length >= 1);
    const callsBefore = JSON.parse(await (await page.request.get(FAKE_AI + '/calls')).text()).calls;
    check('unrelated question: "not in your materials", no AI call', (await page.textContent('#chat')).includes('could not find this in your uploaded materials'));
    await page.fill('#question', 'Who invented the queue data structure?'); await page.locator('#ask-form button[type=submit]').click();
    await page.waitForFunction(() => document.querySelectorAll('#chat .bubble.ungrounded').length >= 2);
    const callsAfter = JSON.parse(await (await page.request.get(FAKE_AI + '/calls')).text()).calls;
    check('AI says the excerpts are not enough -> honest answer', callsAfter === callsBefore + 1);
    await page.fill('#question', 'Please crash on queues'); await page.locator('#ask-form button[type=submit]').click();
    await page.waitForFunction(() => document.querySelector('#toast').textContent.includes('AI service'));
    check('AI service error: friendly message, question kept', (await toast(page)).includes('returned an error') && (await page.inputValue('#question')) === 'Please crash on queues', await toast(page));
    check('AI service error: no technical details shown', !(await page.textContent('body')).includes('api_error'));
    await page.fill('#question', 'slow answer about queues please'); await page.locator('#ask-form button[type=submit]').click();
    await page.waitForFunction(() => document.querySelector('#toast').textContent.includes('too long'), null, { timeout: 15000 });
    check('AI timeout handled', true);
    const convUrl = page.url();
    await shot(page, 'p4-04-assistant');
    // question too long (server)
    const token = await page.inputValue('#antiforgery input');
    const conv = new URL(convUrl).searchParams.get('c');
    const long = await page.request.post(`${BASE}/assistant/${conv}/ask`, { headers: { RequestVerificationToken: token, 'Content-Type': 'application/json', Accept: 'application/json' }, data: { question: 'x'.repeat(501) } });
    check('question over 500 characters rejected by the server', long.status() === 400 && (await long.text()).includes('500'));
    const noToken = await page.request.post(`${BASE}/assistant/${conv}/ask`, { headers: { 'Content-Type': 'application/json', Accept: 'application/json' }, data: { question: 'hi' } });
    check('request without anti-forgery token rejected', noToken.status() === 400, String(noToken.status()));
    // reload: the conversation was saved
    await page.reload(); await page.waitForLoadState('networkidle');
    check('chat saved in the database (still there after reload)', (await page.textContent('#chat')).includes('How are collisions resolved'));
    await logout(page);

    // another student cannot see Aylin's chat or materials
    await login(page, 'can.ozkan@university.edu');
    const other = await page.goto(convUrl);
    check("another student cannot open someone else's chat", other.status() === 404, String(other.status()));
    await page.goto(BASE + '/materials');
    check("another student does not see someone else's materials", (await page.textContent('main')).includes('No materials yet'));
    const tok2 = await page.inputValue('#antiforgery input');
    const steal = await page.request.post(`${BASE}/assistant/${conv}/ask`, { headers: { RequestVerificationToken: tok2, 'Content-Type': 'application/json', Accept: 'application/json' }, data: { question: 'queues' } });
    check("cannot ask in someone else's chat", steal.status() === 404);
    await logout(page);
    // tutor cannot use the student assistant
    await login(page, 'elif.demir@university.edu');
    await page.goto(BASE + '/assistant');
    check('tutor role blocked from the student assistant', (await page.textContent('body')).includes('Access denied'));
    await logout(page);

    // ================= security =================
    const head = await page.request.get(BASE + '/Account/Login');
    const h = head.headers();
    check('security headers present', h['x-frame-options'] === 'DENY' && h['x-content-type-options'] === 'nosniff' && (h['content-security-policy'] || '').includes("frame-ancestors 'none'"));
    const sqli = await browser.newPage();
    await sqli.goto(BASE + '/Account/Login');
    await sqli.fill('#Email', "' OR '1'='1' -- @x.com"); await sqli.fill('#Password', "' OR '1'='1");
    await sqli.locator('.authBox button[type=submit]').click(); await sqli.waitForLoadState('networkidle');
    check('SQL injection in log-in does not work', sqli.url().includes('/Account/Login'));
    await sqli.close();

    // ================= small screens =================
    const phone = await browser.newContext({ ...devices['iPhone 13'] });
    const mp = await phone.newPage();
    mp.on('pageerror', e => errors.push(e.message));
    await mp.goto(BASE + '/'); await mp.screenshot({ path: path.join(OUT, 'p4-05-phone-landing.png') });
    const overflow = await mp.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
    check('phone: landing page has no sideways scrolling', !overflow);
    await mp.goto(BASE + '/Account/Login');
    await mp.fill('#Email', 'aylin.kaya@university.edu'); await mp.fill('#Password', 'Reset@2026xy');
    await mp.locator('.authBox button[type=submit]').click(); await mp.waitForLoadState('networkidle');
    check('phone: sidebar hidden, menu button visible', ((await mp.locator('#sidebar').boundingBox()).x + (await mp.locator('#sidebar').boundingBox()).width <= 1) && await mp.locator('.menu-btn').isVisible());
    await mp.click('.menu-btn'); await mp.waitForTimeout(400);
    await mp.screenshot({ path: path.join(OUT, 'p4-06-phone-menu.png') });
    check('phone: menu opens', await mp.locator('#sidebar a:has-text("My bookings")').isVisible());
    await mp.click('#sidebar a:has-text("My bookings")'); await mp.waitForLoadState('networkidle');
    check('phone: navigate with the menu', mp.url().endsWith('/bookings'));
    await mp.screenshot({ path: path.join(OUT, 'p4-07-phone-bookings.png'), fullPage: true });
    const pages = ['/student', '/tutors', '/bookings', '/assistant', '/materials', '/notifications', '/Account/Profile', '/bookings/9'];
    const wide = [];
    for (const url of pages) {
        await mp.goto(BASE + url); await mp.waitForLoadState('networkidle');
        if (await mp.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1)) wide.push(url);
    }
    check('phone: no student page scrolls sideways', wide.length === 0, wide.join(', '));
    await mp.goto(BASE + '/assistant'); await mp.screenshot({ path: path.join(OUT, 'p4-08-phone-assistant.png'), fullPage: true });
    await phone.close();

    const tablet = await browser.newContext({ ...devices['iPad (gen 7)'] });
    const tp = await tablet.newPage();
    await tp.goto(BASE + '/Account/Login'); await tp.fill('#Email', 'dana@peermind.ai'); await tp.fill('#Password', 'Demo@12345');
    await tp.locator('.authBox button[type=submit]').click(); await tp.waitForLoadState('networkidle');
    const twide = [];
    for (const url of ['/admin', '/admin/users', '/admin/tutor-verification', '/admin/courses', '/admin/moderation']) {
        await tp.goto(BASE + url); await tp.waitForLoadState('networkidle');
        if (await tp.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1)) twide.push(url);
    }
    check('tablet: no admin page scrolls sideways', twide.length === 0, twide.join(', '));
    await tp.goto(BASE + '/admin/courses'); await tp.screenshot({ path: path.join(OUT, 'p4-09-tablet-courses.png'), fullPage: true });
    await tablet.close();

    check('no browser errors', errors.length === 0, errors.join(' | '));
    await browser.close();
    const failed = results.filter(r => !r[0]);
    console.log(`\n${results.length - failed.length}/${results.length} passed`);
    
    process.exit(failed.length ? 1 : 0);
})().catch(e => { console.error('CRASH', e); process.exit(2); });
