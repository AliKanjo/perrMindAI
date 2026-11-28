# PeerMind AI – Code guide

This guide explains where each feature lives in the code and what happens, class by class, when a user does something. Use it to study your own part before the project quiz.

Every feature follows the same path, so learn this first:

```
Browser (Razor view + JavaScript)
   │  form POST or fetch() request
   ▼
Controller            Controllers/…Controller.cs     checks role, validates input, picks the response
   │
   ▼
Repository            Data/…Repository.cs            one C# method per database operation
   │
   ▼
Database helper       Data/Database.cs               opens the connection, sends parameters
   │
   ▼
Stored procedure      database/02_procedures.sql     business rules, transaction, raises errors
   │
   ▼
Tables                database/01_schema.sql         constraints are the last line of defence
```

When a rule is broken, the procedure raises an error with `SIGNAL SQLSTATE '45000'` and a message written for the user (for example "This time slot is no longer available"). `Database.cs` turns that error into a `DbRuleException`, and the controller shows its message on the page.

The owner of each section comes from [CONTRIBUTIONS.md](../CONTRIBUTIONS.md).

---

## Part 1 – Shared foundation (used by every feature)

**Owner: Kenan Aljabi** (the database scripts themselves are Ali Kanjo's, see Part 2)

| File | What it does |
|---|---|
| `Program.cs` | Starts the app. Loads the optional `appsettings.Local.json` (each computer's passwords and API key), registers MVC, the repositories and services (dependency injection), cookie log-in with roles and secure cookie flags, the error pages and the security headers, and checks the anti-forgery token on every POST. |
| `appsettings.json` | The database connection string and the upload settings. |
| `Data/Database.cs` | The only class that talks to MariaDB. `QueryProcedureAsync` / `QuerySqlAsync` return rows, `ExecuteProcedureAsync` / `ExecuteSqlAsync` change data, `ReadProcedureAsync` reads procedures that return several result sets. `In(...)` and `Out(...)` create parameters, so values are never pasted into SQL text (prevents SQL injection). |
| `Data/DbRuleException.cs` | The exception for a broken business rule (SQLSTATE 45000). Its message is safe to show to the user. |
| `Data/ReaderExtensions.cs` | `r.Text("column")`, `r.Int(...)`, `r.DateOrNull(...)`: read a column by name and handle SQL `NULL`. |
| `Controllers/BaseController.cs` | Parent of all controllers. `CurrentUserId` (logged-in user), `Toast(...)` (green or red message after the next page load), `JsonError(...)` (error answer for fetch requests). |
| `Helpers/UserClaims.cs` | Builds the identity stored in the log-in cookie (id, name, email, role) and reads it back with `User.GetUserId()` / `User.GetRole()`. |
| `Helpers/Ui.cs` | Small HTML helpers used by the views: icons, avatars, status badges, money and date formats. |
| `Views/Shared/_Layout.cshtml` | The sidebar, top bar and page frame of the logged-in area. The menu is chosen by role. |
| `Views/Shared/_PublicLayout.cshtml` | Frame of the public pages (landing, log-in, register, errors). |
| `Views/Shared/_FormErrors.cshtml` | Shows errors that are not tied to one field, such as a message from a stored procedure. |
| `wwwroot/css/site.css` | The styles, taken from the Project #2 prototype, plus the small-screen rules (slide-in menu, tables that scroll inside their card, stacked layouts on phones). |
| `wwwroot/js/site.js` | Shared JavaScript: toasts, opening and closing pop-ups, the side menu on small screens, the `api()` fetch helper (sends JSON and the anti-forgery token, turns errors into messages), and client-side form validation for every form marked `data-validate`. |

### How validation works on both sides

1. A form model in `Models/` has attributes such as `[Required]`, `[StringLength]`, `[Range]`.
2. ASP.NET writes those rules into the HTML as `data-val-*` attributes.
3. `site.js` reads them and stops the form in the browser with the same message.
4. When the form arrives, the controller checks `ModelState.IsValid` again on the server, because browser checks can be bypassed.
5. Rules that need the database (free slot, duplicate course code, university e-mail) are checked in the stored procedure.

---

## Part 2 – Database

**Owner: Ali Kanjo**

| File | What it contains |
|---|---|
| `database/01_schema.sql` | 40 tables with primary keys, foreign keys, `CHECK` constraints and indexes. |
| `database/02_procedures.sql` | 3 views, 3 triggers, the `fn_setting` function and the stored procedures that hold the business rules. |
| `database/03_seed_data.sql` | Demo universities, users, courses, tutors, slots, bookings, reviews, applications and reports. |
| `database/99_reset_database.sql` | Drops everything so the database can be rebuilt. |

Things to be able to explain:

- **No double booking (NFR-05).** `bookings.active_slot_id` is a generated column: it holds `slot_id` while the booking is `Pending` or `Confirmed`, otherwise `NULL`. Its `UNIQUE` key (`uq_bookings_active_slot`) means one slot can have only one active booking. `sp_booking_create` also locks the slot row with `SELECT … FOR UPDATE` inside a transaction. If two students click at the same moment, the second insert hits the unique key, and the procedure's handler for error 1062 turns it into "This time slot is no longer available".
- **Tutor rating.** The triggers `trg_reviews_after_insert/update/delete` call `sp_recalculate_tutor_rating`, which stores the average of the visible reviews in `tutor_profiles.average_rating`.
- **Settings.** `fn_setting('BookingCancellationWindowHours')` reads a value from `platform_settings`, so rules such as the 24-hour cancellation window or the minimum GPA can change without changing code.
- **Views.** `vw_approved_tutors` (tutors students may see), `vw_booking_details` (a booking with slot, course, student and tutor joined), `vw_admin_dashboard` (the admin numbers).

---

## Part 3 – Workflow 1: find and book a tutor

**Owner: Ali Kanjo**

### 3.1 Searching tutors without reloading the page (asynchronous, Fetch + JSON)

| Step | Where |
|---|---|
| The student opens *Find a tutor*. | `TutorsController.Index` → `Views/Tutors/Index.cshtml` (only the filters; the list is empty at first) |
| JavaScript sends `GET /tutors/search?q=…&courseId=…` every time a filter changes (after 300 ms of no typing). | `wwwroot/js/tutor-search.js`, function `search()` |
| The controller validates the filters and calls the repository. | `TutorsController.Search` |
| The repository calls `sp_tutor_search`. | `TutorRepository.SearchAsync` |
| The procedure returns approved tutors that match, with their courses and next free slot. | `sp_tutor_search` (uses `vw_approved_tutors`) |
| The controller returns `{ ok: true, tutors: [...] }` as JSON. | `TutorsController.Search` → `Json(...)` |
| JavaScript builds the cards, or shows "No tutors match your search", or an error message. | `tutor-search.js`, functions `tutorCard()` and `messageCard()` |

`latestRequest` in `tutor-search.js` ignores answers to older searches, so fast typing never shows outdated results.

### 3.2 Viewing a tutor profile

| Step | Where |
|---|---|
| The student clicks *View profile* (`GET /tutors/{id}`). | `TutorsController.Details` |
| The repository reads four result sets from one procedure: profile, courses, free slots, reviews. | `TutorRepository.GetProfileAsync` → `sp_tutor_get_profile` |
| The page shows the profile and the booking form. Day chips switch between slots, and the total price follows the chosen slot. | `Views/Tutors/Details.cshtml`, `wwwroot/js/booking-form.js` |

### 3.3 Booking a session

| Step | Where |
|---|---|
| The student picks a slot and a course and clicks *Request booking*. `site.js` first checks that a slot and a course are chosen. | `Views/Tutors/Details.cshtml`, `site.js` |
| The form is posted to `POST /bookings`. The `[Authorize(Roles = Student)]` attribute rejects other roles. | `BookingsController.Create(BookingForm form)` |
| The controller checks `ModelState.IsValid` (rules in `Models/BookingModels.cs` → `BookingForm`). | `BookingsController.Create` |
| The repository calls the procedure with the student id, slot, course, topic and notes, and gets the new booking id from an OUT parameter. | `BookingRepository.CreateAsync` → `sp_booking_create` |
| The procedure, in one transaction: locks the slot, checks it exists, is open, is in the future, is not the student's own, that the tutor teaches the course, that the student has no other session at that time; inserts the booking as `Pending`; writes the first status-history row; sends the tutor a notification. | `sp_booking_create` |
| **Success:** the controller sets a toast and redirects to the booking details page. | `BookingsController.Create` → `RedirectToAction(nameof(Details))` |
| **Failure** (e.g. the slot was just taken): the procedure raises the message, `Database.cs` throws `DbRuleException`, the controller adds it to `ModelState` and shows the profile again with the error and the slots that are still free. | `catch (DbRuleException ex)` in `BookingsController.Create` |

### 3.4 My bookings and booking details

| Action | Controller method | Repository method | Procedure / SQL |
|---|---|---|---|
| List my bookings (tabs Upcoming / Completed / Cancelled) | `BookingsController.Index` | `ListForStudentAsync` | `vw_booking_details` |
| Open one booking | `BookingsController.Details` | `GetAsync` | `sp_booking_get_by_id` (returns nothing unless you are its student, its tutor or an admin, so others get 404) |
| Edit topic and notes | `BookingsController.UpdateNotes` | `UpdateNotesAsync` | `sp_booking_update` |
| Cancel (student or tutor, reason required) | `BookingsController.Cancel` | `CancelAsync` | `sp_booking_cancel` (checks the 24-hour window; cancelling sets the status to `Cancelled`, so `active_slot_id` becomes `NULL` and the slot is free again) |
| Review a completed session (1–5 stars) | `BookingsController.Review` | `CreateReviewAsync` | `sp_review_create` (only once, only after `Completed`; the trigger updates the rating) |

`RunAndReturnAsync` in `BookingsController` runs one of these changes and returns to the details page with a success or error toast. The pop-up for cancelling is in `Views/Bookings/Details.cshtml` (`@section Modals`).

---

## Part 4 – Tutor side of the booking workflow

**Owner: Ali Kanjo** (it completes Workflow 1)

| Action | Where |
|---|---|
| Tutor dashboard (numbers, new requests, next sessions) | `TutorController.Index` → `TutorRepository.GetStatsAsync`, `BookingRepository.ListForTutorAsync` → `Views/Tutor/Index.cshtml` |
| Booking requests page | `TutorController.Requests` → `Views/Tutor/Requests.cshtml` |
| Accept / Decline (fetch + JSON, no reload) | `tutor-requests.js` sends `POST /bookings/{id}/respond` with `{ accept: true/false }` → `BookingsController.Respond` → `BookingRepository.RespondAsync` → `sp_booking_respond` (status `Confirmed` or `Declined`, notifies the student) → JSON `{ ok, status, message }` → the card's badge changes in place |
| Mark a finished session completed (fetch + JSON) | `POST /bookings/{id}/complete` → `BookingsController.Complete` → `CompleteAsync` → `sp_booking_complete` (only `Confirmed` sessions that have ended) |
| Reviews page and replying | `TutorController.Reviews`, `TutorController.Reply` → `TutorRepository.ReplyToReviewAsync` → `sp_review_reply` → `Views/Tutor/Reviews.cshtml` |

---

## Part 5 – Accounts: registration, log-in, profile

**Owner: Ammar Azzan**

| Action | Where |
|---|---|
| Register | `AccountController.Register` (POST) hashes the password with `BCrypt.HashPassword`, calls `UserRepository.CreateAsync` → `sp_user_create` (rejects non-university domains and duplicate e-mails), creates a verification token, redirects to the verification page |
| Verify e-mail (simulated, no mail server) | `AccountController.VerifyEmail` shows the page; `ConfirmEmail` marks the token used and activates the account (`UserRepository.ConfirmEmailAsync`), then logs the user in |
| Log in | `AccountController.Login` (POST): `IsLockedOutAsync` (5 failures in 15 min), `GetByEmailAsync` → `sp_user_get_by_email`, `PasswordMatches` (`BCrypt.Verify`), `RecordLoginAttemptAsync` → `sp_login_attempt_record`, then `SignInAsync` writes the cookie |
| Log out | `AccountController.Logout` |
| Role checks on every request | `[Authorize(Roles = …)]` on each controller; `Services/SessionUserValidator.cs` re-reads the user on every request, so a suspended user is signed out at once and a newly approved tutor gets the Tutor role without logging in again. For fetch requests it answers 401/403 instead of redirecting. |
| Profile and settings | `AccountController.Profile` (GET/POST) → `UserRepository.UpdateProfileAsync` → `sp_user_update` → `Views/Account/Profile.cshtml` |

Form models with their validation rules: `Models/AccountModels.cs` (`LoginForm`, `RegisterForm`, `ProfileForm`).

---

## Part 6 – Workflow 2: becoming a tutor

### 6.1 The application (applicant side)

**Owner: Ammar Azzan**

| Step | Where |
|---|---|
| Open the form (pre-filled from a previous draft or rejected application) | `TutorApplicationController.Apply` (GET) → `Views/TutorApplication/Apply.cshtml` |
| Submit | `TutorApplicationController.Apply` (POST) |
| Extra server checks the attributes cannot express: at least one course, GPA ≥ the `TutorMinimumGPA` setting, year ≥ 2, transcript present | same method |
| Check the PDF: extension `.pdf`, ≤ 5 MB, first bytes are `%PDF` | `Services/TranscriptStorage.ValidateAsync` |
| Save the application, courses and file | `ApplicationRepository.CreateDraftAsync` (`sp_application_create`) or `UpdateDraftAsync`, `SetCoursesAsync` (`sp_application_add_course`), `TranscriptStorage.SaveAsync` (random file name in `App_Data/uploads`, outside `wwwroot`), `AddDocumentAsync` (`sp_application_add_document`) |
| Submit for review | `ApplicationRepository.SubmitAsync` → `sp_application_submit` (checks the rules again, status `Pending`, notifies admins) |
| Status page | `TutorApplicationController.Status` → `Views/TutorApplication/Status.cshtml` |

### 6.2 The admin decision

**Owner: Ammar Azzan** (it completes Workflow 2)

| Action | Where |
|---|---|
| List pending applications and show the selected one | `VerificationController.Index` → `ApplicationRepository.ListPendingAsync`, `GetAsync` → `Views/Verification/Index.cshtml` |
| Download the transcript (admins only, file is outside the public folder) | `VerificationController.Document` → `TranscriptStorage.FindFile` → `PhysicalFile(...)` |
| Approve | `VerificationController.Approve` → `ApproveAsync` → `sp_application_approve` (creates the tutor profile and tutor courses, sets the role to Tutor, notifies the applicant) |
| Reject with a reason / request more information | `VerificationController.Reject`, `RequestInfo` → `sp_application_reject`, `sp_application_request_info` |

---

## Part 7 – Admin: dashboard, users, moderation

**Owner: Ammar Azzan**

| Action | Where |
|---|---|
| Dashboard numbers | `AdminController.Index` → `ApplicationRepository.GetDashboardAsync` → `vw_admin_dashboard` → `Views/Admin/Index.cshtml` |
| User list with search and filters | `AdminController.Users` → `UserRepository.ListAsync` → `sp_user_list` |
| User details, suspend / re-activate | `AdminController.UserDetails`, `SetStatus` → `sp_user_set_status` |
| Delete a user with no activity | `AdminController.DeleteUser` → `sp_user_delete` (refuses users with bookings, applications, etc.) |
| File a report (from a booking or a review) | `BookingsController.Report`, `TutorController.ReportReview` → `ReportRepository.CreateAsync` → `sp_report_create`; pop-up in `Views/Shared/_ReportModal.cshtml` |
| Moderation page and decisions | `ModerationController.Index`, `Decide` → `ReportRepository.DecideAsync` → `sp_report_decide` (suspends the user, sends a warning notification, or hides the reported review) → `Views/Moderation/Index.cshtml` |

---

## Part 8 – Admin: course library (the CRUD entity)

**Owner: Kenan Aljabi**

| Operation | Controller method | Repository method | Procedure |
|---|---|---|---|
| **Read** – list and search | `CoursesController.Index` | `CourseRepository.ListAsync` | `sp_course_list` |
| **Create** | `CoursesController.Create` (GET shows the form, POST saves) | `CreateAsync` | `sp_course_create` (rejects duplicate codes) |
| **Update** | `CoursesController.Edit` (GET/POST) | `GetAsync`, `UpdateAsync` | `sp_course_get_by_id`, `sp_course_update` |
| **Delete** | `CoursesController.Delete` | `DeleteAsync` | `sp_course_delete` (a course in use is only deactivated; the procedure returns a message saying which happened) |

One view serves both create and edit: `Views/Courses/Form.cshtml`. Validation rules: `Models/CourseModels.cs` → `CourseForm`.

---

## Part 9 – Tutor area: availability and public profile

**Owner: Kenan Aljabi**

| Action | Where |
|---|---|
| List my future slots | `TutorController.Availability` → `TutorRepository.GetUpcomingSlotsAsync` → `Views/Tutor/Availability.cshtml` |
| Add a slot | `TutorController.AddSlot` → `AddSlotAsync` → `sp_slot_add` (end after start, in the future, no overlap with my other slots) |
| Remove a slot | `TutorController.RemoveSlot` → `RemoveSlotAsync` → `sp_slot_remove` (refused while a booking is active; a slot with past bookings is closed instead of deleted) |
| Edit the public profile | `TutorController.Profile` (GET/POST) → `GetProfileFormAsync`, `UpdateProfileAsync` → `sp_tutor_update_profile` → `Views/Tutor/Profile.cshtml` |

---

## Part 10 – Notifications and error pages

**Owner: Kenan Aljabi**

| Action | Where |
|---|---|
| Notification list | `NotificationsController.Index` → `UserRepository.GetNotificationsAsync` → `Views/Notifications/Index.cshtml` |
| Mark all as read (fetch + JSON) | inline script in `Views/Notifications/Index.cshtml` → `POST /notifications/read-all` → `NotificationsController.MarkAllRead` → `sp_notification_mark_all_read` |
| Bell with red dot in the top bar | `ViewComponents/NotificationBellViewComponent.cs` → `Views/Shared/Components/NotificationBell/Default.cshtml` |
| Where notifications come from | the stored procedures insert them (booking requested, confirmed, cancelled, application decided, warning, …) |
| Unhandled errors (e.g. database stopped) | `Program.cs` → `UseExceptionHandler("/Home/Error")` → `HomeController.Error` picks the message → `Views/Shared/Error.cshtml` |
| 404, 403, 400 pages | `UseStatusCodePagesWithReExecute` → `HomeController.Status` |

---

## Part 11 – Other pages

**Owner: Kenan Aljabi**

| Page | Where |
|---|---|
| Landing page (top tutors from the database) | `HomeController.Index` → `Views/Home/Index.cshtml` |
| Student dashboard | `StudentController.Index` → `Views/Student/Index.cshtml` |

---

## Part 12 – AI study assistant (Project #4)

**Owner: Not assigned yet**

### 12.1 Uploading material

| Step | Where |
|---|---|
| The student opens *My materials* | `MaterialsController.Index` → `MaterialRepository.ListForStudentAsync` (only the student's own files) → `Views/Materials/Index.cshtml` |
| Upload a PDF: check type, size and the `%PDF` header | `MaterialsController.Upload` → `Services/MaterialStorage.ValidateAsync` (`PdfSignature` checks the header) |
| Save the file privately and register it | `MaterialStorage.SaveAsync` (random name in `App_Data/uploads`, outside `wwwroot`) → `MaterialRepository.CreateAsync` → `sp_material_create` (status Processing) |
| Read the text page by page | `Services/PdfTextExtractor.Extract` (PdfPig library), split into pieces of about 1,200 characters with their page number |
| Store the pieces | `MaterialRepository.AddChunkAsync` → `document_chunks`; then `SetProcessedAsync` → `sp_material_set_processed` (Processed or Failed) |
| Switch a material on or off as an AI source (Fetch + JSON) | inline script in `Views/Materials/Index.cshtml` → `POST /materials/{id}/source` → `MaterialsController.SetSource` |
| Delete | `MaterialsController.Delete` → `sp_material_delete` (refused while a quiz or lesson uses it) → `MaterialStorage.Delete` |

### 12.2 Asking a question

| Step | Where |
|---|---|
| Chat page: list of chats and the selected chat | `AssistantController.Index` → `AssistantRepository.ListConversationsAsync`, `GetMessagesAsync` (`sp_ai_conversation_get`) → `Views/Assistant/Index.cshtml` |
| New chat (all materials or one course) | `AssistantController.Create` → `sp_ai_conversation_create` |
| The browser sends the question as JSON | `wwwroot/js/assistant.js` → `POST /assistant/{id}/ask` |
| The server checks the question (not empty, ≤ 500 characters), that the chat is the student's, and that AI is allowed | `AssistantController.Ask`, `AssistantRepository.IsAiAllowedAsync` |
| Limit: 30 questions per hour | `StudyAssistant.AskAsync` → `AssistantRepository.CountRecentQuestionsAsync` |
| Find the best pages: keyword scoring with a small stemmer | `Services/SourceFinder.FindRelevant` over `MaterialRepository.GetSearchableChunksAsync` |
| No matching page → honest "not in your materials" answer without calling the AI | `StudyAssistant.AskAsync` (`NotFoundAnswer`) |
| Otherwise send only those pages to the AI with strict instructions | `StudyAssistant.CallAiAsync` → `Services/AiClient.AskAsync` (Anthropic: POST `/v1/messages` with the `x-api-key` and `anthropic-version` headers; Groq: POST `/openai/v1/chat/completions` with a Bearer token) |
| The AI says the pages are not enough (`NOT_IN_MATERIALS`) → honest answer | `StudyAssistant.AskAsync` |
| Turn `[1]` citations into `[file, p.N]` and keep the cited pages | `StudyAssistant.ReplaceCitations` |
| Save question, answer and sources | `AssistantRepository.AddMessageAsync` (`sp_ai_message_add`, which also checks the user's AI access) and `AddSourceAsync` (`sp_ai_message_add_source`, only the student's own pages) |
| Log every AI call, also failed ones | `AssistantRepository.LogUsageAsync` → `ai_usage_logs` |
| AI errors and time-outs | `AiClient` throws `AiServiceException` with a friendly message; `AssistantController.Ask` returns it as JSON (503), the details go only to the server log |

The provider (`AI:Provider`, Anthropic or Groq) and the API key (`AI:ApiKey`) are read from the configuration, normally from `appsettings.Local.json`. `AiClient` builds the request in the format of the chosen provider and reads its answer, so the rest of the code does not depend on the provider. Without a key, `AiClient.IsConfigured` is false and the page shows a notice.

---

### 12.3 Practice quizzes and study recommendations (FR-10, FR-11)

| Step | Where |
|---|---|
| Quiz list and the "Generate a quiz" form | `QuizzesController.Index` → `QuizRepository.ListForStudentAsync` → `Views/Quizzes/Index.cshtml` |
| Generate: limit of 10 quizzes per hour, the material must be the student's and processed | `QuizzesController.Generate` → `Services/QuizGenerator.GenerateAsync` |
| Send the material's text (spread over its pages) and the course topics to the AI, asking for JSON | `QuizGenerator.AskAiAsync`, `SelectExcerpts` → `AiClient.AskAsync(..., jsonOnly: true)` |
| Check every question: four different options, one correct answer, a page that exists; skip bad ones | `QuizGenerator.Parse`, `ReadQuestion` |
| Store the quiz, its questions and options (removed again if storing fails half-way) | `QuizRepository.CreateAsync`, `AddQuestionAsync`, `AddOptionAsync` → `sp_quiz_create`, `sp_quiz_add_question`, `sp_quiz_add_option` |
| Start an attempt (also "Retry") | `QuizzesController.Start` / after generating → `QuizRepository.StartAttemptAsync` → `sp_quiz_start` |
| Show the questions without the correct answers | `QuizzesController.Take` → `QuizRepository.GetOpenAttemptAsync` → `Views/Quizzes/Take.cshtml` (progress bar script inside) |
| Submit: every question must be answered (browser and server) | `QuizzesController.Submit` (answers arrive as `answers[questionId] = optionId`) → `SaveAnswerAsync` (`sp_quiz_answer`) → `SubmitAsync` (`sp_quiz_submit`) |
| Scoring and recommendations | `sp_quiz_submit`: score, pass/fail, one "review" recommendation per weak topic (or per material with the missed pages), "retry" when failed, "find a tutor" when a tutor teaches the course |
| Results page | `QuizzesController.Result` → `QuizRepository.GetResultAsync` (`sp_quiz_get_result`) → `Views/Quizzes/Result.cshtml` |
| Recommendations on the dashboard, dismiss with Fetch | `StudentController.Index` → `QuizRepository.GetOpenRecommendationsAsync`; `wwwroot/js/recommendations.js` → `POST /recommendations/{id}/dismiss` → `QuizzesController.Dismiss` |
| Delete a quiz | `QuizzesController.Delete` → `sp_quiz_delete` |

---

## Part 13 – Tutors manage their courses (FR-07)

**Owner: Not assigned yet**

| Action | Where |
|---|---|
| List the courses a tutor teaches | `TutorController.Profile` → `TutorRepository.GetCoursesAsync` → "Courses you teach" card in `Views/Tutor/Profile.cshtml` |
| Remove a course | `TutorController.RemoveCourse` → `TutorRepository.RemoveCourseAsync` → `sp_tutor_remove_course` (refused for the last course or while sessions for it are pending or confirmed) |
| Apply to teach more courses | `TutorApplicationController.Apply`: an approved tutor may apply again (`IsLocked`); courses already taught are not offered (`GetTaughtCourseIdsAsync`); the admin approves as usual and `sp_application_approve` adds the new courses |

---

## Part 14 – AI usage monitoring and AI access (FR-13)

**Owner: Not assigned yet**

| Action | Where |
|---|---|
| AI usage page | `AdminController.AiUsage` → `Data/AiUsageRepository.GetPageAsync` (totals for today, 7 and 30 days, per day, per feature, top users, recent failures from `ai_usage_logs`) → `Views/Admin/AiUsage.cshtml` |
| Turn the AI features off or on for one user | `Views/Admin/UserDetails.cshtml` → `AdminController.SetAiAccess` → `UserRepository.SetAiAccessAsync` → `sp_user_set_status` (`users.ai_access_enabled`); `AssistantRepository.IsAiAllowedAsync` checks it before every AI request, and `sp_ai_message_add` checks it again |

---

## Part 15 – Change and reset password (Project #4)

**Owner: Not assigned yet**

| Action | Where |
|---|---|
| Change password on the profile page | `Views/Account/Profile.cshtml` (separate form) → `AccountController.ChangePassword`: checks the current password with `BCrypt.Verify`, the rules in `ChangePasswordForm` (`PasswordRules` in `Models/AccountModels.cs`), then `UserRepository.SetPasswordAsync` → `sp_user_set_password` |
| Ask for a reset link | `AccountController.ForgotPassword` → `UserRepository.CreatePasswordResetAsync` → `sp_password_reset_request` (returns nothing for unknown e-mails; older links stop working) → `Services/DemoEmailSender` writes the e-mail to the terminal. The page is the same for every e-mail. |
| Open the link | `AccountController.ResetPassword` (GET) → `sp_password_reset_check` → `Views/Account/ResetPassword.cshtml`, or `ResetPasswordInvalid.cshtml` for a used, expired or made-up link |
| Save the new password | `AccountController.ResetPassword` (POST) → `sp_password_reset_complete` (one use only, 30 minutes, clears failed log-ins, sends a notification) |

---

## Part 16 – Automated tests (Project #4)

**Owner: Not assigned yet**

| File | What it does |
|---|---|
| `tests/browser/workflows.test.js` | Project #3 workflows (47 checks) |
| `tests/browser/moderation.test.js` | reports and moderation (20 checks) |
| `tests/browser/project4.test.js` | passwords, materials, AI assistant, security, phone and tablet (48 checks) |
| `tests/browser/final.test.js` | quizzes, recommendations, tutor courses, AI usage, AI access, Groq format (36 checks) |
| `tests/browser/performance.test.js` | response times for NFR-03 |
| `tests/browser/fake-claude.js` | a local stand-in for the Anthropic and Groq APIs used by the tests |
| `docs/TEST_REPORT.md` / `.pdf` | the test report |

