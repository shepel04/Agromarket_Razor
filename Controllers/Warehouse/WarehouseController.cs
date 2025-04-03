using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        public IActionResult Warehouse()
        {
            var entries = _context.WarehouseEntries
                .Include(e => e.Product)
                .AsEnumerable()
                .OrderBy(e => e.Product.Name)
                .ThenBy(e => e.ExpirationDate)
                .ToList();

            ViewBag.Products = _context.Products
                .ToList()
                .Where(p => p.IsInSeason())
                .ToList();




            return View(entries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReceiveMultipleStock(List<WarehouseEntry> entries)
        {
            foreach (var newEntry in entries)
            {
                var product = _context.Products.FirstOrDefault(p => p.Id == newEntry.ProductId);
                if (product == null || !product.IsInSeason())
                    continue;

                var existing = _context.WarehouseEntries.FirstOrDefault(e =>
                    e.ProductId == newEntry.ProductId &&
                    e.Quantity == 0 &&
                    e.PurchasePrice == newEntry.PurchasePrice &&
                    e.SellingPrice == newEntry.SellingPrice &&
                    e.ShelfLifeWeeks == newEntry.ShelfLifeWeeks);

                if (existing != null)
                {
                    existing.Quantity += newEntry.Quantity;
                    existing.ReceivedDate = DateTime.UtcNow;
                    existing.ExpirationDate = existing.ReceivedDate.AddDays(existing.ShelfLifeWeeks * 7);

                    _context.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = existing.ProductId,
                        TransactionType = "Поповнення",
                        Quantity = newEntry.Quantity,
                        TransactionDate = DateTime.UtcNow,
                        PurchasePrice = existing.PurchasePrice,
                        SellingPrice = existing.SellingPrice
                    });
                }
                else
                {
                    newEntry.ReceivedDate = DateTime.UtcNow;
                    newEntry.ExpirationDate = newEntry.ReceivedDate.AddDays(newEntry.ShelfLifeWeeks * 7);
                    _context.WarehouseEntries.Add(newEntry);

                    _context.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = newEntry.ProductId,
                        TransactionType = "Надходження",
                        Quantity = newEntry.Quantity,
                        TransactionDate = DateTime.UtcNow,
                        PurchasePrice = newEntry.PurchasePrice,
                        SellingPrice = newEntry.SellingPrice
                    });
                }
            }

            _context.SaveChanges();
            return RedirectToAction("Warehouse");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeductStock(int entryId, int quantity)
        {
            var entry = _context.WarehouseEntries
                .Include(e => e.Product)
                .FirstOrDefault(e => e.Id == entryId);

            if (entry == null || quantity <= 0 || entry.Quantity < quantity)
                return BadRequest();

            entry.Quantity -= quantity;

            if (entry.Quantity == 0)
            {
                entry.ExpirationDate = null;
                entry.ShelfLifeWeeks = 0; 

                if (!entry.IsAvailableForPreorder)
                {
                    _context.WarehouseEntries.Remove(entry);
                }
            }

            _context.StockTransactions.Add(new StockTransaction
            {
                ProductId = entry.ProductId,
                TransactionType = "Списання",
                Quantity = quantity,
                TransactionDate = DateTime.UtcNow
            });

            _context.SaveChanges();
            return RedirectToAction("Warehouse");
        }
        
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Catalog(string search, decimal? minPrice, decimal? maxPrice, bool inStock = false, string category = null, int page = 1, int pageSize = 12)
        {
            var entriesQuery = _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.Product != null);

            // Пошук
            if (!string.IsNullOrWhiteSpace(search))
            {
                entriesQuery = entriesQuery.Where(e => e.Product.Name.Contains(search));
            }

            // Категорія
            if (!string.IsNullOrWhiteSpace(category))
            {
                entriesQuery = entriesQuery.Where(e => e.Product.Category == category);
            }

            // Діапазон цін
            if (minPrice.HasValue)
            {
                entriesQuery = entriesQuery.Where(e => e.SellingPrice >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                entriesQuery = entriesQuery.Where(e => e.SellingPrice <= maxPrice.Value);
            }

            // Тільки в наявності
            if (inStock)
            {
                entriesQuery = entriesQuery.Where(e => e.Quantity > 0);
            }

            // Групуємо по товарах: беремо перше надходження з наявністю або найсвіжіше
            var grouped = entriesQuery
                .AsEnumerable()
                .GroupBy(e => e.ProductId)
                .Select(g => g
                    .OrderByDescending(e => e.Quantity > 0)
                    .ThenBy(e => e.ExpirationDate)
                    .First())
                .ToList();

            // Пагінація
            var totalItems = grouped.Count;
            var paged = grouped
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Категорії для фільтра
            ViewBag.Categories = _context.Products
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(paged);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TogglePreorder([FromBody] TogglePreorderRequest request)
        {
            var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == request.EntryId);
            if (entry == null) return NotFound();

            entry.IsAvailableForPreorder = request.IsAvailable;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReceiveToExisting(int entryId, int quantity, int shelfLifeWeeks)
        {
            var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null || quantity <= 0 || shelfLifeWeeks <= 0) return BadRequest();

            entry.Quantity += quantity;
            entry.ShelfLifeWeeks = shelfLifeWeeks;
            entry.ReceivedDate = DateTime.UtcNow;
            entry.ExpirationDate = entry.ReceivedDate.AddDays(shelfLifeWeeks * 7);

            _context.StockTransactions.Add(new StockTransaction
            {
                ProductId = entry.ProductId,
                TransactionType = "Поповнення",
                Quantity = quantity,
                TransactionDate = DateTime.UtcNow,
                PurchasePrice = entry.PurchasePrice,
                SellingPrice = entry.SellingPrice
            });

            _context.SaveChanges();
            return RedirectToAction("Warehouse");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteEntry(int entryId)
        {
            var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null && entry.Quantity == 0)
            {
                _context.WarehouseEntries.Remove(entry);
                _context.SaveChanges();
            }
            return RedirectToAction("Warehouse");
        }

        [HttpGet]
        public IActionResult StockHistory(string filterType)
        {
            var transactions = _context.StockTransactions
                .Include(t => t.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterType))
            {
                transactions = transactions.Where(t => t.TransactionType == filterType);
            }

            var result = transactions
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            ViewBag.FilterType = filterType;
            return View(result);
        }

        public class TogglePreorderRequest
        {
            public int EntryId { get; set; }
            public bool IsAvailable { get; set; }
        }
    }
}
