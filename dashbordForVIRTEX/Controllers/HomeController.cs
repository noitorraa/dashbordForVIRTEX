using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using dashbordForVIRTEX.Models;
using Newtonsoft.Json.Linq;
using Npgsql;
using MySql.Data.MySqlClient;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashbordForVIRTEX.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetEquipmentTimeData()
    {
        // 1) Начало текущего дня в UTC и конвертация в FILETIME
        var startOfTodayUtc = DateTime.UtcNow.Date;
        var unixSeconds     = (long)(startOfTodayUtc - DateTime.UnixEpoch).TotalSeconds;
        var cutoffFileTime  = unixSeconds * 10_000_000L + 116444736000000000L;

        // 2) Загружаем сырые данные: метка времени + value
        var rawList = await _db.DataRows
            .Where(r => r.archive_itemid == 35
                    && r.layer       == 0
                    && r.status_code == 0
                    && r.source_time >= cutoffFileTime)
            .OrderBy(r => r.source_time)
            .Select(r => new 
            {
                // конвертируем source_time в DateTimeOffset сразу на стороне клиента
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(
                                (long)(r.source_time - 116444736000000000L) / 10_000_000L)
                            .UtcDateTime,
                IsRunning = r.value > 0
            })
            .ToListAsync();

        // 3) Если записей нет — сразу нули
        if (!rawList.Any())
        {
            return Ok(new EquipmentTimeResult());
        }

        // 4) Проходим по смежным парам и накапливаем интервалы
        var totalSpan = TimeSpan.Zero;
        var runTime    = TimeSpan.Zero;
        var idleTime   = TimeSpan.Zero;

        for (int i = 0; i < rawList.Count - 1; i++)
        {
            var current = rawList[i];
            var next    = rawList[i + 1];
            var span    = next.Timestamp - current.Timestamp;

            totalSpan += span;
            if (current.IsRunning)
                runTime += span;
            else
                idleTime += span;
        }

        // 5) Возвращаем DTO
        var result = new EquipmentTimeResult
        {
            TotalSpan = totalSpan,
            RunTime    = runTime,
            IdleTime   = idleTime
        };

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProductionData()
    {
        var startOfTodayUtc = DateTime.UtcNow.Date;
        
        var unixSeconds    = (long)(startOfTodayUtc - DateTime.UnixEpoch).TotalSeconds;
        var cutoffFileTime = unixSeconds * 10_000_000L + 116444736000000000L;

        var baseQuery = _db.DataRows
            .Where(r => (r.archive_itemid == 35 || r.archive_itemid == 33)
                    && r.status_code == 0
                    && r.layer       == 0
                    && r.source_time >= cutoffFileTime);

        var stats = await baseQuery
            .GroupBy(r => r.archive_itemid)
            .Select(g => new
            {
                ItemId = g.Key,
                Sum    = (decimal)g.Sum(r => r.value),
                Avg    = (decimal)g.Average(r => r.value)
            })
            .ToListAsync();

        var productCount = stats.FirstOrDefault(x => x.ItemId == 35)?.Sum ?? 0m;
        var productivity = stats.FirstOrDefault(x => x.ItemId == 33)?.Avg ?? 0m;

        return Ok(new ProductionData
        {
            ProductCount = productCount,
            Productivity = productivity
        });
    }

    // GET: /Home/GetLatestScadaString
    [HttpGet]
    public async Task<IActionResult> GetLatestScadaString()
    {
        var last = await _db.ScadaStrings
            .OrderByDescending(s => s.Id)
            .Select(s => s.Value).FirstOrDefaultAsync();

        return Ok(new { scadaString = last });
    }

    // POST: /Home/SaveScadaString
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveScadaString([FromBody] ScadaStringModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ScadaString))
            return BadRequest("SCADA-строка не задана");

        var entity = new ScadaString { Value = model.ScadaString };
        _db.ScadaStrings.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new { id = entity.Id });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveDbConfig([FromBody] ConnectionStringModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ConnectionString))
            return BadRequest("Пустая строка подключения");

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        var json = System.IO.File.ReadAllText(filePath);
        var jObj = JObject.Parse(json);

            // Берём или создаём секцию ConnectionStrings
        var connSection = jObj["ConnectionStrings"] as JObject ?? new JObject();

            // Пишем нашу строку под ключом DefaultConnection
        connSection["DefaultConnection"] = model.ConnectionString;
        jObj["ConnectionStrings"] = connSection;

            // Сохраняем файл с отступами для читабельности
        System.IO.File.WriteAllText(filePath, jObj.ToString(Newtonsoft.Json.Formatting.Indented));
        return Ok();  // 200 OK
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestDbConnection([FromBody] ConnectionStringModel model)
        {
            
            if (string.IsNullOrWhiteSpace(model.ConnectionString))
                return BadRequest("Пустая строка подключения");

            try
            {
                var cs = model.ConnectionString;
                if (cs.StartsWith("postgres://"))
                {
                    // Преобразуем postgres:// → стандартную строку Npgsql
                    var builder = new NpgsqlConnectionStringBuilder(cs);
                    using var conn = new NpgsqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();
                    await conn.CloseAsync();
                }
                else if (cs.StartsWith("mysql://"))
                {
                    var builder = new MySqlConnectionStringBuilder();
                    // mysql://user:pwd@host:port/dbname
                    var uri = new Uri(cs);
                    builder.Server   = uri.Host;
                    builder.Port     = (uint)uri.Port;
                    builder.Database = uri.AbsolutePath.Trim('/');
                    builder.UserID   = uri.UserInfo.Split(':')[0];
                    builder.Password = uri.UserInfo.Split(':')[1];
                    using var conn = new MySqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();
                    await conn.CloseAsync();
                }
                else if (cs.StartsWith("mssql://"))
                {
                    // mssql://user:pwd@host:port/dbname?... 
                    var uri = new Uri(cs);
                    var builder = new SqlConnectionStringBuilder
                    {
                        DataSource = $"{uri.Host},{uri.Port}",
                        InitialCatalog = uri.AbsolutePath.Trim('/'),
                        UserID    = uri.UserInfo.Split(':')[0],
                        Password  = uri.UserInfo.Split(':')[1],
                        Encrypt   = uri.Query.Contains("encrypt=true")
                    };
                    using var conn = new SqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();
                    await conn.CloseAsync();
                }
                else
                {
                    return BadRequest("Неизвестный формат строки подключения");
                }

                return Ok("Соединение установлено");
            }
            catch (Exception ex)
            {
                // возвращаем текст ошибки
                return BadRequest(ex.Message);
            }
        }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }
}

public class ConnectionStringModel
{
    public string? ConnectionString { get; set; }
}
public class ScadaStringModel
{
    public string ScadaString { get; set; } = null!;
}

public class EquipmentTimeResult
{
    [Column("total_span")]
    public TimeSpan TotalSpan { get; set; }

    [Column("run_time")]
    public TimeSpan RunTime { get; set; }

    [Column("idle_time")]
    public TimeSpan IdleTime { get; set; }
}

// DTO для значений продукции и производительности
public class ProductionData
{
    public decimal ProductCount { get; set; }
    public decimal Productivity { get; set; }
}