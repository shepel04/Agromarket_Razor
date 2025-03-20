using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public IActionResult Warehouse(int? selectedProductId)
        {
            var products = _context.Products.Where(p => p.StockQuantity > 0).ToList();

            ViewBag.Products = _context.Products
                .AsEnumerable() 
                .Where(p => p.IsInSeason()) 
                .ToList();

            ViewBag.SelectedProductId = selectedProductId;

            return View(products);
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReceiveMultipleStock(List<StockItem> stockItems)
        {
            foreach (var item in stockItems)
            {
                var product = _context.Products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product == null || !product.IsInSeason())
                {
                    continue;
                }

                // Додаємо товар у складський облік
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
        [ValidateAntiForgeryToken]
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
