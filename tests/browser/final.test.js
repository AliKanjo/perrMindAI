// Final additions: AI practice quizzes and recommendations (FR-10, FR-11), tutors managing their
// courses (FR-07), AI usage page and AI access switch (FR-13), and the Groq provider.
// Needs a freshly imported database and the app started against fake-claude.js.
// Set PROVIDER=Groq when the app runs with AI__Provider=Groq: then only the AI checks run.
const { chromium, devices } = require('playwright');
const fs = require('fs');
const path = require('path');
const BASE = process.env.BASE_URL || 'http://localhost:5055';
const PROVIDER = process.env.PROVIDER || 'Anthropic';
const DIR = __dirname;
const OUT = path.join(DIR, 'shots'); fs.mkdirSync(OUT, { recursive: true });
const results = [];
const check = (name, ok, extra = '') => { results.push([ok, name]); console.log(ok ? 'PASS' : 'FAIL', name, extra); };
const shot = (page, name) => page.screenshot({ path: path.join(OUT, name + '.png'), fullPage: true });

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

async function makePdf(browser, file, html) {
    const page = await browser.newPage();
    await page.setContent(html);
    await page.pdf({ path: file });
    await page.close();
}

(async () => {
    const browser = await chromium.launch(process.env.BROWSER_CHANNEL ? { channel: process.env.BROWSER_CHANNEL } : {});
    const page = await (await browser.newContext({ viewport: { width: 1366, height: 850 } })).newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error' && !m.text().startsWith('Failed to load resource')) errors.push(m.text()); });
    page.on('dialog', d => d.accept());

    const transcript = path.join(DIR, 'transcript.pdf');
    await makePdf(browser, transcript, '<h1>Official transcript</h1><p>Databases: A</p>');

    // ================= practice quizzes (FR-10) =================
    await login(page, 'aylin.kaya@university.edu');
    await page.goto(BASE + '/quizzes');
    if (PROVIDER === 'Anthropic') {
        check('seeded quiz listed with its best score', (await page.textContent('main')).includes('Section 3 quiz') && (await page.textContent('main')).includes('60%'));
        await page.locator('#generate-form button[type=submit]').click();
        check('generate: material required (browser)', (await page.textContent('#generate-form')).includes('Please choose a material'));
    }
    await page.selectOption('#Form_MaterialId', { label: 'Lecture 6 – Queues.pptx (Data Structures)' });
    await page.selectOption('#Form_QuestionCount', '5');
    await page.locator('#generate-form button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check(`[${PROVIDER}] quiz generated and opened`, /\/quizzes\/attempts\/\d+$/.test(page.url()), page.url());
    check(`[${PROVIDER}] invalid AI question skipped (5 of 6 kept)`, (await page.locator('fieldset').count()) === 5);
    const attemptUrl = page.url();
    if (PROVIDER === 'Anthropic') await shot(page, 'f-01-quiz-take');

    if (PROVIDER === 'Anthropic') {
        await page.locator('#quiz-form button[type=submit]').click();
        check('quiz: every question must be answered (browser)', (await page.textContent('#quiz-form')).includes('Please choose an answer'));
        // server-side: post only one answer
        const token = await page.inputValue('#quiz-form input[name="__RequestVerificationToken"]');
        const firstName = await page.locator('fieldset input[type=radio]').first().getAttribute('name');
        const firstValue = await page.locator('fieldset input[type=radio]').first().getAttribute('value');
        const partial = await page.request.post(attemptUrl, { form: { __RequestVerificationToken: token, [firstName]: firstValue } });
        check('quiz: unanswered questions refused by the server', (await partial.text()).includes('Please answer every question'));
    }

    // answer A everywhere: questions 1 and 5 are A -> 2 of 5 = 40% (fail)
    for (const set of await page.locator('fieldset').all()) await set.locator('label.opt').first().click();
    check('progress bar counts answers', (await page.textContent('#progress-text')).startsWith('5 of 5'));
    await page.locator('#quiz-form button[type=submit]').click(); await page.waitForLoadState('networkidle');
    const resultText = await page.textContent('main');
    check(`[${PROVIDER}] result: 40%, not passed`, resultText.includes('40%') && resultText.includes('Not quite there yet'));
    check('result: explanations and page references shown', resultText.includes('Why:') && resultText.includes('Source: Lecture 6 – Queues.pptx, p.'));
    check('recommendations: review topics, retry, find a tutor (FR-11)',
        resultText.includes('Review "') && resultText.includes('pass mark is 75%') && resultText.includes('A verified tutor can help you'));
    const resultUrl = page.url();
    if (PROVIDER === 'Anthropic') await shot(page, 'f-02-quiz-result');

    // retry and answer correctly (A, B, C, D, A)
    await page.locator('main form[action$="/start"] button').click(); await page.waitForLoadState('networkidle');
    const sets = await page.locator('fieldset').all();
    for (let i = 0; i < sets.length; i++) await sets[i].locator('label.opt').nth(i % 4).click();
    await page.locator('#quiz-form button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('retry: 100% and passed', (await page.textContent('main')).includes('100%') && (await page.textContent('main')).includes('You passed'));
    await page.goto(attemptUrl);
    check('a submitted attempt opens its result instead', page.url().includes('/quizzes/results/'));

    if (PROVIDER === 'Groq') {
        await page.goto(BASE + '/assistant'); await page.waitForLoadState('networkidle');
        await page.fill('#question', 'What does a queue do with enqueue and dequeue?'); await page.locator('#ask-form button[type=submit]').click();
        await page.waitForFunction(() => !document.querySelector('#chat').textContent.includes('Searching your materials'));
        check('[Groq] assistant answer with page references', (await page.textContent('#chat')).includes('Sources:'));
        await logout(page);
        await login(page, 'dana@peermind.ai');
        await page.goto(BASE + '/admin/ai-usage');
        check('[Groq] AI usage page shows the Groq provider', (await page.textContent('.ph')).includes('Groq · openai/gpt-oss-120b'));
        finish(); return;
    }

    // citations written as 【1】 (as gpt-oss does) are turned into page references too
    await page.goto(BASE + '/assistant?c=1'); await page.waitForLoadState('networkidle');
    await page.fill('#question', 'Explain LIFO vs FIFO'); await page.locator('#ask-form button[type=submit]').click();
    await page.waitForFunction(() => !document.querySelector('#chat').textContent.includes('Searching your materials'));
    const lastAnswer = await page.locator('#chat .bubble.ai').last().textContent();
    check('citations written as \u30101\u3011 become page references', !lastAnswer.includes('\u3010') && lastAnswer.includes('[Lecture 5'), lastAnswer.slice(0, 120));

    // dashboard recommendations (FR-11) and dismiss with fetch
    await page.goto(BASE + '/student');
    const recs = await page.locator('[data-recommendation]').count();
    check('dashboard shows study recommendations', recs >= 3, String(recs));
    const dismissedId = await page.locator('[data-recommendation]').first().getAttribute('data-recommendation');
    await page.locator('[data-dismiss-recommendation]').first().click();
    await page.waitForFunction(n => document.querySelectorAll('[data-recommendation]:not(.hide)').length === n - 1, recs);
    await page.reload();
    check('dismissed recommendation stays hidden after reload (saved)', (await page.locator(`[data-recommendation="${dismissedId}"]`).count()) === 0);
    await shot(page, 'f-03-dashboard-recommendations');

    // unusable AI answer -> friendly error, nothing stored
    const broken = path.join(DIR, 'broken-material.pdf');
    await makePdf(browser, broken, '<h1>Queues</h1><p>A queue is first in first out. Enqueue adds at the rear.</p>');
    await page.goto(BASE + '/materials');
    await page.selectOption('#Form_CourseId', { label: 'Data Structures' });
    await page.setInputFiles('#Form_File', broken); await page.locator('form[action="/materials"] button[type=submit]').click(); await page.waitForLoadState('networkidle');
    await page.goto(BASE + '/quizzes');
    const before = await page.locator('tbody tr').count();
    await page.selectOption('#Form_MaterialId', { label: 'broken-material.pdf (Data Structures)' });
    await page.locator('#generate-form button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('unusable AI quiz: friendly error', (await page.textContent('main')).includes('could not create a usable quiz'));
    check('unusable AI quiz: nothing stored', (await page.locator('tbody tr').count()) === before);

    // delete a generated quiz
    await page.locator('tbody tr', { hasText: 'Practice quiz' }).first().locator('button[aria-label^="Delete"]').click(); await page.waitForLoadState('networkidle');
    check('delete a generated quiz', (await toast(page)).includes('deleted') && (await page.locator('tbody tr').count()) === before - 1);
    await logout(page);

    // another student cannot use Aylin's quizzes
    await login(page, 'can.ozkan@university.edu');
    check("another student cannot open someone else's result", (await page.goto(resultUrl)).status() === 404);
    const tok = await page.inputValue('#antiforgery input');
    const start = await page.request.post(BASE + '/quizzes/1/start', { form: { __RequestVerificationToken: tok } });
    check("another student cannot start someone else's quiz", start.status() === 404);
    await logout(page);

    // ================= tutors manage their courses (FR-07) =================
    await login(page, 'elif.demir@university.edu');
    await page.goto(BASE + '/tutor/profile');
    check('tutor profile lists the courses taught', (await page.locator('#tutor-courses .li').count()) === 2);
    await page.locator('#tutor-courses .li', { hasText: 'Data Structures' }).locator('button').click(); await page.waitForLoadState('networkidle');
    check('remove refused while sessions for the course are upcoming', (await toast(page)).includes('upcoming sessions'), await toast(page));
    await logout(page);

    await login(page, 'sara.haddad@university.edu');
    await page.goto(BASE + '/tutor/profile');
    await page.locator('#tutor-courses .li', { hasText: 'Data Structures' }).locator('button').click(); await page.waitForLoadState('networkidle');
    check('remove a course without upcoming sessions', (await toast(page)).includes('removed') && (await page.locator('#tutor-courses .li').count()) === 1);
    await page.locator('#tutor-courses .li').first().locator('button').click(); await page.waitForLoadState('networkidle');
    check('the last course cannot be removed', (await toast(page)).includes('at least one course'));
    await shot(page, 'f-04-tutor-courses');
    await page.click('a:has-text("Apply to teach more courses")'); await page.waitForLoadState('networkidle');
    check('apply for more courses: form opens for an approved tutor', (await page.textContent('h1')).includes('Apply to teach more courses'));
    check('courses already taught are not offered again', !(await page.textContent('form[action="/tutor/application"]')).includes('Analysis of Algorithms'));
    await page.click('label.chip:has-text("Databases")');
    await page.setInputFiles('#Form_Transcript', transcript);
    await page.locator('form[action="/tutor/application"] button[type=submit]').click(); await page.waitForLoadState('networkidle');
    check('new course application submitted (pending)', (await page.textContent('main')).includes('under review'));
    await logout(page);

    await login(page, 'dana@peermind.ai');
    await page.goto(BASE + '/admin/tutor-verification');
    await page.click('a.li:has-text("Sara Haddad")'); await page.waitForLoadState('networkidle');
    await page.click('button:has-text("Approve")'); await page.waitForLoadState('networkidle');
    check('admin approves the new course', (await toast(page)).includes('approved'));

    // ================= AI usage page and AI access (FR-13) =================
    await page.goto(BASE + '/admin/ai-usage');
    const usage = await page.textContent('main');
    check('AI usage page: totals, features and failures', usage.includes('Practice quiz generator') && usage.includes('AI study assistant') && usage.includes('Recent failures'));
    check('AI usage page: provider and model shown', (await page.textContent('.ph')).includes('Anthropic · claude'));
    await shot(page, 'f-05-ai-usage');
    await page.goto(BASE + '/admin/users/2');
    await page.click('button:has-text("Turn off")'); await page.waitForLoadState('networkidle');
    check('admin turns AI off for a user', (await toast(page)).includes('turned off'));
    await logout(page);

    await login(page, 'sara.haddad@university.edu');
    await page.goto(BASE + '/tutor/profile');
    check('approved course appears on the tutor profile', (await page.textContent('#tutor-courses')).includes('Databases'));
    await logout(page);

    await login(page, 'aylin.kaya@university.edu');
    await page.goto(BASE + '/assistant?c=1');
    check('AI off: assistant explains it and disables the input', (await page.textContent('main')).includes('turned off') && await page.isDisabled('#question'));
    await page.goto(BASE + '/quizzes');
    check('AI off: quiz generation disabled', await page.isDisabled('#generate-form button[type=submit]'));
    const tok2 = await page.inputValue('#antiforgery input');
    const ask = await page.request.post(BASE + '/assistant/1/ask', { headers: { RequestVerificationToken: tok2, 'Content-Type': 'application/json', Accept: 'application/json' }, data: { question: 'queues' } });
    check('AI off: the server refuses questions too', ask.status() === 403);
    await logout(page);
    await login(page, 'dana@peermind.ai');
    await page.goto(BASE + '/admin/users/2'); await page.click('button:has-text("Turn on")'); await page.waitForLoadState('networkidle');
    await logout(page);

    // ================= phone =================
    const phone = await browser.newContext({ ...devices['iPhone 13'] });
    const mp = await phone.newPage();
    await login(mp, 'aylin.kaya@university.edu');
    const wide = [];
    for (const url of ['/quizzes', '/quizzes/results/1', '/student']) {
        await mp.goto(BASE + url); await mp.waitForLoadState('networkidle');
        if (await mp.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) wide.push(url);
    }
    await mp.goto(BASE + '/quizzes/results/1'); await mp.screenshot({ path: path.join(OUT, 'f-06-phone-result.png'), fullPage: true });
    check('phone: quiz pages do not scroll sideways', wide.length === 0, wide.join(', '));
    await phone.close();

    finish();

    function finish() {
        check('no browser errors', errors.length === 0, errors.join(' | '));
        browser.close().then(() => {
            const failed = results.filter(r => !r[0]).length;
            console.log(`\n${results.length - failed}/${results.length} passed`);
            process.exit(failed ? 1 : 0);
        });
    }
})().catch(e => { console.error('CRASH', e); process.exit(2); });
