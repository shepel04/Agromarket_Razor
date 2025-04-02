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
                .AsEnumerable()
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
                    e.ShelfLifeWeeks == newEntry.ShelfLifeWeeks
                );

                if (existing != null)
                {
                    existing.Quantity += newEntry.Quantity;
                    existing.ReceivedDate = DateTime.UtcNow;
                    existing.ExpirationDate = existing.ReceivedDate.AddDays(existing.ShelfLifeWeeks * 7);
                }
                else
                {
                    newEntry.ReceivedDate = DateTime.UtcNow;
                    newEntry.ExpirationDate = newEntry.ReceivedDate.AddDays(newEntry.ShelfLifeWeeks * 7);
                    _context.WarehouseEntries.Add(newEntry);
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

                if (!entry.IsAvailableForPreorder)
                {
                    _context.WarehouseEntries.Remove(entry);
                }
            }

            _context.SaveChanges();
            return RedirectToAction("Warehouse");
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

        public class TogglePreorderRequest
        {
            public int EntryId { get; set; }
            public bool IsAvailable { get; set; }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Catalog(string search, decimal? minPrice, decimal? maxPrice, bool inStock = false, string category = null, int page = 1, int pageSize = 12)
        {
            var entriesQuery = _context.WarehouseEntries
                .Include(e => e.Product)
                .AsEnumerable()
                .Where(e =>
                        (e.Quantity > 0 || e.IsAvailableForPreorder) &&
                        (string.IsNullOrEmpty(search) || e.Product.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrEmpty(category) || e.Product.Category == category) &&
                        (!minPrice.HasValue || e.SellingPrice >= minPrice) &&
                        (!maxPrice.HasValue || e.SellingPrice <= maxPrice) &&
                        (!inStock || e.Quantity > 0)
                );

            var grouped = entriesQuery
                .GroupBy(e => e.ProductId)
                .Select(g => g.OrderByDescending(e => e.Quantity > 0).ThenBy(e => e.ExpirationDate).First())
                .ToList();

            var totalItems = grouped.Count;
            var paged = grouped
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Categories = _context.Products
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category)
                .Distinct()
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(paged);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteEntry(int entryId)
        {
            var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
                _context.WarehouseEntries.Remove(entry);
                _context.SaveChanges();
            }
            return RedirectToAction("Warehouse");
        }


    }
}
