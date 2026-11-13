using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Registration, e-mail verification, log-in / log-out and the profile page (FR-01, FR-02).</summary>
public class AccountController : BaseController
{
    private readonly UserRepository _users;

    public AccountController(UserRepository users) => _users = users;

    // ---------- log in ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        if (await _users.IsLockedOutAsync(form.Email))
        {
            ModelState.AddModelError("", "Too many failed log-in attempts. Please wait 15 minutes and try again.");
            return View(form);
        }

        var account = await _users.GetByEmailAsync(form.Email);
        var passwordIsCorrect = account != null && PasswordMatches(form.Password, account.PasswordHash);

        await _users.RecordLoginAttemptAsync(form.Email, passwordIsCorrect,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        if (account == null || !passwordIsCorrect)
        {
            ModelState.AddModelError("", "Incorrect e-mail or password.");
            return View(form);
        }
        if (account.Status == UserStatus.Suspended)
        {
            ModelState.AddModelError("", "This account is suspended. Please contact support@peermind.ai.");
            return View(form);
        }
        if (account.Status == UserStatus.PendingVerification)
        {
            var token = await _users.GetOpenVerificationTokenAsync(account.UserId)
                        ?? await _users.CreateVerificationTokenAsync(account.UserId);
            return RedirectToAction(nameof(VerifyEmail), new { token });
        }

        await SignInAsync(account.UserId, account.FullName, account.Email, account.Role, form.RememberMe);

        if (!string.IsNullOrEmpty(form.ReturnUrl) && Url.IsLocalUrl(form.ReturnUrl))
        {
            return Redirect(form.ReturnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => RedirectToAction("Status", "Home", new { code = 403 });

    // ---------- register ----------

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        return View(new RegisterForm { Universities = await _users.GetUniversitiesAsync() });
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(form.Password);
                var userId = await _users.CreateAsync(form.FullName.Trim(), form.Email.Trim(), passwordHash,
                    form.UniversityId, department: null, yearOfStudy: null);

                var token = await _users.CreateVerificationTokenAsync(userId);
                return RedirectToAction(nameof(VerifyEmail), new { token, tutor = form.WantsToTutor });
            }
            catch (DbRuleException ex)
            {
                // e.g. "Only university e-mail addresses are accepted."
                ModelState.AddModelError(nameof(form.Email), ex.Message);
            }
        }

        form.Universities = await _users.GetUniversitiesAsync();
        return View(form);
    }

    // ---------- e-mail verification ----------
    // There is no mail server in this project, so the page that would normally be
    // opened from the e-mail link is shown directly after registration.

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string? token, bool tutor = false)
    {
        var userId = string.IsNullOrEmpty(token) ? null : await _users.GetUserIdByVerificationTokenAsync(token);
        var user = userId == null ? null : await _users.GetByIdAsync(userId.Value);

        ViewBag.Token = token;
        ViewBag.WantsToTutor = tutor;
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmEmail(string? token, bool tutor = false)
    {
        var userId = string.IsNullOrEmpty(token) ? null : await _users.GetUserIdByVerificationTokenAsync(token);
        if (userId == null)
        {
            return RedirectToAction(nameof(VerifyEmail), new { token });
        }

        await _users.ConfirmEmailAsync(userId.Value, token!);
        var user = await _users.GetByIdAsync(userId.Value);
        if (user == null || user.Status != UserStatus.Active)
        {
            Toast("Your account could not be activated. Please contact support.", isError: true);
            return RedirectToAction(nameof(Login));
        }

        await SignInAsync(user.UserId, user.FullName, user.Email, user.Role, rememberMe: false);
        Toast("Your e-mail is verified. Welcome to PeerMind AI!");
        return tutor ? Redirect("/tutor/application") : RedirectToAction("Index", "Home");
    }

    // ---------- profile & settings ----------

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetByIdAsync(CurrentUserId);
        if (user == null)
        {
            return NotFound();
        }
        var preferences = await _users.GetPreferencesAsync(CurrentUserId);

        var form = new ProfileForm
        {
            FullName = user.FullName,
            Phone = user.Phone,
            UniversityId = user.UniversityId,
            Department = user.Department,
            YearOfStudy = user.YearOfStudy,
            StudentNumber = user.StudentNumber,
            EmailNotifications = preferences.EmailNotifications,
            StudyReminders = preferences.StudyReminders,
            MessageAlerts = preferences.MessageAlerts,
            WeeklyReport = preferences.WeeklyReport
        };
        return View(await FillProfilePageAsync(form, user));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Profile(ProfileForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _users.UpdateProfileAsync(CurrentUserId, form.FullName.Trim(), form.Phone?.Trim(), form.UniversityId,
                    form.Department?.Trim(), form.YearOfStudy, form.StudentNumber?.Trim(),
                    new UserPreferences
                    {
                        EmailNotifications = form.EmailNotifications,
                        StudyReminders = form.StudyReminders,
                        MessageAlerts = form.MessageAlerts,
                        WeeklyReport = form.WeeklyReport
                    });
                Toast("Your settings were saved.");
                return RedirectToAction(nameof(Profile));
            }
            catch (DbRuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        var user = await _users.GetByIdAsync(CurrentUserId);
        return user == null ? NotFound() : View(await FillProfilePageAsync(form, user));
    }

    // ---------- helpers ----------

    private async Task<ProfileForm> FillProfilePageAsync(ProfileForm form, UserProfile user)
    {
        form.Email = user.Email;
        form.Role = user.Role;
        form.Universities = await _users.GetUniversitiesAsync();
        return form;
    }

    private static bool PasswordMatches(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;   // the stored value is not a bcrypt hash
        }
    }

    private Task SignInAsync(int userId, string fullName, string email, string role, bool rememberMe) =>
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            UserClaims.Create(userId, fullName, email, role),
            new AuthenticationProperties { IsPersistent = rememberMe });
}
