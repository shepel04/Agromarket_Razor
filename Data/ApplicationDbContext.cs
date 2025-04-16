using Agromarket.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Agromarket.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SupplyProduct>()
            .HasOne(sp => sp.Supplier)
            .WithMany(s => s.SupplyProducts)
            .HasForeignKey(sp => sp.SupplierId);

        modelBuilder.Entity<SupplyProduct>()
            .HasOne(sp => sp.Product)
            .WithMany()
            .HasForeignKey(sp => sp.ProductId);
    }
    
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    
    public DbSet<OrderItem> OrderItems { get; set; }
    
    public DbSet<WarehouseEntry> WarehouseEntries { get; set; }
    
    public DbSet<StockTransaction> StockTransactions { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    
    public DbSet<SupplyProduct> SupplyProducts { get; set; } 
    public DbSet<SupplyOrder> SupplyOrders { get; set; }
    
}