# PeerMind AI

A peer tutoring platform for university students: find a verified tutor, book a session, and apply to become a tutor yourself.

**Course:** SOFT3111 – Internet and Web Programming, Fall 2026 · **Project #3** (functional web application)

**Group members**

| Name | Student ID |
|---|---|
| Ali Kanjo | 23COMP1065 |
| Ammar Azzan | 22COMP1048 |
| Kenan Aljabi | 21COMP1073 |

What each member implemented is described in [CONTRIBUTIONS.md](CONTRIBUTIONS.md).

## Implemented functionality

The application has three roles (Student, Tutor, Admin). Each role sees its own menu, and the server checks the role on every request.

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

- **Accounts:** registration restricted to university e-mail domains, e-mail verification step, log-in and log-out, lock-out after five failed log-ins, profile and settings page.
- **Course library (admin):** create, list, search, edit and delete courses. A course that is in use is deactivated instead of deleted.
- **User management (admin):** search and filter users, suspend or re-activate an account, delete an account that has no activity.
- **Reports and moderation:** a student or tutor can report the other person of a booking, and a tutor can report a review. The administrator dismisses the report, warns the user or suspends the user; warning or suspending also hides a reported review and recalculates the tutor's rating. Every decision appears in a decision log.
- **Tutor area:** dashboard, availability (add and remove time slots), reviews with replies, public profile editor.
- **Notifications:** created by the database for bookings, reviews and application decisions; "mark all as read" uses Fetch.

The AI features of the proposal (roadmap, study assistant, quiz generation) are not part of this version.

## Technology

| Layer | Technology |
|---|---|
| Backend | C#, ASP.NET Core MVC (.NET 8) |
| Database | MariaDB / MySQL, accessed with SQL stored procedures through `MySqlConnector` |
| Frontend | Razor views, HTML, CSS and plain JavaScript (no framework) |
| Passwords | bcrypt hashes (`BCrypt.Net-Next`) |

## Project structure

```
database/                 SQL scripts that create the database
  01_schema.sql             40 tables with keys, constraints and indexes
  02_procedures.sql         views, triggers and stored procedures (business rules)
  03_seed_data.sql          demo data
  99_reset_database.sql     removes everything (to start again)
docs/                     Project #1 proposal and Project #2 prototype
PeerMindAI/               the ASP.NET Core MVC application
  Controllers/              one controller per area (Account, Tutors, Bookings, Tutor, Admin, Moderation, ...)
  Models/                   entities and form models with validation attributes
  Data/                     database access: one repository class per area
  Services/                 log-in session check and private file storage
  Helpers/                  small view helpers (icons, badges, date formats)
  Views/                    Razor pages
  wwwroot/css, wwwroot/js   stylesheet and JavaScript
```

Responsibilities are separated as MVC requires: controllers handle requests, repositories talk to the database, views only display data. The business rules (double booking, cancellation window, minimum GPA, ...) live in the stored procedures, which raise a readable error message that the application shows to the user.

## Installation

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer
- MariaDB 10.4+ or MySQL 8 (for example the one included in [XAMPP](https://www.apachefriends.org/))

### 1. Create the database

With XAMPP: start **MySQL** (and **Apache** for phpMyAdmin), then open <http://localhost/phpmyadmin>.

1. Create a new database named `peermindai_db`.
2. Select it in the left sidebar and use **Import** to run these files **in this order**:
   1. `database/01_schema.sql`
   2. `database/02_procedures.sql`
   3. `database/03_seed_data.sql`

Or from a terminal:

```bash
mysql -u root -e "CREATE DATABASE peermindai_db CHARACTER SET utf8mb4"
mysql -u root peermindai_db < database/01_schema.sql
mysql -u root peermindai_db < database/02_procedures.sql
mysql -u root peermindai_db < database/03_seed_data.sql
```

Import the seed data only once. To start again from an empty database, import `database/99_reset_database.sql` and then the three files again.

### 2. Configure the connection

The connection string is in `PeerMindAI/appsettings.json`. The default matches a standard XAMPP installation (user `root`, empty password):

```json
"ConnectionStrings": {
  "PeerMind": "Server=localhost;Port=3306;Database=peermindai_db;User ID=root;Password=;"
}
```

Change the user, password or port there if your MySQL installation is different.

### 3. Run the application

```bash
cd PeerMindAI
dotnet run
```

Then open <http://localhost:5055>. In Visual Studio you can instead open `PeerMindAI/PeerMindAI.csproj` and press **F5**.

The NuGet packages (`MySqlConnector`, `BCrypt.Net-Next`) are downloaded automatically on the first build.

## Demo accounts

All demo accounts use the password `Demo@12345`. The log-in page has buttons that fill them in.

| Role | E-mail |
|---|---|
| Student | `aylin.kaya@university.edu` |
| Tutor | `elif.demir@university.edu` |
| Admin | `dana@peermind.ai` |

These are sample accounts from `03_seed_data.sql`; there are no real passwords or secrets in this repository.

### Suggested demonstration

1. **Student** – search for a tutor, open *Elif Demir*, book a free slot. Open *My bookings* and review the completed session with Mei Tanaka.
2. **Tutor** – open *Booking requests*, accept the new request, and mark yesterday's session as completed.
3. **Register** a new account with an e-mail such as `you@isikun.edu.tr` (a Gmail address is rejected), verify it, and submit a tutor application with any PDF.
4. **Admin** – approve the application under *Tutor verification*, then add, edit and delete a course in the *Course library*.
5. **Admin** – open *Moderation* and decide the open reports, for example suspend the author of the spam review and see the review disappear from the tutor's profile.

## Validation and error handling

- Every form is validated in the browser (JavaScript) **and** again on the server (model validation attributes). Both use the same messages.
- Rules that need the database are enforced by the stored procedures, for example "This time slot is no longer available", "A course with this code already exists" or "Only university e-mail addresses are accepted".
- Uploaded transcripts must be real PDF files of at most 5 MB. They are stored outside the public web folder and only administrators can download them.
- Lists show a message when they are empty. Unknown pages, forbidden pages and a stopped database show a friendly error page.
- All POST requests are protected with an anti-forgery token, and all SQL uses parameters.

## Notes

- **E-mail verification is simulated.** The project has no mail server, so the page that the verification link would open is shown directly after registration.
- **Payments and video calls** are out of scope, as stated in the proposal. Prices are shown for information only.
- The transcripts listed for the seeded applications are sample records without a file; transcripts uploaded through the application can be opened by the administrator.
- Data is stored in MariaDB/MySQL, so it is kept when the application or the database server is restarted.
