using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System;

namespace Agromarket.Controllers.Products
{
    [Authorize(Roles = "warehousemanager, admin")] // Доступ для warehousemanager і admin
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult ProductList(string searchQuery, string sortOrder, string seasonFilter)
        {
            var products = _context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                products = products.Where(p => p.Name.Contains(searchQuery));
            }

            var productList = products.AsEnumerable();

            productList = sortOrder switch
            {
                "name_desc" => productList.OrderByDescending(p => p.Name),
                "season" => productList.OrderByDescending(p => p.IsInSeason()), 
                _ => productList.OrderBy(p => p.Name),
            };

            return View(productList.ToList());
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

                _context.Products.Add(model);
                _context.SaveChanges();

                ViewBag.SuccessMessage = "Товар успішно додано!";
                ViewBag.Months = GetMonths(); 
                return View();
            }

            ViewBag.Months = GetMonths();
            return View(model);
        }
        
        [HttpGet]
        public IActionResult AddProduct()
        {
            ViewBag.Months = GetMonths();
            return View();
        }

        [HttpGet]
        public IActionResult EditProduct(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            ViewBag.Months = GetMonths();
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

        private List<string> GetMonths()
        {
            return new List<string>
            {
                "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень"
            };
        }
    }
}
