using dashbordForVIRTEX.Controllers;
using dashbordForVIRTEX.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace dashbordForVIRTEX.Services;
public class EquipmentService : IEquipmentService
{
    public EquipmentTimeResultDto CalculateTimeMetrics(List<EquipmentPoint> rawList, DateTime day)
    {
        if (rawList.IsNullOrEmpty())
        {
            return new EquipmentTimeResultDto();
        }
        var startOfDay = new DateTimeOffset(day, TimeSpan.Zero);
        var endOfDay = startOfDay.AddDays(1);
        rawList.Insert(0, new EquipmentPoint(startOfDay, rawList.First().IsRunning));
        rawList.Add(new EquipmentPoint(endOfDay, false));

        var segments = rawList.Zip(rawList.Skip(1), (curr, next) => new
        {
            Duration = next.Timestamp - curr.Timestamp,
            IsRunning = curr.IsRunning
        }).ToList();

        return new EquipmentTimeResultDto
        {
            TotalMinutes = segments.Sum(s => s.Duration.TotalMinutes),
            RunMinutes = segments.Where(s => s.IsRunning).Sum(s => s.Duration.TotalMinutes),
            IdleMinutes = segments.Sum(s => s.Duration.TotalMinutes) -
                         segments.Where(s => s.IsRunning).Sum(s => s.Duration.TotalMinutes)
        };
    }

    public async Task<List<EquipmentPoint>> GetEquipmentDataAsync(ApplicationDbContext db, int archiveItemId, int layer, long start, long end)
    {
        return await db.DataRows
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
    }

    public HourlyEquipmentDto[] SplitByHours(List<EquipmentPoint> raw)
    {
        var perHour = HourlyEquipmentDto.GetEmptyHourlyData();
        for (int i = 0; i < raw.Count - 1; i++)
        {
            var curr = raw[i];
            var next = raw[i + 1];
            var segStart = curr.Timestamp;
            var segEnd = next.Timestamp;
            var isRun = curr.IsRunning;

            while (segStart < segEnd)
            {
                var hourStart = new DateTimeOffset(
                    segStart.Year, segStart.Month, segStart.Day,
                    segStart.Hour, 0, 0, TimeSpan.Zero);
                var hourEnd = hourStart.AddHours(1);
                var sliceEnd = segEnd < hourEnd ? segEnd : hourEnd;
                var minutes = (sliceEnd - segStart).TotalMinutes;

                if (isRun)
                    perHour[segStart.Hour].RunMinutes += minutes;
                else
                    perHour[segStart.Hour].IdleMinutes += minutes;

                segStart = sliceEnd;
            }
        }
        return perHour;
    }
}

public interface IEquipmentService
{
    EquipmentTimeResultDto CalculateTimeMetrics(List<EquipmentPoint> rawList, DateTime day);
    Task<List<EquipmentPoint>> GetEquipmentDataAsync(ApplicationDbContext db, int archiveItemId, int layer, long start, long end);
    HourlyEquipmentDto[] SplitByHours(List<EquipmentPoint> raw);
}
