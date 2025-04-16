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
        
        [HttpGet]
        public IActionResult SalesReport(DateTime? startDate, DateTime? endDate, string action)
        {
            var orderItemsQuery = _context.OrderItems
                .Include(o => o.Order)
                .Where(o => o.Order.Status == OrderStatus.Виконано);

            if (startDate.HasValue)
                orderItemsQuery = orderItemsQuery.Where(o => o.Order.OrderDate >= DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc));

            if (endDate.HasValue)
                orderItemsQuery = orderItemsQuery.Where(o => o.Order.OrderDate <= DateTime.SpecifyKind(endDate.Value.AddDays(1).AddSeconds(-1), DateTimeKind.Utc));

            var orderItems = orderItemsQuery.ToList(); // ⬅️ Спочатку виконуємо запит

            var productCosts = _context.WarehouseEntries
                .GroupBy(e => e.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AvgCost = g.Average(e => e.PurchasePrice)
                })
                .ToDictionary(e => e.ProductId, e => e.AvgCost);

            var result = orderItems
                .GroupBy(o => new { o.ProductId, o.ProductName, o.Unit })
                .Select(g =>
                {
                    decimal avgCost = productCosts.ContainsKey(g.Key.ProductId) ? productCosts[g.Key.ProductId] : 0;

                    return new SalesReportRow
                    {
                        ProductName = g.Key.ProductName,
                        Unit = g.Key.Unit,
                        TotalQuantity = g.Sum(x => x.Quantity),
                        TotalRevenue = g.Sum(x => x.Quantity * x.Price),
                        TotalCost = g.Sum(x => x.Quantity) * avgCost
                    };
                })
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            if (action == "export")
            {
                
            }

            return View(result);
        }



        [HttpGet]
        public IActionResult StockReport()
        {
            var entries = _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.Quantity > 0)
                .ToList();

            var summary = new StockSummary
            {
                TotalProductTypes = entries.Select(e => e.ProductId).Distinct().Count(),
                TotalStockValue = entries.Sum(e => e.PurchasePrice * e.Quantity),
                CategoryDetails = entries
                    .GroupBy(e => e.Product.Category)
                    .Select(g => new StockReportRow
                    {
                        Category = g.Key,
                        ProductCount = g.Select(e => e.ProductId).Distinct().Count(),
                        TotalQuantity = g.Sum(e => e.Quantity),
                        TotalValue = g.Sum(e => e.PurchasePrice * e.Quantity)
                    })
                    .OrderByDescending(r => r.TotalValue)
                    .ToList()
            };

            return View(summary);
        }
        
        [HttpGet]
        public IActionResult DiscountSalesReport(DateTime? startDate, DateTime? endDate)
        {
            var orderItemsQuery = _context.OrderItems
                .Include(o => o.Order)
                .Where(o => o.Order.Status == OrderStatus.Виконано);

            if (startDate.HasValue)
                orderItemsQuery = orderItemsQuery.Where(o => o.Order.OrderDate >= DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc));

            if (endDate.HasValue)
                orderItemsQuery = orderItemsQuery.Where(o => o.Order.OrderDate <= DateTime.SpecifyKind(endDate.Value.AddDays(1).AddSeconds(-1), DateTimeKind.Utc));

            var orderItems = orderItemsQuery.ToList();

            var productCosts = _context.WarehouseEntries
                .GroupBy(e => e.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AvgCost = g.Average(e => e.PurchasePrice)
                })
                .ToDictionary(e => e.ProductId, e => e.AvgCost);

            var productSelling = _context.WarehouseEntries
                .GroupBy(e => e.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AvgSellingPrice = g.Average(e => e.SellingPrice)
                })
                .ToDictionary(e => e.ProductId, e => e.AvgSellingPrice ?? 0);

            var discounted = orderItems
                .Where(o =>
                {
                    var originalPrice = productSelling.ContainsKey(o.ProductId) ? productSelling[o.ProductId] : 0;
                    return o.Price < originalPrice;
                })
                .GroupBy(o => new { o.ProductId, o.ProductName, o.Unit })
                .Select(g =>
                {
                    var pid = g.Key.ProductId;
                    decimal avgCost = productCosts.ContainsKey(pid) ? productCosts[pid] : 0;
                    decimal sellingPrice = productSelling.ContainsKey(pid) ? productSelling[pid] : 0;

                    var totalQty = g.Sum(x => x.Quantity);
                    var revenue = g.Sum(x => x.Price * x.Quantity);
                    var cost = avgCost * totalQty;
                    var profit = revenue - cost;

                    return new DiscountSalesReportRow
                    {
                        ProductName = g.Key.ProductName,
                        Unit = g.Key.Unit,
                        TotalQuantity = totalQty,
                        Revenue = revenue,
                        Cost = cost,
                        Profit = profit,
                        IsLoss = profit < 0,
                        OriginalPrice = sellingPrice
                    };
                })
                .OrderBy(r => r.Profit)
                .ToList();

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View("DiscountSalesReport", discounted);
        }







    }
    

    public class SalesReportRow
    {
        public string ProductName { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }    
        public decimal Profit => TotalRevenue - TotalCost;
        public string Unit { get; set; }
    }

    
    public class StockReportRow
    {
        public string Category { get; set; }
        public int ProductCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class StockSummary
    {
        public int TotalProductTypes { get; set; }
        public decimal TotalStockValue { get; set; }
        public List<StockReportRow> CategoryDetails { get; set; }
    }
    
    public class DiscountSalesReportRow
    {
        public string ProductName { get; set; }
        public string Unit { get; set; }
        public int TotalQuantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public decimal OriginalPrice { get; set; }
        public bool IsLoss { get; set; }
    }



}
