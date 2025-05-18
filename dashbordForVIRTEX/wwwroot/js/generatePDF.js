 // generatePDF
 document.querySelectorAll('.report-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('.report-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
      });
    });

    // Генерация и скачивание PDF
    document.getElementById('generateReportBtn').addEventListener('click', async () => {
      const period = document.getElementById('periodSelect').value;
      const activeBtn = document.querySelector('.report-btn.active');
      const productItemId = activeBtn.dataset.product;
      const productivityItemId = activeBtn.dataset.productivity;

      const response = await fetch(
        `/Home/GenerateReport?period=${period}&productItemId=${productItemId}&productivityItemId=${productivityItemId}`
      );
      if (!response.ok) {
        alert('Ошибка при формировании отчета');
        return;
      }
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report_${period}.pdf`;
      a.click();
      window.URL.revokeObjectURL(url);
    });
