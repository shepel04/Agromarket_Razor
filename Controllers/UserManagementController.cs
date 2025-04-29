using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agromarket.Controllers
{
    [Authorize(Roles = "admin,clientmanager")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserManagementController(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allUsers = _userManager.Users.ToList();
            var filteredUsers = new List<(IdentityUser, string)>();
            var userOrders = new Dictionary<string, List<Order>>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                if (role == "client" || role == "enterprice")
                {
                    filteredUsers.Add((user, role));

                    var orders = await _context.Orders
                        .Where(o => o.CustomerEmail == user.Email)
                        .OrderByDescending(o => o.OrderDate)
                        .ToListAsync();

                    userOrders[user.Id] = orders;
                }
            }

            ViewBag.UserOrders = userOrders;
            return View(filteredUsers);
        }
    }
}