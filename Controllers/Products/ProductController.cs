using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.IO;
using System;

namespace Agromarket.Controllers
{
    [Authorize(Roles = "warehousemanager, admin, client")] // Доступ для warehousemanager, admin, client
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult ProductList(string searchQuery, string sortOrder)
        {
            var products = _context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                products = products.Where(p => p.Name.Contains(searchQuery));
            }

            products = sortOrder switch
            {
                "name_desc" => products.OrderByDescending(p => p.Name),
                "season" => products.AsEnumerable().OrderByDescending(p => p.IsInSeason()).AsQueryable(),
                _ => products.OrderBy(p => p.Name),
            };

            return View(products.ToList());
        }

        [HttpGet]
        public IActionResult AddProduct()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Categories = GetCategories();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddProduct(Product model, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        imageFile.CopyTo(memoryStream);
                        model.ImageData = memoryStream.ToArray();
                    }
                }

                // Якщо обрано "Додати нову категорію"
                if (!string.IsNullOrEmpty(model.Category) && model.Category == "new")
                {
                    model.Category = Request.Form["newCategory"].ToString();
                }

                _context.Products.Add(model);
                _context.SaveChanges();

                ViewBag.SuccessMessage = "Товар успішно додано!";
                ViewBag.Months = GetMonths();
                ViewBag.Categories = GetCategories();

                return View();
            }

            // Передаємо список місяців і категорій, якщо валідація не пройшла
            ViewBag.Months = GetMonths();
            ViewBag.Categories = GetCategories();

            return View(model);
        }

        [HttpGet]
        public IActionResult EditProduct(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            ViewBag.Months = GetMonths();
            ViewBag.Categories = GetCategories();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProduct(Product model, IFormFile? imageFile)
        {
            var product = _context.Products.Find(model.Id);
            if (product == null) return NotFound();

            product.Name = model.Name;
            product.Description = model.Description;
            product.SeasonStartMonth = model.SeasonStartMonth;
            product.SeasonEndMonth = model.SeasonEndMonth;
            product.Category = model.Category;

            if (imageFile != null && imageFile.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    imageFile.CopyTo(memoryStream);
                    product.ImageData = memoryStream.ToArray();
                }
            }

            _context.SaveChanges();
            return RedirectToAction("ProductList");
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            _context.SaveChanges();
            return RedirectToAction("ProductList");
        }

        public IActionResult Details(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        public IActionResult Catalog(string search, decimal? minPrice, decimal? maxPrice, bool inStock = false, string category = null)
        {
            var products = _context.Products.AsQueryable();

            products = products.Where(p => p.StockQuantity > 0);

            if (!string.IsNullOrEmpty(search))
            {
                products = products.Where(p => p.Name.Contains(search));
            }

            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category == category);
            }

            if (minPrice.HasValue)
            {
                products = products.Where(p => p.SellingPrice >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                products = products.Where(p => p.SellingPrice <= maxPrice.Value);
            }

            if (inStock)
            {
                products = products.Where(p => p.StockQuantity > 0);
            }

            ViewBag.Categories = _context.Products
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            return View(products.ToList());
        }




        private List<string> GetMonths()
        {
            return new List<string>
            {
                "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень"
            };
        }
        
        private List<string> GetCategories()
        {
            var defaultCategories = new List<string>
            {
                "Фрукти",
                "Овочі",
                "Ягоди",
                "Зелень",
                "Горіхи",
                "Зернові та бобові",
                "Молочні продукти",
                "М'ясо та птиця",
                "Риба та морепродукти",
                "Напої",
                "Мед та продукти бджільництва",
                "Бакалія",
                "Випічка та хліб",
                "Яйця"
            };

            var existingCategories = _context.Products
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            return defaultCategories.Union(existingCategories).ToList();
        }

    }
}
