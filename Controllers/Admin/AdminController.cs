using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agromarket.Controllers.Admin
{
    [Authorize(Roles = "admin")] 
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();
            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            var userRoles = new Dictionary<string, string>();

            foreach (var user in users)
            {
                var role = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = role.FirstOrDefault() ?? "client";
            }

            ViewBag.Roles = roles;
            return View(users.Select(u => new { u.Id, u.UserName, u.Email, Role = userRoles[u.Id] }));
        }

        [HttpPost]
        public async Task<IActionResult> ChangeRole(string userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Користувача не знайдено.";
                return RedirectToAction("Index");
            }

            if (!await _roleManager.RoleExistsAsync(newRole))
            {
                TempData["Error"] = $"Роль '{newRole}' не існує.";
                return RedirectToAction("Index");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Не вдалося видалити поточні ролі.";
                return RedirectToAction("Index");
            }

            var addResult = await _userManager.AddToRoleAsync(user, newRole);
            if (!addResult.Succeeded)
            {
                TempData["Error"] = "Не вдалося призначити нову роль.";
                return RedirectToAction("Index");
            }

            TempData["Success"] = "Роль оновлено успішно.";
            return RedirectToAction("Index");
        }

    }
}