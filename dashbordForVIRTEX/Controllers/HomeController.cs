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
    private readonly IPdfReportService _pdfService;
    private const int _maxProd = 15000;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext db,
        IDateTimeProvider clock,
        IEquipmentService equipmentService,
        IProductionService productionService,
        IConfigurationService configService,
        IPdfReportService pdfService)
    {
        _logger = logger;
        _db = db;
        _clock = clock;
        _equipmentService = equipmentService;
        _productionService = productionService;
        _configService = configService;
        _pdfService = pdfService;
    }

    [HttpGet("Home/GenerateReport")]
    public async Task<IActionResult> GenerateReport(
        string period,
        int productItemId,
        int productivityItemId)
    {
        var todayUtc = _clock.UtcNow.AddDays(-30).Date;
        var report = new ReportDataDto
        {
            EquipmentName = GetEquipmentName(productItemId),
            Period = period,
            ReportDate = todayUtc
        };

        switch (period)
        {
            case "day":
                await LoadDaily(report, productItemId, productivityItemId, todayUtc);
                break;
            case "week":
                await LoadPeriodOee(
                    report,
                    productItemId,
                    productivityItemId,
                    todayUtc.AddDays(-6),
                    todayUtc);
                break;
            case "month":
                var startMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1);
                await LoadPeriodOee(
                    report,
                    productItemId,
                    productivityItemId,
                    startMonth,
                    todayUtc);
                break;
            default:
                return BadRequest("Неверный период");
        }

        var pdfBytes = _pdfService.GeneratePdf(report);
        return File(
            pdfBytes,
            "application/pdf",
            $"report_{period}_{report.ReportDate:yyyy-MM-dd}.pdf");
    }

    private async Task LoadDaily(
        ReportDataDto report,
        int prodId,
        int prodId2,
        DateTime dayUtc)
    {
        var (start, end) = DateUtils.GetDayFileTimeRange(dayUtc);

            // Получение данных для суточного отчета
            var equipRaw = await _equipmentService
                .GetEquipmentDataAsync(_db, prodId, 0, start, end);
            var equipmentMetrics = _equipmentService.CalculateTimeMetrics(equipRaw, dayUtc);
            
            var productionMetrics = await _productionService
                .GetProductionDataAsync(_db, prodId, prodId2, 0, start, end);

            // Заполняем только DailyMetrics
            report.DailyMetrics = new DailyMetricsDto 
            {
                TotalTime = Math.Round(equipmentMetrics.TotalMinutes, 1),
                RunTime = Math.Round(equipmentMetrics.RunMinutes, 1),
                IdleTime = Math.Round(equipmentMetrics.IdleMinutes, 1),
                ProductCount = (double)Math.Round(productionMetrics.ProductCount, 1),
                Productivity = Math.Round(productionMetrics.Productivity, 1),
                OEE = Math.Round(CalculateOee(equipmentMetrics, productionMetrics) * 100, 1),
                Availability = equipmentMetrics.TotalMinutes > 0 
                    ? Math.Round((equipmentMetrics.RunMinutes / equipmentMetrics.TotalMinutes) * 100, 1)
                    : 0,
                Performance = _maxProd > 0
                    ? Math.Round((productionMetrics.Productivity / _maxProd) * 100, 1)
                    : 0
            };
        // Остальная существующая логика
        var raw = await _db.DataRows
            .Where(r =>
                r.archive_itemid == prodId &&
                r.status_code == 0 &&
                r.source_time >= start &&
                r.source_time < end)
            .OrderBy(r => r.source_time)
            .Select(r => new
            {
                TimeUtc = DateTimeOffset.FromFileTime(r.source_time).UtcDateTime,
                Value = (decimal)r.value
            })
            .ToListAsync();

        var hourly = raw
            .GroupBy(x => x.TimeUtc.Hour)
            .Select(g =>
            {
                var first = g.First().Value;
                var last = g.Last().Value;
                return new HourlyProductionReportItem
                {
                    Hour = g.Key,
                    Count = last - first,
                    Oee = 0
                };
            })
            .OrderBy(x => x.Hour)
            .ToList();

        report.HourlyProductionData = hourly;
    }

    private async Task LoadPeriodOee(
        ReportDataDto report, 
        int productItemId,
        int productivityItemId,
        DateTime startDate, 
        DateTime endDate)
    {
        var series = new List<DailyOeeItemDto>();
        
        for (var dt = startDate; dt <= endDate; dt = dt.AddDays(1))
        {
            var (start, end) = DateUtils.GetDayFileTimeRange(dt);

            // Получаем данные оборудования
            var equipmentData = await _equipmentService
                .GetEquipmentDataAsync(_db, productItemId, 0, start, end);
            
            // Получаем производственные данные
            var productionData = await _productionService
                .GetProductionDataAsync(_db, productItemId, productivityItemId, 0, start, end);

            // Рассчитываем OEE
            var equipmentMetrics = _equipmentService.CalculateTimeMetrics(equipmentData, dt);
            var oee = CalculateOee(equipmentMetrics, productionData);

            series.Add(new DailyOeeItemDto 
            {
                Date = dt,
                Oee = oee,
                ProductCount = productionData.ProductCount
            });
        }

        report.DailyOeeSeries = series;
    }

    private string GetEquipmentName(int productItemId)
    {
        return productItemId switch
        {
            35 => "Leepack",
            49 => "Omag 2",
            42 => "Стик Машек",
            _ => "Неизвестное оборудование"
        };
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
        var availability = equipment.TotalMinutes > 0
            ? equipment.RunMinutes / equipment.TotalMinutes
            : 0;

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

public interface IPdfReportService
{
    byte[] GeneratePdf(ReportDataDto report);
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
