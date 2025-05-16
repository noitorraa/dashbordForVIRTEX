
using dashbordForVIRTEX.DTOs;
using Microsoft.EntityFrameworkCore;

namespace dashbordForVIRTEX.Services;
public class ProductionService : IProductionService
{
    public async Task<ProductionDataDto> GetProductionDataAsync(
        ApplicationDbContext db,
        int productItemId,
        int productivityItemId,
        int layer,
        long start,
        long end)
    {
        var latestValues = await db.DataRows
            .Where(r => (r.archive_itemid == productItemId || r.archive_itemid == productivityItemId) &&
                       r.layer == layer &&
                       r.status_code == 0 &&
                       r.source_time >= start &&
                       r.source_time < end)
            .GroupBy(r => r.archive_itemid)
            .Select(g => new
            {
                ItemId = g.Key,
                LastValue = g
                    .OrderByDescending(r => r.source_time)
                    .Where(r => r.value > 0)
                    .Select(r => (decimal)r.value)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return new ProductionDataDto
        {
            ProductCount = latestValues
                .FirstOrDefault(x => x.ItemId == productItemId)?
                .LastValue ?? 0m,
            Productivity = (double)(latestValues
                .FirstOrDefault(x => x.ItemId == productivityItemId)?
                .LastValue ?? 0m)
        };
    }
}

public interface IProductionService
{
    Task<ProductionDataDto> GetProductionDataAsync(
        ApplicationDbContext db, 
        int productItemId, 
        int productivityItemId, 
        int layer, 
        long start, 
        long end);
}
