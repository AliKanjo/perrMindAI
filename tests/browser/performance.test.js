// NFR-03: "at least 95% of ordinary database-backed page requests complete within 2 seconds".
// Logs in as each role, requests every main page many times (one at a time, then 10 at once)
// and reports the median, the 95th percentile and the slowest time. AI requests are excluded,
// as the requirement allows. Needs the app running with the demo data.
const { chromium } = require('playwright');
const BASE = process.env.BASE_URL || 'http://localhost:5055';
const ROUNDS = Number(process.env.ROUNDS || 25);
const LIMIT_MS = 2000;

const PAGES = {
    'aylin.kaya@university.edu': ['/student', '/tutors', '/tutors/search?courseId=1', '/tutors/3', '/bookings', '/bookings/9',
        '/assistant', '/materials', '/quizzes', '/quizzes/results/1', '/notifications', '/Account/Profile'],
    'elif.demir@university.edu': ['/tutor', '/tutor/requests', '/tutor/availability', '/tutor/reviews', '/tutor/profile'],
    'dana@peermind.ai': ['/admin', '/admin/users', '/admin/tutor-verification', '/admin/courses', '/admin/moderation', '/admin/ai-usage']
};

const percentile = (values, p) => {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.min(sorted.length - 1, Math.ceil(p / 100 * sorted.length) - 1)];
};

async function timed(request, url) {
    const start = performance.now();
    const response = await request.get(BASE + url, { headers: url.includes('/search') ? { Accept: 'application/json' } : {} });
    await response.body();
    return { ms: performance.now() - start, status: response.status() };
}

(async () => {
    const browser = await chromium.launch(process.env.BROWSER_CHANNEL ? { channel: process.env.BROWSER_CHANNEL } : {});
    const all = [];
    const perPage = [];
    let errors = 0;

    for (const [email, urls] of Object.entries(PAGES)) {
        const context = await browser.newContext();
        const page = await context.newPage();
        await page.goto(BASE + '/Account/Login');
        await page.fill('#Email', email); await page.fill('#Password', 'Demo@12345');
        await page.locator('.authBox button[type=submit]').click(); await page.waitForLoadState('networkidle');

        for (const url of urls) {
            await timed(context.request, url);   // warm-up, not counted
            const times = [];
            for (let i = 0; i < ROUNDS; i++) {
                const { ms, status } = await timed(context.request, url);
                if (status !== 200) errors++;
                times.push(ms);
            }
            // 10 requests at the same time (several users at once)
            const parallel = await Promise.all(Array.from({ length: 10 }, () => timed(context.request, url)));
            parallel.forEach(r => { if (r.status !== 200) errors++; times.push(r.ms); });
            all.push(...times);
            perPage.push({ url, median: percentile(times, 50), p95: percentile(times, 95), max: Math.max(...times) });
        }
        await context.close();
    }
    await browser.close();

    const within = all.filter(ms => ms <= LIMIT_MS).length;
    console.log('Page'.padEnd(30), 'median', '   p95', '   max  (ms)');
    for (const row of perPage) {
        console.log(row.url.padEnd(30), row.median.toFixed(0).padStart(6), row.p95.toFixed(0).padStart(6), row.max.toFixed(0).padStart(6));
    }
    console.log(`\nRequests: ${all.length}, errors: ${errors}`);
    console.log(`Median ${percentile(all, 50).toFixed(0)} ms, 95th percentile ${percentile(all, 95).toFixed(0)} ms, slowest ${Math.max(...all).toFixed(0)} ms`);
    console.log(`Within ${LIMIT_MS} ms: ${within} of ${all.length} (${(100 * within / all.length).toFixed(1)}%)`);
    const ok = errors === 0 && within / all.length >= 0.95;
    console.log(ok ? 'PASS NFR-03' : 'FAIL NFR-03');
    process.exit(ok ? 0 : 1);
})().catch(e => { console.error('CRASH', e); process.exit(2); });
