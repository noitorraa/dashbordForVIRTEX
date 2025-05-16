using dashbordForVIRTEX.Controllers;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using dashbordForVIRTEX.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) Добавляем MVC
builder.Services.AddControllersWithViews();

// 2) Добавляем EF Core с Postgres
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>(); 
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

var app = builder.Build();

// 3) Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// 4) Статика из wwwroot
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// 5) Маршруты контроллеров
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
