async function fetchJson(url) {
  const res = await fetch(url);
  return res.ok ? res.json() : [];
}

// 3.1 Почасовой график работы оборудования
async function drawHourlyEquipment() {
  const data = await fetchJson('/Home/GetHourlyEquipmentData');
  const labels = data.map(x => `${x.hour}:00`);
  const run    = data.map(x => x.runMinutes.toFixed(1));
  const idle   = data.map(x => x.idleMinutes.toFixed(1));

  new Chart(document.getElementById('hourly-equipment-chart'), {
    type: 'bar',
    data: {
      labels,
      datasets: [
        { label: 'Работа (мин)', data: run  },
        { label: 'Простой (мин)', data: idle }
      ]
    },
    options: {
      scales: {
        y: { beginAtZero: true, max: 60 }
      }
    }
  });
}


// 3.2 Недельный OEE
async function drawWeeklyOee() {
  const data = await fetchJson('/Home/GetWeeklyOee');
  const labels = data.map(x=> x.date);
  const values = data.map(x=> (x.oee * 100).toFixed(1));

  new Chart(document.getElementById('weekly-performance-chart'), {
    type: 'line',
    data: {
      labels,
      datasets: [{ 
        label: 'OEE (%)',
        data: values,
        fill: false,
        tension: 0.3
      }]
    },
    options: {
      responsive: true,
      scales: { y: { beginAtZero: true, max: 100 } }
    }
  });
}

// 3.3 Почасовой выпуск продукции
async function drawHourlyProduction() {
  const data = await fetchJson('/Home/GetHourlyProductionData');
  const labels = data.map(x=> x.hour + ':00');
  const values = data.map(x=> x.count);

  new Chart(document.getElementById('hourly-production-chart'), {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        label: 'Выпуск (шт)',
        data: values,
        backgroundColor: 'rgba(0, 123, 255, 0.5)'
      }]
    },
    options: {
      responsive: true,
      scales: { y: { beginAtZero: true } }
    }
  });
}

// Инициализация всех графиков
document.addEventListener('DOMContentLoaded', () => {
  drawHourlyEquipment();
  drawWeeklyOee();
  drawHourlyProduction();
});
