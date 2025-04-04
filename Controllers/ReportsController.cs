using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.IO;
using OfficeOpenXml;

namespace Agromarket.Controllers
{
    [Authorize(Roles = "admin,warehousemanager,clientmanager")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Загальна сторінка всіх звітів
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
        
        public IActionResult SalesReport(DateTime? startDate, DateTime? endDate, string action)
        {
            var orderItems = _context.OrderItems
                .Include(o => o.Order)
                .Where(o => o.Order.Status == OrderStatus.Виконано)
                .AsQueryable();

            if (startDate.HasValue)
                orderItems = orderItems.Where(o => o.Order.OrderDate >= DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc));

            if (endDate.HasValue)
                orderItems = orderItems.Where(o => o.Order.OrderDate <= DateTime.SpecifyKind(endDate.Value.AddDays(1).AddSeconds(-1), DateTimeKind.Utc));

            var result = orderItems
                .GroupBy(o => new { o.ProductName, o.Unit })
                .Select(g => new SalesReportRow
                {
                    ProductName = g.Key.ProductName,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * x.Price)
                })
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            if (action == "export")
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Звіт продажів");

                worksheet.Cell(1, 1).Value = "Товар";
                worksheet.Cell(1, 2).Value = "Кількість";
                worksheet.Cell(1, 3).Value = "Одиниця";
                worksheet.Cell(1, 4).Value = "Сума продажу";

                for (int i = 0; i < result.Count; i++)
                {
                    worksheet.Cell(i + 2, 1).Value = result[i].ProductName;
                    worksheet.Cell(i + 2, 2).Value = result[i].TotalQuantity;
                    worksheet.Cell(i + 2, 3).Value = result[i].Unit;
                    worksheet.Cell(i + 2, 4).Value = result[i].TotalRevenue;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"SalesReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            fileName);
            }

            // Якщо не експорт — просто показати звіт
            return View(result);
        }
    }
    

    public class SalesReportRow
    {
        public int ProductId { get; set; }  
        public string ProductName { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public string Unit { get; set; }
    }

    public class StockReportRow
    {
        public string ProductName { get; set; }
        public int TotalQuantity { get; set; }
        public string Unit { get; set; }
    }
}
