# Automated browser tests

These scripts drive a real Chromium browser through the application with
[Playwright](https://playwright.dev) and print `PASS` or `FAIL` for every check.
The results are summarised in [docs/TEST_REPORT.md](../../docs/TEST_REPORT.md).

| Script | What it covers | Checks |
|---|---|---|
| `workflows.test.js` | log-in, registration, tutor application and approval, course CRUD, users, availability, tutor search (Fetch), booking, double booking, reviews, accept/complete (Fetch), cancellation | 47 |
| `moderation.test.js` | reporting a user and a review, admin decisions and their effects | 20 |
| `project4.test.js` | change and reset password, material upload, AI assistant, access control, security headers, SQL injection, phone and tablet layouts | 48 |

## Running them

1. Install Node.js 18+ and, in this folder, the test tool:

   ```bash
   npm install
   npx playwright install chromium
   ```

2. Import the database again (`99_reset_database.sql`, then `01`, `02`, `03`). Every script expects the
   demo data exactly as it is after a fresh import, so re-import it before each script.

3. Start the stand-in for the Claude API (only needed for `project4.test.js`); it answers like the real
   API, costs nothing, and can simulate errors and time-outs:

   ```bash
   node fake-claude.js
   ```

4. Start the application pointed at it, writing its output to `app.log` in this folder
   (the password-reset test reads the demo e-mail from there). From the `PeerMindAI` folder:

   ```bash
   AI__ApiKey=test-key AI__BaseUrl=http://localhost:5099 AI__TimeoutSeconds=3 dotnet run > ../tests/browser/app.log
   ```

   On Windows PowerShell set the variables first, for example `$env:AI__ApiKey="test-key"`.

5. Run a script:

   ```bash
   node project4.test.js
   ```

Screenshots are written to `shots/`. Environment variables `BASE_URL`, `FAKE_AI_URL` and `APP_LOG`
change the defaults (`http://localhost:5055`, `http://localhost:5099`, `./app.log`).
