// Project #3 workflows: accounts, booking, tutor application, admin, courses. Needs a freshly imported database.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const BASE = process.env.BASE_URL || 'http://localhost:5055';
const OUT = path.join(__dirname, 'shots');
fs.mkdirSync(OUT, { recursive: true });

const results = [];
const check = (name, ok, extra = '') => { results.push([ok ? 'PASS' : 'FAIL', name, extra]); console.log(ok ? 'PASS' : 'FAIL', name, extra); };

async function login(page, email, password = 'Demo@12345') {
    await page.goto(BASE + '/Account/Login');
    await page.fill('#Email', email);
    await page.fill('#Password', password);
    await page.locator('button[type=submit]:not([title="Log out"])').first().click();
    await page.waitForLoadState('networkidle');
}
const logout = async page => { await page.click('button[title="Log out"]'); await page.waitForLoadState('networkidle'); };
const shot = (page, name) => page.screenshot({ path: path.join(OUT, name + '.png'), fullPage: true });
const toastText = async page => (await page.textContent('#toast')) || '';

(async () => {
    const browser = await chromium.launch();
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 850 } });
    const page = await ctx.newPage();
    const errors = [];
    page.on('pageerror', e => errors.push('pageerror: ' + e.message));
    page.on('console', m => { if (m.type() === 'error') errors.push('console: ' + m.text()); });
    page.on('dialog', d => d.accept());

    // ---- public + validation ----
    await page.goto(BASE + '/'); await shot(page, '01-landing');
    check('landing shows tutors from DB', (await page.locator('.mock .av').count()) === 3);

    await page.goto(BASE + '/Account/Login');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click();
    check('login client validation', (await page.textContent('[data-valmsg-for="Email"]')).includes('Please enter'));
    await login(page, 'aylin.kaya@university.edu', 'wrong-password');
    check('login wrong password rejected', (await page.textContent('.alert-bad')).includes('Incorrect'));
    await login(page, 'sam.r@university.edu');
    check('suspended user rejected', (await page.textContent('.alert-bad')).includes('suspended'));

    // ---- registration with a non-university e-mail (server rule) ----
    await page.goto(BASE + '/Account/Register');
    await page.fill('#FullName', 'Test Person'); await page.fill('#Email', 'someone@gmail.com');
    await page.fill('#Password', 'short'); await page.fill('#ConfirmPassword', 'different');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click();
    check('register client validation (password)', (await page.textContent('[data-valmsg-for="Password"]')).length > 5);
    await page.fill('#Password', 'Strong@Pass123'); await page.fill('#ConfirmPassword', 'Strong@Pass123');
    await page.check('#AcceptTerms');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('register gmail rejected by server', (await page.textContent('[data-valmsg-for="Email"]')).includes('university'));
    await shot(page, '02-register-error');

    // ---- register a new student who wants to tutor ----
    const stamp = Date.now();
    const newEmail = `new.tutor${stamp}@isikun.edu.tr`;
    await page.fill('#Email', newEmail); await page.fill('#Password', 'Strong@Pass123'); await page.fill('#ConfirmPassword', 'Strong@Pass123');
    await page.fill('#FullName', 'Nil Tutor');
    await page.locator('label.rolec').nth(1).click();
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('register -> verify page', page.url().includes('VerifyEmail'), page.url());
    await shot(page, '03-verify');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('verify -> tutor application', page.url().endsWith('/tutor/application'), page.url());

    // application: invalid first (low GPA, no course, no file)
    await page.selectOption('#Form_UniversityId', { label: 'Işık University' });
    await page.selectOption('#Form_Degree', 'BSc'); await page.fill('#Form_Major', 'Software Engineering');
    await page.fill('#Form_Gpa', '2.5'); await page.selectOption('#Form_YearOfStudy', '3');
    await page.fill('#Form_Bio', 'I enjoy helping classmates understand databases and SQL.');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click();
    check('application client validation (course+file)', (await page.textContent('#courses-error')).includes('at least one'));
    // valid file + course, but GPA too low -> server-side rule
    const pdf = path.join(__dirname, 'transcript.pdf');
    fs.writeFileSync(pdf, '%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n');
    const fake = path.join(__dirname, 'fake.pdf'); fs.writeFileSync(fake, 'this is not a pdf');
    await page.click('label.chip:has-text("Databases")');
    await page.setInputFiles('#Form_Transcript', fake);
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    const body1 = await page.textContent('main');
    check('server rejects low GPA', body1.includes('GPA of at least'));
    check('server rejects fake PDF', body1.includes('does not look like a real PDF'));
    await shot(page, '04-application-errors');
    await page.fill('#Form_Gpa', '3.6'); await page.setInputFiles('#Form_Transcript', pdf);
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('application submitted -> status pending', page.url().endsWith('/tutor/verification') && (await page.textContent('main')).includes('under review'), page.url());
    await shot(page, '05-application-pending');
    await logout(page);

    // ---- admin approves ----
    await login(page, 'dana@peermind.ai');
    check('admin lands on dashboard', page.url().endsWith('/admin'), page.url());
    await shot(page, '06-admin-dashboard');
    await page.goto(BASE + '/admin/tutor-verification'); await page.waitForLoadState('networkidle');
    await page.click('a.li:has-text("Nil Tutor")'); await page.waitForLoadState('networkidle');
    await shot(page, '07-admin-verification');
    check('admin sees uploaded transcript', (await page.locator('a:has-text("Open PDF")').count()) === 1);
    const pdfResp = await page.request.get(BASE + await page.getAttribute('a:has-text("Open PDF")', 'href'));
    check('transcript downloads for admin', pdfResp.status() === 200 && (await pdfResp.body()).toString().startsWith('%PDF'));
    // reject empty reason -> client validation
    await page.click('button[data-modal="reject-modal"]'); await page.click('#reject-modal button[type=submit]');
    check('reject needs a reason', (await page.textContent('#reject-modal [data-valmsg-for="Note"]')).includes('reason'));
    await page.click('#reject-modal [data-close]');
    await page.click('button:has-text("Approve")'); await page.waitForLoadState('networkidle');
    check('approve toast', (await toastText(page)).includes('approved'), await toastText(page));

    // ---- admin course CRUD ----
    await page.goto(BASE + '/admin/courses'); await shot(page, '08-courses');
    await page.goto(BASE + '/admin/courses/create');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click();
    check('course client validation', (await page.textContent('[data-valmsg-for="Code"]')).includes('course code'));
    await page.fill('#Code', 'comp2112'); await page.fill('#Name', 'Duplicate course');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('duplicate course code rejected by DB', (await page.textContent('main')).includes('already exists'));
    const code = 'TST' + String(stamp).slice(-5);
    await page.fill('#Code', code); await page.fill('#Name', 'Test Course'); await page.fill('#Department', 'Testing');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('course created', (await page.textContent('main')).includes(code));
    await page.click(`tr:has-text("${code}") a:has-text("Edit")`); await page.waitForLoadState('networkidle');
    await page.fill('#Name', 'Test Course Renamed'); await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('course updated', (await page.textContent('main')).includes('Test Course Renamed'));
    await page.click(`tr:has-text("${code}") button:has-text("Delete")`); await page.waitForLoadState('networkidle');
    check('course deleted', !(await page.textContent('main')).includes(code), await toastText(page));
    await page.click('tr:has-text("COMP2112") button:has-text("Delete")'); await page.waitForLoadState('networkidle');
    check('course in use only deactivated', (await toastText(page)).includes('hidden instead'), await toastText(page));
    // re-activate it
    await page.click('tr:has-text("COMP2112") a:has-text("Edit")'); await page.waitForLoadState('networkidle');
    await page.check('#IsActive'); await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');

    // users
    await page.goto(BASE + '/admin/users?q=sam'); await shot(page, '09-users');
    check('user search', (await page.locator('tbody tr').count()) === 1);
    await page.goto(BASE + '/admin/users?q=zzzz');
    check('user search empty state', (await page.textContent('main')).includes('No users found'));
    await logout(page);

    // ---- new tutor adds availability ----
    await login(page, newEmail, 'Strong@Pass123');
    check('approved student is now tutor', page.url().endsWith('/tutor'), page.url());
    await page.goto(BASE + '/tutor/availability');
    const d = new Date(Date.now() + 3 * 86400000).toISOString().slice(0, 10);
    await page.fill('#Form_Date', d); await page.fill('#Form_StartTime', '10:00');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('slot added', (await page.textContent('main')).includes('10:00 – 11:00'), await toastText(page));
    await page.fill('#Form_Date', d); await page.fill('#Form_StartTime', '10:30');
    await page.locator('button[type=submit]:not([title="Log out"])').first().click(); await page.waitForLoadState('networkidle');
    check('overlapping slot rejected', (await page.textContent('main')).includes('overlaps'));
    await shot(page, '10-availability');
    await logout(page);

    // ---- student: search + book ----
    await login(page, 'aylin.kaya@university.edu');
    check('student lands on dashboard', page.url().endsWith('/student'), page.url());
    await shot(page, '11-student-dashboard');
    await page.goto(BASE + '/tutors'); await page.waitForSelector('#tutor-results .card a');
    check('search lists all approved tutors (7)', (await page.locator('#tutor-results .card a').count()) === 7, String(await page.locator('#tutor-results .card a').count()));
    await shot(page, '12-tutor-search');
    await page.selectOption('#filter-course', { label: 'Databases' });
    await page.waitForFunction(() => document.querySelector('#tutor-count').textContent.startsWith('2 '));
    check('async course filter (Databases -> 2 incl. new tutor)', (await page.textContent('#tutor-results')).includes('Nil Tutor'));
    await page.selectOption('#filter-course', ''); await page.fill('#filter-search', 'zzzzzz');
    await page.waitForFunction(() => document.querySelector('#tutor-count').textContent.includes('No tutors'));
    check('search empty state', true);
    await page.fill('#filter-search', 'Elif');
    await page.waitForFunction(() => document.querySelector('#tutor-count').textContent.startsWith('1 '));
    await page.click('#tutor-results a:has-text("View profile")'); await page.waitForLoadState('networkidle');
    await shot(page, '13-tutor-profile');
    await page.click('#booking-form button[type=submit]');
    check('booking needs a slot (client)', (await page.textContent('[data-valmsg-for="SlotId"]')).includes('pick a time'));
    const slotLabel = page.locator('[data-day-panel]:not(.hide) label.slot').first();
    const slotId = await slotLabel.locator('input').getAttribute('value');
    await slotLabel.click();
    check('total price updates', (await page.textContent('#booking-total')).includes('$25'), await page.textContent('#booking-total'));
    await page.selectOption('#CourseId', { label: 'Data Structures' });
    await page.fill('#Topic', 'E2E test topic');
    await page.click('#booking-form button[type=submit]'); await page.waitForLoadState('networkidle');
    check('booking created -> details Pending', /\/bookings\/\d+$/.test(page.url()) && (await page.textContent('.ph')).includes('Pending'), page.url());
    const bookingUrl = page.url(); const bookingId = bookingUrl.split('/').pop();
    await shot(page, '14-booking-details');

    // double booking: a second student posts the same slot
    const ctx2 = await browser.newContext(); const p2 = await ctx2.newPage();
    await login(p2, 'can.ozkan@university.edu');
    await p2.goto(BASE + '/tutors/3');
    const token = await p2.inputValue('#booking-form input[name="__RequestVerificationToken"]');
    const resp = await p2.request.post(BASE + '/bookings', { form: { __RequestVerificationToken: token, TutorId: '3', SlotId: slotId, CourseId: '1' } });
    check('double booking rejected', (await resp.text()).includes('no longer available'));
    // other student cannot open someone else's booking
    const r2 = await p2.goto(bookingUrl);
    check('booking of another student -> 404', r2.status() === 404, String(r2.status()));
    const r3 = await p2.goto(BASE + '/admin');
    check('student blocked from admin', (await p2.textContent('body')).includes('Access denied'), p2.url());
    await ctx2.close();

    // edit notes
    await page.fill('#Notes', 'Please bring examples.'); await page.click('button:has-text("Save notes")'); await page.waitForLoadState('networkidle');
    check('notes updated', (await page.inputValue('#Notes')) === 'Please bring examples.');
    // review of completed booking 2
    await page.goto(BASE + '/bookings/2');
    await page.click('button:has-text("Submit review")');
    check('review needs rating (client)', (await page.textContent('[data-valmsg-for="Rating"]')).includes('rating'));
    await page.click('label[for="rating-5"]'); await page.fill('#Comment', 'Great session, very clear.');
    await page.click('button:has-text("Submit review")'); await page.waitForLoadState('networkidle');
    check('review saved', (await page.textContent('#review')).includes('rated 5'));
    await page.goto(BASE + '/bookings'); await shot(page, '15-my-bookings');
    await page.goto(BASE + '/notifications'); await shot(page, '16-notifications');
    await page.click('#read-all'); await page.waitForFunction(() => document.querySelectorAll('.unread').length === 0);
    check('notifications marked read (fetch)', true);
    await logout(page);

    // ---- tutor Elif: accept + complete ----
    await login(page, 'elif.demir@university.edu');
    await shot(page, '17-tutor-dashboard');
    await page.goto(BASE + '/tutor/requests'); await shot(page, '18-tutor-requests');
    const card = page.locator(`[data-booking="${bookingId}"]`);
    await card.locator('button:has-text("Accept")').click();
    await page.waitForFunction(id => document.querySelector(`[data-booking="${id}"] [data-status]`).textContent.includes('Confirmed'), bookingId);
    check('tutor accepted via fetch', true);
    const done = page.locator('[data-booking="18"]');
    await done.locator('button:has-text("Mark as completed")').click();
    await page.waitForFunction(() => document.querySelector('[data-booking="18"] [data-status]').textContent.includes('Completed'));
    check('tutor completed past session via fetch', true);
    await page.goto(BASE + '/tutor/reviews'); await shot(page, '19-tutor-reviews');
    await page.goto(BASE + '/tutor/profile'); await shot(page, '20-tutor-profile-edit');
    await page.fill('#HourlyRate', '-5'); await page.locator('button[type=submit]:not([title="Log out"])').first().click();
    check('tutor profile validation', (await page.textContent('[data-valmsg-for="HourlyRate"]')).includes('between'));
    await logout(page);

    // ---- student cancels the confirmed booking? window >24h -> allowed if slot is far enough
    await login(page, 'aylin.kaya@university.edu');
    await page.goto(bookingUrl);
    await page.click('button[data-modal="cancel-modal"]'); await page.fill('#Reason', 'E2E test: no longer needed.');
    await page.click('#cancel-modal button[type=submit]'); await page.waitForLoadState('networkidle');
    const t = await toastText(page);
    check('cancel handled (cancelled or 24h rule message)', t.includes('cancelled') || t.includes('hours before'), t);
    await shot(page, '21-booking-after-cancel');

    check('no browser console/page errors', errors.length === 0, errors.join(' | '));
    await browser.close();
    const failed = results.filter(r => r[0] === 'FAIL');
    console.log(`\n${results.length - failed.length}/${results.length} passed`);
    process.exit(failed.length ? 1 : 0);
})().catch(e => { console.error('CRASH', e); process.exit(2); });
