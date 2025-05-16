using System.ComponentModel.DataAnnotations.Schema;

namespace dashbordForVIRTEX.DTOs;

public class EquipmentTimeResultDto
{
    [Column("total_span")] // Используется при работе с БД
    public double TotalMinutes { get; set; }

    [Column("run_time")] // Используется при работе с БД
    public double RunMinutes { get; set; }

    [Column("idle_time")] // Используется при работе с БД
    public double IdleMinutes { get; set; }
}