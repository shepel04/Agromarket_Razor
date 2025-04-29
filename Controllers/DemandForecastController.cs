using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Agromarket.Controllers
{
    [Authorize(Roles = "admin,warehousemanager")]
    public class DemandForecastController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DemandForecastController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string category = null, int? productId = null)
        {
            var products = _context.Products.ToList();
            var orderItems = _context.OrderItems
                .Include(o => o.Order)
                .Where(o => o.Order.Status == OrderStatus.Виконано)
                .ToList();

            var productMap = products.ToDictionary(p => p.Id);

            var result = new List<DemandForecastRow>();

            foreach (var product in products)
            {
                if (!product.IsInSeason())
                    continue;

                if (!string.IsNullOrEmpty(category) && product.Category != category)
                    continue;

                if (productId.HasValue && product.Id != productId.Value)
                    continue;

                var currentMonth = DateTime.UtcNow.Month;

                int startMonth = GetMonthNumber(product.SeasonStartMonth);
                int endMonth = GetMonthNumber(product.SeasonEndMonth);

                // Підрахунок кількості місяців у сезоні
                int totalSeasonMonths = startMonth <= endMonth
                    ? endMonth - startMonth + 1
                    : 12 - startMonth + endMonth + 1;

                int remainingSeasonMonths = endMonth >= currentMonth
                    ? endMonth - currentMonth + 1
                    : 12 - currentMonth + endMonth + 1;

                var productOrders = orderItems
                    .Where(o => o.ProductId == product.Id)
                    .GroupBy(o => new { o.Order.OrderDate.Year, o.Order.OrderDate.Month })
                    .Select(g => new
                    {
                        Month = g.Key.Month,
                        Year = g.Key.Year,
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .ToList();

                int totalSold = productOrders.Sum(o => o.Quantity);
                double avgPerMonth = totalSeasonMonths > 0 ? (double)totalSold / totalSeasonMonths : 0;
                int forecast = (int)Math.Ceiling(avgPerMonth * remainingSeasonMonths);

                result.Add(new DemandForecastRow
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Category = product.Category,
                    Unit = product.Unit,
                    TotalSoldThisSeason = totalSold,
                    AvgPerMonth = Math.Round(avgPerMonth, 2),
                    ForecastedOrderQuantity = forecast,
                    RemainingSeasonMonths = remainingSeasonMonths
                });
            }

            ViewBag.Categories = _context.Products
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.Products = products.OrderBy(p => p.Name).ToList();

            return View(result);
        }

        private int GetMonthNumber(string monthName)
        {
            var months = new[]
            {
                "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень"
            };

            return Array.IndexOf(months, monthName) + 1;
        }
    }

    public class DemandForecastRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public int TotalSoldThisSeason { get; set; }
        public double AvgPerMonth { get; set; }
        public int RemainingSeasonMonths { get; set; }
        public int ForecastedOrderQuantity { get; set; }
    }
}

