using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using UserManagementApp.Data;
using UserManagementApp.Models;
using UserManagementApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;

namespace UserManagementApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AppDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public AccountController(AppDbContext db, IEmailService emailService, IConfiguration config, IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _emailService = emailService;
            _config = config;
            _scopeFactory = scopeFactory;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            if (user.Status == UserStatus.Blocked)
            {
                ModelState.AddModelError("", "Your account has been blocked. Contact administrator.");
                return View(model);
            }

            if (!VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            user.LastLoginTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim("Status", user.Status.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "UserManagement");
        }

        [AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                PasswordHash = HashPassword(model.Password),
                Status = UserStatus.Unverified,
                RegistrationTime = DateTime.UtcNow,
                LastLoginTime = null,
                JobTitle = model.JobTitle,
                Company = model.Company
            };

            try
            {
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Users_Email_Unique") == true)
            { 
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(model);
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    if (string.IsNullOrEmpty(user.Email))
                        return;

                    string token = Guid.NewGuid().ToString();
                    var confirmationLink = Url.Action("VerifyEmail", "Account",
                        new { email = user.Email, token }, Request.Scheme)!;
                    string body = $"<p>Please confirm your email by clicking <a href='{confirmationLink}'>here</a>.</p>";

                    await emailService.SendEmailAsync(user.Email, "Confirm your email", body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Email sending failed: " + ex.Message);
                }
            });

            TempData["Message"] = "Registration successful! Please check your email to verify your account.";
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return BadRequest();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            if (user.Status == UserStatus.Unverified)
            {
                user.Status = UserStatus.Active;
                await _db.SaveChangesAsync();
                TempData["Message"] = "Email verified successfully. You can now log in.";
            }

            return RedirectToAction("Login");
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                await _emailService.SendEmailAsync("test@example.com", "Test", "Hello from Ethereal");
                return Content("Email sent successfully!");
            }
            catch (Exception ex)
            {
                return Content("Error: " + ex.ToString());
            }
        }
    }
}