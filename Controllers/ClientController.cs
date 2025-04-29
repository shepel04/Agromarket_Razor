using Agromarket.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Agromarket.Models;
using System.Linq;

namespace Agromarket.Controllers.Admin
{
    [Authorize(Roles = "admin,clientmanager")]
    public class ClientController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ClientController(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<(IdentityUser User, string Role)>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();
                if (role == "client" || role == "entertaiment")
                {
                    result.Add((user, role));
                }
            }

            return View(result);
        }

        

    }
}