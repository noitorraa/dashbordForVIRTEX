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
    private readonly IDateTimeProvider _clock;
    private const int _maxProd = 15000;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db, IDateTimeProvider clock)
    {
        _logger = logger;
        _db = db;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> GetEquipmentTimeData(
        int archiveItemId = 35,
        int layer         = 0,
        DateTime? dateUtc = null)
    {
        // 1) День, начало и конец в FILETIME
        var day           = dateUtc?.Date ?? _clock.UtcNow.AddDays(-18).Date; // убрать добавочный (-1) год
        var startFileTime = day        .ToFileTimeUtc();
        var endFileTime   = day.AddDays(1).ToFileTimeUtc();

        // 2) Срез данных именно за этот день
        var rawList = await _db.DataRows
            .Where(r => r.archive_itemid == archiveItemId
                    && r.layer       == layer
                    && r.status_code == 0
                    && r.source_time >= startFileTime
                    && r.source_time <  endFileTime)
            .OrderBy(r => r.source_time)
            .Select(r => new EquipmentPoint(
                DateTimeOffset.FromFileTime(r.source_time),
                r.value > 0))
            .ToListAsync();

        // Если нет точек — сразу нули
        if (!rawList.Any())
            return Ok(new EquipmentTimeResultDto());

        // 3) Вставляем границы дня
        var startOfDay = new DateTimeOffset(day, TimeSpan.Zero);
        var endOfDay   = startOfDay.AddDays(1);

        rawList.Insert(0, new EquipmentPoint(startOfDay, rawList.First().IsRunning));
        rawList.Add(    new EquipmentPoint(endOfDay,   false));

        // 4) Считаем интервалы
        var segments = rawList
            .Zip(rawList.Skip(1), (curr, next) => new {
                Duration  = next.Timestamp - curr.Timestamp,
                IsRunning = curr.IsRunning
            })
            .ToList();

        var totalMinutes = segments.Sum(s => s.Duration.TotalMinutes);
        var runMinutes   = segments.Where(s => s.IsRunning)
                                .Sum(s => s.Duration.TotalMinutes);
        var idleMinutes  = totalMinutes - runMinutes;

        // 5) Возвращаем DTO
        return Ok(new EquipmentTimeResultDto {
            TotalMinutes = totalMinutes,
            RunMinutes   = runMinutes,
            IdleMinutes  = idleMinutes
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetProductionData(
        int productItemId      = 35,
        int productivityItemId = 33,
        int layer              = 0,
        DateTime? dateUtc      = null)
    {
        // 1) Определяем «день» и его FILETIME‑границы
        var day           = dateUtc?.Date ?? _clock.UtcNow.AddDays(-18).Date;
        var startFileTime = day       .ToFileTimeUtc();
        var endFileTime   = day.AddDays(1).ToFileTimeUtc();

        // 2) Фильтруем по периоду
        var query = _db.DataRows
            .Where(r => (r.archive_itemid == productItemId
                    || r.archive_itemid == productivityItemId)
                    && r.layer       == layer
                    && r.status_code == 0
                    && r.source_time >= startFileTime
                    && r.source_time <  endFileTime);

        // 3) Для каждого ID берём запись с максимальным временем
        var latestValues = await query
            .GroupBy(r => r.archive_itemid)
            .Select(g => new
            {
                ItemId    = g.Key,
                // сортируем по source_time DESC и берём первое значение value
                LastValue = g
                    .OrderByDescending(r => r.source_time)
                    .Where(r => r.value > 0)
                    .Select(r => (decimal)r.value)
                    .FirstOrDefault()
            })
            .ToListAsync();

        // 4) Извлекаем нужные показатели
        var productCount = latestValues
            .FirstOrDefault(x => x.ItemId == productItemId)?
            .LastValue ?? 0m;

        var productivity = latestValues
            .FirstOrDefault(x => x.ItemId == productivityItemId)?
            .LastValue ?? 0m;

        // 5) Возвращаем DTO
        return Ok(new ProductionDataDto
        {
            ProductCount = productCount,
            Productivity = (double)productivity
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

    // Возвращает массив точек { hour: int, runMinutes: double, idleMinutes: double }
    [HttpGet]
    public async Task<IActionResult> GetHourlyEquipmentData(
        int archiveItemId = 35, int layer = 0, DateTime? dateUtc = null)
    {
        // 1) Границы дня
        var day       = dateUtc?.Date ?? _clock.UtcNow.AddDays(-18).Date;
        var start     = new DateTimeOffset(day, TimeSpan.Zero);
        var end       = start.AddDays(1);

        // 2) Сырой список с границами
        var raw = await _db.DataRows
            .Where(r => r.archive_itemid == archiveItemId
                    && r.source_time >= start.ToFileTime()   // <-- вместо ToFileTimeUtc()
                    && r.source_time <  end.ToFileTime())    // <-- вместо ToFileTimeUtc()
            .OrderBy(r => r.source_time)
            .Select(r => new EquipmentPoint(
                DateTimeOffset.FromFileTime(r.source_time),
                r.value > 0))
            .ToListAsync();

        if (!raw.Any())
        {
            // возвращаем 24 пустые записи
            return Ok(Enumerable.Range(0,24).Select(h => new HourlyEquipmentDto {
                Hour = h, RunMinutes = 0, IdleMinutes = 0
            }));
        }

        // вставляем рамки дня
        raw.Insert(0, new EquipmentPoint(start, raw.First().IsRunning));
        raw.Add(new EquipmentPoint(end, false));

        // 3) Группируем по сегментам и "режем" по часам
        var perHour = new HourlyEquipmentDto[24];
        for (int h = 0; h < 24; h++)
            perHour[h] = new HourlyEquipmentDto { Hour = h };

        for (int i = 0; i < raw.Count - 1; i++)
        {
            var curr = raw[i];
            var next = raw[i + 1];
            var segStart = curr.Timestamp;
            var segEnd   = next.Timestamp;
            var isRun    = curr.IsRunning;

            // разбиваем сегмент по часам
            while (segStart < segEnd)
            {
                var hourStart = new DateTimeOffset(
                    segStart.Year, segStart.Month, segStart.Day,
                    segStart.Hour, 0, 0, TimeSpan.Zero);
                var hourEnd   = hourStart.AddHours(1);

                // граница внутри сегмента
                var sliceEnd = segEnd < hourEnd ? segEnd : hourEnd;
                var minutes  = (sliceEnd - segStart).TotalMinutes;

                if (isRun)
                    perHour[segStart.Hour].RunMinutes += minutes;
                else
                    perHour[segStart.Hour].IdleMinutes += minutes;

                // двигаемся дальше
                segStart = sliceEnd;
            }
        }

        return Ok(perHour);
    }

    // Возвращает массив { date: "YYYY-MM-DD", oee: double }
    [HttpGet]
    public async Task<IActionResult> GetWeeklyOee(
        int archiveItemId = 35, int productItemId = 35, int productivityItemId = 33, int layer = 0)
    {
        var today   = _clock.UtcNow.AddDays(-18).Date;
        var weekAgo = today.AddDays(-6); // -6
        var series  = new List<object>();

        for (var d = weekAgo; d <= today; d = d.AddDays(1))
        {
            // Equipment
            var startF = d.ToFileTimeUtc();
            var endF   = d.AddDays(1).ToFileTimeUtc();
            var rawEq  = await _db.DataRows
                .Where(r => r.archive_itemid == archiveItemId && r.layer == layer
                        && r.status_code == 0
                        && r.source_time >= startF && r.source_time < endF)
                .OrderBy(r=>r.source_time)
                .Select(r=> new EquipmentPoint(
                    DateTimeOffset.FromFileTime(r.source_time),
                    r.value > 0))
                .ToListAsync();

            if (!rawEq.Any()) {
                series.Add(new { date = d.ToString("yyyy-MM-dd"), oee = 0.0 });
                continue;
            }

            rawEq.Insert(0, new EquipmentPoint(new DateTimeOffset(d, TimeSpan.Zero), rawEq.First().IsRunning));
            rawEq.Add(new EquipmentPoint(new DateTimeOffset(d.AddDays(1), TimeSpan.Zero), false));

            var seg = rawEq.Zip(rawEq.Skip(1), (c, n) => new {
                Duration  = n.Timestamp - c.Timestamp,
                IsRunning = c.IsRunning
            });
            var total = seg.Sum(s=>s.Duration.TotalMinutes);
            var run   = seg.Where(s=>s.IsRunning).Sum(s=>s.Duration.TotalMinutes);
            var avail = run / total;

            // Production
            var rawProd = await _db.DataRows
                .Where(r => (r.archive_itemid == productItemId || r.archive_itemid == productivityItemId)
                        && r.layer == layer
                        && r.status_code == 0
                        && r.source_time >= startF && r.source_time < endF)
                .GroupBy(r=>r.archive_itemid)
                .Select(g=>new {
                    ItemId    = g.Key,
                    LastValue = g.Where(r=>r.value>0)
                                .OrderByDescending(r=>r.source_time)
                                .Select(r=>(decimal)r.value)
                                .FirstOrDefault()
                })
                .ToListAsync();

            var count = rawProd.FirstOrDefault(x=>x.ItemId==productItemId)?.LastValue ?? 0m;
            var prodv = (double)(rawProd.FirstOrDefault(x=>x.ItemId==productivityItemId)?.LastValue ?? 0m);
            var perf  = _maxProd>0 ? prodv / _maxProd : 0;

            series.Add(new { date = d.ToString("yyyy-MM-dd"), oee = avail * perf });
        }

        return Ok(series);
    }


    // Возвращает массив { hour: int, count: decimal }
    [HttpGet]
    public async Task<IActionResult> GetHourlyProductionData(
        int productItemId = 35, DateTime? dateUtc = null)
    {
        var day      = (dateUtc?.Date ?? _clock.UtcNow.AddDays(-18).Date);
        var startFile= day      .ToFileTimeUtc();
        var endFile  = day.AddDays(1).ToFileTimeUtc();

        var raw = await _db.DataRows
            .Where(r => r.archive_itemid==productItemId
                    && r.source_time >= startFile
                    && r.source_time <  endFile
                    && r.status_code==0)
            .OrderBy(r=>r.source_time)
            .Select(r=> new {
                Time  = DateTimeOffset.FromFileTime(r.source_time).UtcDateTime,
                Count = (decimal)r.value
            })
            .ToListAsync();

        // Предполагаем, что поле Count — это накопительное суточное количество.
        // Тогда почасовой выпуск = разница между соседними точками в каждом часе.
        var hourly = raw
            .GroupBy(x => x.Time.Hour)
            .Select(g => {
                var first = g.First().Count;
                var last  = g.Last().Count;
                return new {
                    hour  = g.Key,
                    count = (decimal) (last - first)
                };
            })
            .OrderBy(x=>x.hour)
            .ToList();

        return Ok(hourly);
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

    public record EquipmentPoint(DateTimeOffset Timestamp, bool IsRunning);


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

public class EquipmentTimeResultDto
{
    [Column("total_span")]
    public double TotalMinutes { get; set; }

    [Column("run_time")]
    public double RunMinutes { get; set; }

    [Column("idle_time")]
    public double IdleMinutes { get; set; }
}
public class ProductionDataDto
    {
        public decimal ProductCount { get; set; }

        public double Productivity { get; set; }
    }

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

// Реализация по умолчанию
public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public class HourlyEquipmentDto
{
    public int Hour { get; set; }  
    public double RunMinutes  { get; set; }
    public double IdleMinutes { get; set; }
}