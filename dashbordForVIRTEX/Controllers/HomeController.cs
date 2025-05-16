using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using dashbordForVIRTEX.Models;
using Newtonsoft.Json.Linq;
using Npgsql;
using MySql.Data.MySqlClient;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using dashbordForVIRTEX.Services;
using System.ComponentModel.DataAnnotations.Schema;
using dashbordForVIRTEX.DTOs;
using Microsoft.IdentityModel.Tokens;

namespace dashbordForVIRTEX.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IEquipmentService _equipmentService;
    private readonly IProductionService _productionService;
    private readonly IConfigurationService _configService;
    private const int _maxProd = 15000;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext db,
        IDateTimeProvider clock,
        IEquipmentService equipmentService,
        IProductionService productionService,
        IConfigurationService configService)
    {
        _logger = logger;
        _db = db;
        _clock = clock;
        _equipmentService = equipmentService;
        _productionService = productionService;
        _configService = configService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHourlyProductionData(
        int productItemId = 35, DateTime? dateUtc = null)
    {
        var day = (dateUtc?.Date ?? _clock.UtcNow.AddDays(-30).Date);
        var startFile = day.ToFileTimeUtc();
        var endFile = day.AddDays(1).ToFileTimeUtc();

        var raw = await _db.DataRows
            .Where(r => r.archive_itemid == productItemId
                    && r.source_time >= startFile
                    && r.source_time < endFile
                    && r.status_code == 0)
            .OrderBy(r => r.source_time)
            .Select(r => new {
                Time = DateTimeOffset.FromFileTime(r.source_time).UtcDateTime,
                Count = (decimal)r.value
            })
            .ToListAsync();

        // Расчет почасового производства
        var hourly = raw
            .GroupBy(x => x.Time.Hour)
            .Select(g => {
                var first = g.First().Count;
                var last = g.Last().Count;
                return new {
                    hour = g.Key,
                    count = (decimal)(last - first)
                };
            })
            .OrderBy(x => x.hour)
            .ToList();

        return Ok(hourly);
    }

    [HttpGet]
    public async Task<IActionResult> GetEquipmentTimeData(
        int archiveItemId = 35,
        int layer = 0,
        DateTime? dateUtc = null)
    {
        var day = dateUtc?.Date ?? _clock.UtcNow.AddDays(-30).Date;
        var (start, end) = DateUtils.GetDayFileTimeRange(day);

        var rawList = await _db.DataRows
            .Where(r => r.archive_itemid == archiveItemId &&
                       r.layer == layer &&
                       r.status_code == 0 &&
                       r.source_time >= start &&
                       r.source_time < end)
            .OrderBy(r => r.source_time)
            .Select(r => new EquipmentPoint(
                DateTimeOffset.FromFileTime(r.source_time),
                r.value > 0))
            .ToListAsync();

        if (rawList.IsNullOrEmpty())
            return Ok(new EquipmentTimeResultDto());

        var result = _equipmentService.CalculateTimeMetrics(rawList, day);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProductionData(
        int productItemId = 35,
        int productivityItemId = 33,
        int layer = 0,
        DateTime? dateUtc = null)
    {
        var day = dateUtc?.Date ?? _clock.UtcNow.AddDays(-30).Date;
        var (start, end) = DateUtils.GetDayFileTimeRange(day);

        var result = await _productionService.GetProductionDataAsync(
            _db, productItemId, productivityItemId, layer, start, end);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHourlyEquipmentData(
        int archiveItemId = 35, int layer = 0, DateTime? dateUtc = null)
    {
        var day = dateUtc?.Date ?? _clock.UtcNow.AddDays(-30).Date;
        var (start, end) = DateUtils.GetDayFileTimeRange(day, false);

        var raw = await _db.DataRows
            .Where(r => r.archive_itemid == archiveItemId &&
                       r.source_time >= start &&
                       r.source_time < end)
            .OrderBy(r => r.source_time)
            .Select(r => new EquipmentPoint(
                DateTimeOffset.FromFileTime(r.source_time),
                r.value > 0))
            .ToListAsync();

        if (raw.IsNullOrEmpty())
            return Ok(HourlyEquipmentDto.GetEmptyHourlyData());

        raw.Insert(0, new EquipmentPoint(new DateTimeOffset(day, TimeSpan.Zero), raw.First().IsRunning));
        raw.Add(new EquipmentPoint(new DateTimeOffset(day.AddDays(1), TimeSpan.Zero), false));

        var hourlyData = _equipmentService.SplitByHours(raw);
        return Ok(hourlyData);
    }

    [HttpGet]
    public async Task<IActionResult> GetWeeklyOee(
        int archiveItemId = 35, int productItemId = 35, int productivityItemId = 33, int layer = 0)
    {
        var today = _clock.UtcNow.AddDays(-30).Date;
        var weekAgo = today.AddDays(-6);
        var series = new List<object>();

        for (var currentDate = weekAgo; currentDate <= today; currentDate = currentDate.AddDays(1))
        {
            var (start, end) = DateUtils.GetDayFileTimeRange(currentDate);

            var equipmentData = await _equipmentService.GetEquipmentDataAsync(_db, archiveItemId, layer, start, end);
            var productionData = await _productionService.GetProductionDataAsync(_db, productItemId, productivityItemId, layer, start, end);

            var equipmentMetrics = _equipmentService.CalculateTimeMetrics(equipmentData, currentDate); // Теперь переменная имеет смысл
            var oee = CalculateOee(equipmentMetrics, productionData);
            series.Add(new { date = currentDate.ToString("yyyy-MM-dd"), oee });
        }

        return Ok(series);
    }

    private double CalculateOee(EquipmentTimeResultDto equipment, ProductionDataDto production)
    {
        var availability = equipment.TotalMinutes == 0 
            ? 0 
            : equipment.RunMinutes / equipment.TotalMinutes;

        var performance = _maxProd > 0 
            ? production.Productivity / _maxProd 
            : 0;

        return availability * performance;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveDbConfig([FromBody] ConnectionStringModel model)
    {
        return _configService.SaveConfiguration(model.ConnectionString);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestDbConnection([FromBody] ConnectionStringModel model)
    {
        return await _configService.TestConnectionAsync(model.ConnectionString);
    }

    [HttpGet]
    public IActionResult Index() => View();
    [HttpGet]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}

public record EquipmentPoint(DateTimeOffset Timestamp, bool IsRunning);

public static class DateUtils
    {
        public static (long start, long end) GetDayFileTimeRange(DateTime day, bool utc = true)
        {
            var startOfDay = utc ? new DateTimeOffset(day, TimeSpan.Zero) : new DateTimeOffset(day, TimeSpan.Zero).ToLocalTime();
            return (startOfDay.ToFileTime(), startOfDay.AddDays(1).ToFileTime());
        }
    }