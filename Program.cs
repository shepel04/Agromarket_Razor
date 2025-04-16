using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Agromarket.Data;
using Agromarket.Services;

var builder = WebApplication.CreateBuilder(args);

// Підключення до PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)); 

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Додаємо Identity з ролями
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Автоматичне призначення ролі "client" новим користувачам
builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.RequireUniqueEmail = true;
});

// Додаємо підтримку сесій
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Час зберігання сесії
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Додаємо MVC
builder.Services.AddControllersWithViews();

// Додаємо сервіс для ініціалізації ролей та адміністратора
builder.Services.AddHostedService<DatabaseInitializerService>();

builder.Services.AddHttpClient();

var app = builder.Build();

// Конфігурація HTTP-пайплайну
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Додаємо сесії
app.UseSession();

app.UseAuthorization();

// Маршрутизація
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
).WithStaticAssets();

// Додаємо маршрут до адмін-панелі
app.MapControllerRoute(
    name: "admin",
    pattern: "admin",
    defaults: new { controller = "Admin", action = "Index" }
);

app.MapRazorPages().WithStaticAssets();

app.Run();
