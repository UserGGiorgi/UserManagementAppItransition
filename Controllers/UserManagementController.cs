using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserManagementApp.Data;
using UserManagementApp.Models;

namespace UserManagementApp.Controllers
{
    [Authorize]
    public class UserManagementController : Controller
    {
        private readonly AppDbContext _db;
        public UserManagementController(AppDbContext db) => _db = db;

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    var currentUser = await _db.Users.FindAsync(userId);
                    if (currentUser == null || currentUser.Status == UserStatus.Blocked)
                    {
                        await HttpContext.SignOutAsync();
                        context.Result = RedirectToAction("Login", "Account");
                        return;
                    }
                }
            }
            await base.OnActionExecutionAsync(context, next);
        }

        public async Task<IActionResult> Index()
        {
            var users = await _db.Users
                .OrderByDescending(u => u.LastLoginTime ?? DateTime.MinValue)
                .ToListAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> Block([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || !ids.Any())
                    return Json(new { success = false, message = "No users selected." });

                var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
                foreach (var u in users)
                {
                    u.Status = UserStatus.Blocked;
                }
                await _db.SaveChangesAsync();

                var currentId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                if (ids.Contains(currentId))
                    await HttpContext.SignOutAsync();

                return Json(new { success = true, message = $"{users.Count} user(s) blocked." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Json(new { success = false, message = $"Block failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Unblock([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || !ids.Any())
                    return Json(new { success = false, message = "No users selected." });

                var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
                foreach (var u in users)
                {
                    u.Status = UserStatus.Active;
                }
                await _db.SaveChangesAsync();
                return Json(new { success = true, message = $"{users.Count} user(s) unblocked." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Json(new { success = false, message = $"Unblock failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || !ids.Any())
                    return Json(new { success = false, message = "No users selected." });

                var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
                _db.Users.RemoveRange(users);
                await _db.SaveChangesAsync();

                var currentId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                if (ids.Contains(currentId))
                    await HttpContext.SignOutAsync();

                return Json(new { success = true, message = $"{users.Count} user(s) deleted." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Json(new { success = false, message = $"Delete failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUnverified()
        {
            try
            {
                var unverifiedUsers = await _db.Users.Where(u => u.Status == UserStatus.Unverified).ToListAsync();
                if (!unverifiedUsers.Any())
                    return Json(new { success = true, message = "No unverified users to delete." });

                _db.Users.RemoveRange(unverifiedUsers);
                await _db.SaveChangesAsync();
                return Json(new { success = true, message = $"{unverifiedUsers.Count} unverified user(s) deleted." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Json(new { success = false, message = $"Delete unverified failed: {ex.Message}" });
            }
        }
    }
}