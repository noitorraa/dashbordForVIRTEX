namespace dashbordForVIRTEX.DTOs;

public class DailyOeeItemDto { public DateTime Date { get; set; } public double Oee { get; set; } public decimal ProductCount { get; set; } }
public class ReportDataDto
{
    public string EquipmentName { get; set; }
    public string Period { get; set; }
    public DateTime ReportDate { get; set; }
    public List<HourlyProductionReportItem> HourlyProductionData { get; set; }
    public List<DailyOeeItemDto> DailyOeeSeries { get; set; }
    
    // Только для суточного отчета
    public DailyMetricsDto DailyMetrics { get; set; }
}

public class DailyMetricsDto
{
    public double TotalTime { get; set; }
    public double RunTime { get; set; }
    public double IdleTime { get; set; }
    public double ProductCount { get; set; }
    public double Productivity { get; set; }
    public double OEE { get; set; }
    public double Availability { get; set; }
    public double Performance { get; set; }
}