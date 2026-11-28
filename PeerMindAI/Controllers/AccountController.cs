using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;
using PeerMindAI.Services;

namespace PeerMindAI.Controllers;

/// <summary>Registration, e-mail verification, log-in / log-out and the profile page (FR-01, FR-02).</summary>
public class AccountController : BaseController
{
    private readonly UserRepository _users;
    private readonly DemoEmailSender _email;

    public AccountController(UserRepository users, DemoEmailSender email)
    {
        _users = users;
        _email = email;
    }

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

    // ---------- forgot / reset password ----------

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordForm());

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordForm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var token = Guid.NewGuid().ToString("N");
        var account = await _users.CreatePasswordResetAsync(form.Email.Trim(), token);
        if (account != null)
        {
            var link = Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme);
            await _email.SendAsync(account.Value.Email, "Reset your PeerMind AI password",
                $"Hi {Ui.FirstName(account.Value.FullName)}, open this link within 30 minutes to choose a new password: {link}");
        }

        // the same answer whether or not the address is registered (no user enumeration)
        ViewBag.Sent = true;
        return View(form);
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        var email = string.IsNullOrEmpty(token) ? null : await _users.CheckPasswordResetAsync(token);
        if (email == null)
        {
            return View("ResetPasswordInvalid");
        }
        return View(new ResetPasswordForm { Token = token!, Email = email });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _users.CompletePasswordResetAsync(form.Token, BCrypt.Net.BCrypt.HashPassword(form.NewPassword));
                Toast("Your password was changed. You can log in with the new password now.");
                return RedirectToAction(nameof(Login));
            }
            catch (DbRuleException)
            {
                // the link expired or was used while the form was open
                return View("ResetPasswordInvalid");
            }
        }

        form.Email = await _users.CheckPasswordResetAsync(form.Token) ?? "";
        return form.Email == "" ? View("ResetPasswordInvalid") : View(form);
    }

    // ---------- change password (logged in) ----------

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordForm form)
    {
        var account = await _users.GetByEmailAsync(User.FindFirstValue(ClaimTypes.Email) ?? "");
        if (account == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid && !PasswordMatches(form.CurrentPassword, account.PasswordHash))
        {
            ModelState.AddModelError("Password.CurrentPassword", "The current password is not correct.");
        }
        if (ModelState.IsValid && form.NewPassword == form.CurrentPassword)
        {
            ModelState.AddModelError("Password.NewPassword", "The new password must be different from the current one.");
        }

        if (ModelState.IsValid)
        {
            await _users.SetPasswordAsync(account.UserId, BCrypt.Net.BCrypt.HashPassword(form.NewPassword));
            Toast("Your password was changed.");
            return RedirectToAction(nameof(Profile));
        }

        // show the profile page again with the password errors (the typed passwords are not sent back)
        var user = await _users.GetByIdAsync(account.UserId);
        if (user == null)
        {
            return NotFound();
        }
        var preferences = await _users.GetPreferencesAsync(account.UserId);
        var page = await FillProfilePageAsync(NewProfileForm(user, preferences), user);
        page.Password = new ChangePasswordForm();
        Toast("Your password was not changed. Please check the highlighted fields.", isError: true);
        return View(nameof(Profile), page);
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
        return View(await FillProfilePageAsync(NewProfileForm(user, preferences), user));
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

    private static ProfileForm NewProfileForm(UserProfile user, UserPreferences preferences) => new()
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
