# PeerMind AI

An AI-powered peer tutoring and learning platform for university students. Students ask an AI assistant about their own lecture notes, find and book verified peer tutors, and can apply to become tutors themselves.

**Course:** SOFT3111 – Internet and Web Programming, Fall 2026 · **Project #4** (final application)

**Group members**

| Name | Student ID |
|---|---|
| Ali Kanjo | 23COMP1065 |
| Ammar Azzan | 22COMP1048 |
| Kenan Aljabi | 21COMP1073 |

What each member implemented is described in [CONTRIBUTIONS.md](CONTRIBUTIONS.md). The test report is in [docs/TEST_REPORT.pdf](docs/TEST_REPORT.pdf).

## Purpose

University students often need help that matches their own courses. PeerMind AI combines two kinds of help:

- an **AI study assistant** that answers questions from the student's own course materials and shows the pages it used, and writes **practice quizzes** from them with study recommendations, and
- **peer tutoring** with students who did well in the course and were verified by an administrator.

There are three roles (Student, Tutor, Admin). Each role sees its own menu, and the server checks the role on every request.

## Main features

### AI study assistant (FR-08, FR-09)

1. A student uploads a PDF (lecture notes, slides exported to PDF) under *My materials*. The server reads the text page by page and stores it.
2. In the *AI assistant* the student asks a question. The server finds the pages that match the question best and sends **only those pages** to the AI (Claude or a Groq model) with the instruction to answer only from them.
3. The answer appears without a page reload, with references such as *Lecture 5 – Stacks.pdf, p.3*. Questions and answers are saved.
4. When the materials do not contain the answer, the assistant says so instead of guessing (Scenario 2 of the proposal). If nothing in the materials matches at all, the AI is not even called.

Each student's files and chats are private. A notice under every answer says that AI answers may contain errors (NFR-07).

### AI practice quizzes and study recommendations (FR-10, FR-11)

1. Under *Practice quizzes* the student picks one of their materials and the number of questions. The AI writes multiple-choice questions from it, and the server checks every question (four different options, exactly one correct answer, a real page number) before storing the quiz.
2. The student answers the questions and submits. The result shows the score, whether the pass mark (75%) was reached, the correct answers with explanations and the page each question came from.
3. Based on the missed questions the student gets recommendations: review a topic or particular pages, retry the quiz, or find a tutor for the course. They also appear on the dashboard and can be dismissed. Quizzes can be retried and deleted.

### Workflow 1 – Find and book a tutor

1. A student searches approved tutors by name, course, rating and availability. The results are loaded with the Fetch API as JSON, without reloading the page.
2. The student opens a tutor profile (qualifications, reviews, free time slots), picks a slot and sends a booking request.
3. The tutor accepts or declines the request, and later marks the finished session as completed (both with Fetch + JSON).
4. The student edits the notes, cancels the booking, or rates the completed session. The tutor can reply to reviews.

Two students can never book the same slot: the database enforces it with a unique key and a transaction.

### Workflow 2 – Become a tutor

1. A student fills in the tutor application: academic details, courses to teach and a transcript (PDF upload).
2. An administrator opens the application, downloads the transcript and approves it, rejects it with a reason, or asks for more information.
3. After approval the student becomes a tutor, appears in the tutor search and can add availability.

### Other features

- **Accounts:** registration restricted to university e-mail domains, e-mail verification step, log-in and log-out, lock-out after five failed log-ins, profile and settings page, **change password**, and **forgot password** with a one-time reset link.
- **Course library (admin):** create, list, search, edit and delete courses. A course that is in use is deactivated instead of deleted.
- **User management (admin):** search and filter users, suspend or re-activate an account, delete an account that has no activity.
- **Reports and moderation:** students and tutors report problems; the administrator dismisses, warns or suspends, and every decision is logged.
- **Tutor area:** dashboard, availability (add and remove time slots), reviews with replies, public profile editor, and **courses**: a tutor removes a course they no longer teach and applies to teach more courses, which an administrator approves (FR-07).
- **AI usage monitoring (admin):** requests, tokens, answer times and failures per day, feature and user, and a switch to turn the AI features off for one user (FR-13).
- **Notifications:** created by the database for bookings, reviews and application decisions; "mark all as read" uses Fetch.
- **Works on phones and tablets:** a menu button opens the sidebar on small screens and tables scroll inside their card.

## Technologies

| Layer | Technology |
|---|---|
| Backend | C#, ASP.NET Core MVC (.NET 8) |
| Database | MariaDB / MySQL, accessed with SQL stored procedures through `MySqlConnector` |
| Frontend | Razor views, HTML, CSS and plain JavaScript (Fetch API, no framework) |
| AI | Claude (Anthropic Messages API) or Groq (OpenAI-compatible API), called with `HttpClient` |
| PDF text | `PdfPig` |
| Passwords | bcrypt hashes (`BCrypt.Net-Next`) |
| Tests | Playwright browser tests (Node.js), see `tests/browser/` |

## Project structure

[docs/CODE_GUIDE.md](docs/CODE_GUIDE.md) explains, feature by feature, which class and stored procedure handle each action.

```
database/                 SQL scripts that create the database
  01_schema.sql             40 tables with keys, constraints and indexes
  02_procedures.sql         views, triggers and stored procedures (business rules)
  03_seed_data.sql          demo data
  99_reset_database.sql     removes everything (to start again)
docs/                     test report, code guide, Project #1 proposal and Project #2 prototype
PeerMindAI/               the ASP.NET Core MVC application
  Controllers/              one controller per area (Account, Assistant, Tutors, Bookings, Admin, ...)
  Models/                   entities and form models with validation attributes
  Data/                     database access: one repository class per area
  Services/                 AI client, PDF reading, file storage, log-in session check
  Helpers/                  small view helpers (icons, badges, date formats)
  Views/                    Razor pages
  wwwroot/css, wwwroot/js   stylesheet and JavaScript
tests/browser/            automated browser tests
```

Controllers handle requests, repositories talk to the database, views only display data. The business rules (double booking, cancellation window, minimum GPA, ...) live in the stored procedures, which raise a readable error message that the application shows to the user.

## Installation and running

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer (the project also runs on newer .NET versions)
- MariaDB 10.4+ or MySQL 8, for example the one included in [XAMPP](https://www.apachefriends.org/)
- Optional, for the AI features: a [Groq API key](https://console.groq.com/keys) (free tier) or an [Anthropic API key](https://console.anthropic.com/)

The NuGet packages (`MySqlConnector`, `BCrypt.Net-Next`, `PdfPig`) are downloaded automatically on the first build.

### 1. Initialize the database

With XAMPP: start **MySQL** (and **Apache** for phpMyAdmin), then open <http://localhost/phpmyadmin>.

1. Create a new database named `peermindai_db`.
2. Select it in the left sidebar and use **Import** to run these files **in this order**:
   1. `database/01_schema.sql` – tables
   2. `database/02_procedures.sql` – views, triggers and stored procedures
   3. `database/03_seed_data.sql` – demo data

Or from a terminal:

```bash
mysql -u root -e "CREATE DATABASE peermindai_db CHARACTER SET utf8mb4"
mysql -u root peermindai_db < database/01_schema.sql
mysql -u root peermindai_db < database/02_procedures.sql
mysql -u root peermindai_db < database/03_seed_data.sql
```

Import the demo data only once; a second import stops with "Duplicate entry". To start again from an empty database, import `database/99_reset_database.sql` and then the three files again.

### 2. Configure the connection

`PeerMindAI/appsettings.json` matches a standard XAMPP installation (user `root`, empty password, database `peermindai_db`):

```json
"ConnectionStrings": {
  "PeerMind": "Server=localhost;Port=3306;Database=peermindai_db;User ID=root;Password=;"
}
```

If your database name, user, password or port is different, do not edit `appsettings.json`. Create `PeerMindAI/appsettings.Local.json` with only the values that differ, for example:

```json
{
  "ConnectionStrings": {
    "PeerMind": "Server=localhost;Port=3306;Database=my_database;User ID=root;Password=secret;"
  }
}
```

`appsettings.Local.json` overrides `appsettings.json` and is excluded from Git, so passwords are never committed.

### 3. AI features (optional)

The AI study assistant and the practice quizzes need an API key from **Groq** or **Anthropic**. Add it to the same `appsettings.Local.json`.

With Groq (free tier available, open models such as Llama):

```json
{
  "AI": {
    "Provider": "Groq",
    "ApiKey": "gsk_..."
  }
}
```

With Anthropic (Claude):

```json
{
  "AI": {
    "Provider": "Anthropic",
    "ApiKey": "sk-ant-..."
  }
}
```

The default models are `llama-3.3-70b-versatile` for Groq and `claude-haiku-5-5` for Anthropic. To use another model, add `"Model": "..."` to the `AI` section; providers retire models over time, so check the provider's model list if a model stops working.

Without a key, everything else works: the AI pages explain that the AI is not configured, and materials can still be uploaded. A student can ask at most 30 questions and generate at most 10 quizzes per hour. Paid providers charge a small amount per request.

Never put the key in `appsettings.json`, and do not include `appsettings.Local.json` in a ZIP you share.

### 4. Run the application

```bash
cd PeerMindAI
```

```bash
dotnet run
```

Then open <http://localhost:5055>. In Visual Studio or VS Code (with the C# Dev Kit extension) you can instead open the project and press **F5**.

## Demo accounts and sample data

All demo accounts use the password `Demo@12345`. The log-in page has buttons that fill them in.

| Role | E-mail |
|---|---|
| Student | `aylin.kaya@university.edu` |
| Tutor | `elif.demir@university.edu` |
| Admin | `dana@peermind.ai` |

These are sample accounts from `03_seed_data.sql`; there are no real passwords or secrets in this repository. The demo data also contains 20 users, 6 approved tutors, 7 courses, bookings in every state, reviews, pending tutor applications, open reports, and four processed Data Structures materials with an AI chat for Aylin.

### Suggested demonstration

1. **Student (Aylin)** – open the *AI assistant* and ask "Explain LIFO vs FIFO"; upload a PDF under *My materials* and ask about it; generate a quiz under *Practice quizzes*, take it and look at the recommendations.
2. **Student** – search for a tutor, open *Elif Demir* and book a free slot; review the completed session with Mei Tanaka under *My bookings*.
3. **Tutor (Elif)** – open *Booking requests*, accept the new request and mark yesterday's session as completed; on *Tutor profile* see the courses she teaches.
4. **Register** a new account with an e-mail such as `you@isikun.edu.tr` (a Gmail address is rejected), verify it and submit a tutor application with any PDF.
5. **Admin (Dana)** – approve the application under *Tutor verification*, manage a course in the *Course library*, decide a report under *Moderation*, and open *AI usage*.
6. **Phone** – open the site on a phone or narrow the browser window and use the menu button.

## Validation, error handling and security

- Every form is validated in the browser (JavaScript) **and** again on the server (model validation attributes). Both show the same messages.
- Rules that need the database are enforced by the stored procedures, for example "This time slot is no longer available" or "A course with this code already exists".
- All SQL uses parameters or stored procedures, so user input is never part of the SQL text (protection against SQL injection).
- Passwords are stored as bcrypt hashes. Five failed log-ins lock an account for 15 minutes. Reset links work once and for 30 minutes, and "forgot password" gives the same answer for every e-mail, so nobody can find out which addresses have accounts.
- Every page checks the user's role on the server; a student can only open their own bookings, materials and chats.
- All POST requests need an anti-forgery token. Responses carry security headers (no framing, no type guessing, a content security policy), and the log-in cookie cannot be read by JavaScript.
- Uploaded PDFs are checked (type, size, real PDF header) and stored outside the public web folder.
- Users only see friendly messages: unknown and forbidden pages, a stopped database and AI failures show an explanation without technical details, which go to the server log.

## Testing

The test report ([docs/TEST_REPORT.pdf](docs/TEST_REPORT.pdf), source [docs/TEST_REPORT.md](docs/TEST_REPORT.md)) contains 52 test cases covering normal operation, invalid input, exceptional situations, security, speed and browser support. 151 automated browser checks back it, run in both Chromium and Microsoft Edge, plus a speed test (95% of page requests must finish within 2 seconds; measured: 95% within 25 ms). [tests/browser/README.md](tests/browser/README.md) explains how to run them.

## Notes

- **E-mails are simulated.** The project has no mail server. After registration, the verification page is shown directly. Password-reset e-mails are written to the terminal where the application runs; copy the link from there.
- **Payments and video calls** are out of scope, as stated in the proposal. Prices are shown for information only.
- **Database technology:** the proposal planned SQL Server with Entity Framework Core; the application uses MariaDB/MySQL with SQL stored procedures instead, which is what the course covers and what XAMPP provides.
- **AI roadmaps** (the section-by-section learning path of the Project #2 prototype) are not part of this version; they go beyond the functional requirements of the proposal.
- The seeded materials and transcripts are sample records whose text is in the database but whose files are not; files uploaded through the application are stored in `PeerMindAI/App_Data/uploads`.
- Data is stored in MariaDB/MySQL, so it is kept when the application or the database server is restarted.
