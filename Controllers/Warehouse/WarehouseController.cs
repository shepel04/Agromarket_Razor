using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Collections.Generic;

namespace Agromarket.Controllers.Warehouse
{
    [Authorize(Roles = "warehousemanager,admin")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarehouseController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Warehouse()
        {
            var products = _context.Products.Where(p => p.StockQuantity > 0).ToList();
            ViewBag.Products = _context.Products.ToList();
            return View(products);
        }

        [HttpPost]
        public IActionResult ReceiveMultipleStock(List<StockItem> stockItems)
        {
            foreach (var item in stockItems)
            {
                var product = _context.Products.Find(item.ProductId);
                if (product == null) continue;

                product.StockQuantity += item.Quantity;
                product.PurchasePrice = item.PurchasePrice;
                product.SellingPrice = item.SellingPrice;
                product.ReceivedDate = DateTime.UtcNow;
                product.ShelfLifeWeeks = item.ShelfLifeWeeks;

                var transaction = new StockTransaction
                {
                    ProductId = product.Id,
                    TransactionType = "Надходження",
                    Quantity = item.Quantity,
                    TransactionDate = DateTime.UtcNow,
                    PurchasePrice = item.PurchasePrice,
                    SellingPrice = item.SellingPrice
                };

                _context.StockTransactions.Add(transaction);
            }

            _context.SaveChanges();
            return RedirectToAction("Warehouse");
        }

        [HttpPost]
        public IActionResult DeductStock(int productId, int quantity)
        {
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            if (quantity > 0 && product.StockQuantity >= quantity)
            {
                product.StockQuantity -= quantity;

                if (product.StockQuantity == 0)
                {
                    product.ReceivedDate = null;
                }

                var transaction = new StockTransaction
                {
                    ProductId = product.Id,
                    TransactionType = "Списання",
                    Quantity = quantity,
                    TransactionDate = DateTime.UtcNow,
                    SellingPrice = product.SellingPrice
                };

                _context.StockTransactions.Add(transaction);
                _context.SaveChanges();
            }

            return RedirectToAction("Warehouse");
        }

        [HttpGet]
        public IActionResult StockHistory(string filterType)
        {
            var history = _context.StockTransactions
                .OrderByDescending(t => t.TransactionDate)
                .AsQueryable(); 

            if (!string.IsNullOrEmpty(filterType))
            {
                history = history.Where(t => t.TransactionType == filterType);
            }

            ViewBag.FilterType = filterType; 
            return View(history.ToList());
        }

    }
}
