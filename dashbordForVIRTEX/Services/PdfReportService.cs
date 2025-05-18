using dashbordForVIRTEX.Controllers;
using dashbordForVIRTEX.DTOs;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using static dashbordForVIRTEX.DTOs.DailyOeeItemDto;

namespace dashbordForVIRTEX.Services
{
    public class PdfReportService : IPdfReportService
    {
        public byte[] GeneratePdf(ReportDataDto report)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            var graphics = XGraphics.FromPdfPage(page);
            var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 12, XFontStyle.Bold);
            var bodyFont = new XFont("Arial", 10);
            var brush = XBrushes.Black;
            
            double yPosition = 40;
            const double leftMargin = 50;
            const double rightMargin = 50;
            const double columnWidth = 150;

            // Заголовок отчета
            graphics.DrawString($"ОТЧЕТ VIRTEXFOOD - {report.EquipmentName}", 
                            titleFont, brush, leftMargin, yPosition);
            yPosition += 30;
            graphics.DrawString($"Период: {GetPeriodDisplayName(report.Period)}", 
                            headerFont, brush, leftMargin, yPosition);
            yPosition += 25;
            graphics.DrawString($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", 
                            bodyFont, brush, leftMargin, yPosition);
            yPosition += 40;

            // Секция для суточного отчета
            if (report.Period == "day" && report.DailyMetrics != null)
            {
                DrawDailyMetrics(graphics, report.DailyMetrics, ref yPosition);
                yPosition += 20;
                DrawHourlyProductionChart(graphics, report.HourlyProductionData, ref yPosition);
            }

            // Секция для недельных/месячных отчетов
            if (report.Period != "day" && report.DailyOeeSeries?.Any() == true)
            {
                DrawPeriodTable(graphics, report.DailyOeeSeries, ref yPosition);
            }

            // Подпись
            graphics.DrawString("Система мониторинга производства VIRTEXFOOD", 
                            bodyFont, XBrushes.Gray, leftMargin, yPosition + 20);

            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }

        private void DrawDailyMetrics(XGraphics graphics, DailyMetricsDto metrics, ref double y)
        {
            const double leftMargin = 50;
            var font = new XFont("Arial", 12);

            // Рамка
            graphics.DrawRectangle(XBrushes.LightGray, new XRect(leftMargin - 10, y, 500, 190));
            y += 15;

            // Заголовок секции
            graphics.DrawString("ПОКАЗАТЕЛИ ЗА СУТКИ", font, XBrushes.Black, leftMargin, y);
            y += 25;

            // Колонки
            DrawMetricColumn(graphics, "Оборудование включено:", $"{metrics.TotalTime} мин", leftMargin, ref y);
            DrawMetricColumn(graphics, "Время работы:", $"{metrics.RunTime} мин", leftMargin, ref y);
            DrawMetricColumn(graphics, "Простой:", $"{metrics.IdleTime} мин", leftMargin, ref y);
            DrawMetricColumn(graphics, "Продукция:", $"{metrics.ProductCount} шт", leftMargin, ref y);
            DrawMetricColumn(graphics, "Производительность:", $"{metrics.Productivity} шт/ч", leftMargin, ref y);
            DrawMetricColumn(graphics, "OEE:", $"{metrics.OEE:F1}%", leftMargin, ref y);
            DrawMetricColumn(graphics, "Готовность:", $"{metrics.Availability:F1}%", leftMargin, ref y);
            DrawMetricColumn(graphics, "Эффективность:", $"{metrics.Performance:F1}%", leftMargin, ref y);
            
            y += 20;
        }

        private void DrawMetricColumn(XGraphics graphics, string label, string value, double x, ref double y)
        {
            var labelFont = new XFont("Arial", 11, XFontStyle.Bold);
            var valueFont = new XFont("Arial", 11);
            
            graphics.DrawString(label, labelFont, XBrushes.Black, x, y);
            graphics.DrawString(value, valueFont, XBrushes.Black, x + 200, y);
            y += 20;
        }

        private void DrawPeriodTable(XGraphics graphics, List<DailyOeeItemDto> data, ref double y)
        {
            const double leftMargin = 50;
            var headerFont = new XFont("Arial", 12, XFontStyle.Bold);
            var rowFont = new XFont("Arial", 11);
            
            // Заголовок таблицы
            graphics.DrawString("ДАТА", headerFont, XBrushes.Black, leftMargin, y);
            graphics.DrawString("ПРОДУКЦИЯ", headerFont, XBrushes.Black, leftMargin + 150, y);
            graphics.DrawString("OEE", headerFont, XBrushes.Black, leftMargin + 300, y);
            y += 20;
            
            // Разделительная линия
            graphics.DrawLine(XPens.Black, leftMargin, y, leftMargin + 400, y);
            y += 15;

            // Строки данных
            foreach (var item in data)
            {
                graphics.DrawString(item.Date.ToString("dd.MM.yyyy"), rowFont, XBrushes.Black, leftMargin, y);
                graphics.DrawString(item.ProductCount.ToString("N0"), rowFont, XBrushes.Black, leftMargin + 150, y);
                graphics.DrawString((item.Oee * 100).ToString("N1") + "%", rowFont, XBrushes.Black, leftMargin + 300, y);
                y += 20;
            }

            // Итоговая строка
            graphics.DrawLine(XPens.Black, leftMargin, y, leftMargin + 400, y);
            y += 15;
            graphics.DrawString("ВСЕГО:", headerFont, XBrushes.Black, leftMargin, y);
            graphics.DrawString(data.Sum(d => d.ProductCount).ToString("N0"), 
                            headerFont, XBrushes.Black, leftMargin + 150, y);
            y += 30;
        }

        private void DrawHourlyProductionChart(XGraphics graphics, List<HourlyProductionReportItem> data, ref double y)
        {
            if (data == null || data.Count == 0) return;

            const int chartHeight = 200;
            const int chartWidth = 500;
            var maxValue = data.Max(d => d.Count);

            // Заголовок
            graphics.DrawString("ПОЧАСОВОЙ ВЫПУСК ПРОДУКЦИИ", 
                            new XFont("Arial", 12), 
                            XBrushes.Black, 50, y);
            y += 25;

            // Оси
            graphics.DrawRectangle(XPens.Black, 50, y, chartWidth, chartHeight);
            
            // Подписи часов
            for (int i = 0; i < data.Count; i++)
            {
                var xPos = 50 + (i * (chartWidth / data.Count));
                graphics.DrawString($"{data[i].Hour:00}:00", 
                                new XFont("Arial", 8), 
                                XBrushes.Black, 
                                xPos, 
                                y + chartHeight + 10);
            }

            for (int i = 0; i < data.Count; i++)
            {
                double currentCount = Convert.ToDouble(data[i].Count);
                double maxValueDbl = Convert.ToDouble(maxValue);
                
                double barHeight = (currentCount / maxValueDbl) * chartHeight;

                graphics.DrawRectangle(
                    XBrushes.SteelBlue,
                    50 + (i * (chartWidth / data.Count)) + 2, // X
                    y + chartHeight - barHeight,               // Y (исправлено!)
                    (chartWidth / data.Count) - 4,             // Width
                    barHeight                                  // Height
                );
            }

            y += chartHeight + 40;
        }

        private string GetPeriodDisplayName(string period) => period switch
        {
            "day" => "СУТКИ",
            "week" => "НЕДЕЛЯ",
            "month" => "МЕСЯЦ",
            _ => "НЕИЗВЕСТНЫЙ ПЕРИОД"
        };
    }
}