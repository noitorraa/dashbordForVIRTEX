namespace dashbordForVIRTEX.DTOs;
public class HourlyEquipmentDto
{
    public int Hour { get; set; }          // Час дня (0–23)
    public double RunMinutes { get; set; } // Время работы в минутах
    public double IdleMinutes { get; set; } // Время простоя в минутах

    // Возвращает 24 пустые записи для заполнения данными
    public static HourlyEquipmentDto[] GetEmptyHourlyData()
    {
        var result = new HourlyEquipmentDto[24];
        for (int h = 0; h < 24; h++)
            result[h] = new HourlyEquipmentDto { Hour = h };
        return result;
    }
}