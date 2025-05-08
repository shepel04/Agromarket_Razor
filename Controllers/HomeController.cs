using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Agromarket.Models;
using Agromarket.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Agromarket.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var now = DateTime.UtcNow;

            var discountedEntries = _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.HasDiscount &&
                            e.Quantity > 0 &&
                            (e.ExpirationDate == null || e.ExpirationDate > now))
                .ToList();

            var categories = _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.Quantity > 0 &&
                            (e.ExpirationDate == null || e.ExpirationDate > now))
                .Select(e => e.Product.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            ViewBag.Categories = categories;

            return View(discountedEntries);
        }



        

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}