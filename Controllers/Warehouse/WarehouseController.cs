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

<<<<<<< Updated upstream
=======
        public IActionResult Warehouse(string sort = "name", string order = "asc")
        {
            ApplyExpirationDiscount();
            var now = DateTime.UtcNow;

            var allEntries = _context.WarehouseEntries
                .Include(e => e.Product)
                .ToList();

            var expiredFully = allEntries.Where(e => e.ExpirationDate.HasValue && e.Quantity > 0 && e.ExpirationDate.Value <= now).ToList();
            var nearExpiration = allEntries.Where(e => e.ExpirationDate.HasValue && e.Quantity > 0 && e.ExpirationDate.Value > now && e.ExpirationDate.Value <= now.AddDays(4)).ToList();
            var mainEntries = allEntries.Where(e => !expiredFully.Contains(e)).AsQueryable();

            mainEntries = (sort, order.ToLower()) switch
            {
                ("name", "asc") => mainEntries.OrderBy(e => e.Product.Name),
                ("name", "desc") => mainEntries.OrderByDescending(e => e.Product.Name),
                ("quantity", "asc") => mainEntries.OrderBy(e => e.Quantity),
                ("quantity", "desc") => mainEntries.OrderByDescending(e => e.Quantity),
                ("price", "asc") => mainEntries.OrderBy(e => e.SellingPrice),
                ("price", "desc") => mainEntries.OrderByDescending(e => e.SellingPrice),
                _ => mainEntries.OrderBy(e => e.Product.Name)
            };

            ViewBag.Sort = sort;
            ViewBag.Order = order;
            
            var monthMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["січень"] = 1, ["лютий"] = 2, ["березень"] = 3, ["квітень"] = 4,
                ["травень"] = 5, ["червень"] = 6, ["липень"] = 7, ["серпень"] = 8,
                ["вересень"] = 9, ["жовтень"] = 10, ["листопад"] = 11, ["грудень"] = 12
            };
            
            var currentMonth = DateTime.UtcNow.Month;

            ViewBag.Products = _context.Products
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SeasonStartMonth,
                    p.SeasonEndMonth
                })
                .ToList() // переходимо до LINQ-to-Objects
                .Where(p =>
                    monthMap.TryGetValue(p.SeasonStartMonth?.ToLower(), out var startMonth) &&
                    monthMap.TryGetValue(p.SeasonEndMonth?.ToLower(), out var endMonth) &&
                    (
                        (startMonth <= endMonth && startMonth <= currentMonth && currentMonth <= endMonth) ||
                        (startMonth > endMonth && (currentMonth >= startMonth || currentMonth <= endMonth))
                    )
                )
                .Select(p => new { p.Id, p.Name })
                .ToList();
            
            ViewBag.SpoilableEntries = expiredFully.Concat(nearExpiration).ToList();

            return View(mainEntries.ToList());
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
        
>>>>>>> Stashed changes
        [HttpGet]
        public IActionResult Warehouse(int? selectedProductId)
        {
<<<<<<< Updated upstream
            var products = _context.Products.Where(p => p.StockQuantity > 0).ToList();

            ViewBag.Products = _context.Products
=======
            var entriesQuery = _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.Product != null &&
                            (e.Quantity > 0 || e.IsAvailableForPreorder));
            
                
        
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
        
            // НОВА УМОВА — виключаємо прострочені товари
            entriesQuery = entriesQuery.Where(e => 
                e.ExpirationDate == null || e.ExpirationDate > DateTime.UtcNow
            );
        
            // Групуємо по товарах
            var grouped = entriesQuery
>>>>>>> Stashed changes
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
