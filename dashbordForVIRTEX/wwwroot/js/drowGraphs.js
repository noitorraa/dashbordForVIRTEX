// drowGraphs.js
// ------------------------------------------------------------------
// Глобальные ссылки на графики
let hourlyEquipmentChart = null;
let weeklyOeeChart = null;
let hourlyProductionChart = null;

// Утилита для fetch + JSON
async function fetchJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    console.error(`Ошибка загрузки ${url}:`, res.statusText);
    return [];
  }
  return res.json();
}

// Уничтожаем все текущие графики и очищаем канвасы
function destroyCharts() {
  [ 
    {chart: hourlyEquipmentChart, canvasId: 'hourly-equipment-chart'},
    {chart: weeklyOeeChart,     canvasId: 'weekly-performance-chart'},
    {chart: hourlyProductionChart, canvasId: 'hourly-production-chart'}
  ].forEach(({chart, canvasId}) => {
    // если экземпляр существует, уничтожаем
    if (chart) {
      chart.destroy();
      // очищаем содержимое холста
      const cvs = document.getElementById(canvasId);
      const ctx = cvs.getContext('2d');
      ctx.clearRect(0, 0, cvs.width, cvs.height);
    }
  });
  // обнуляем переменные
  hourlyEquipmentChart = weeklyOeeChart = hourlyProductionChart = null;
}

// Рисуем почасовую работу оборудования
async function drawHourlyEquipment(productItemId) {
  const data = await fetchJson(`/Home/GetHourlyEquipmentData?archiveItemId=${productItemId}`);
  const ctx = document.getElementById('hourly-equipment-chart').getContext('2d');
  return new Chart(ctx, {
    type: 'bar',
    data: {
      labels: data.map(x => `${x.hour}:00`),
      datasets: [
        { label: 'Работа (мин)', data: data.map(x => +x.runMinutes.toFixed(1)) },
        { label: 'Простой (мин)', data: data.map(x => +x.idleMinutes.toFixed(1)) }
      ]
    },
    options: { scales: { y: { beginAtZero: true, max: 60 } } }
  });
}

// Рисуем недельный OEE
async function drawWeeklyOee(productItemId, productivityItemId) {
  const data = await fetchJson(
    `/Home/GetWeeklyOee?archiveItemId=${productItemId}&productItemId=${productItemId}&productivityItemId=${productivityItemId}`
  );
  const ctx = document.getElementById('weekly-performance-chart').getContext('2d');
  return new Chart(ctx, {
    type: 'line',
    data: {
      labels: data.map(x => x.date),
      datasets: [{
        label: 'OEE (%)',
        data: data.map(x => +(x.oee * 100).toFixed(1)),
        fill: false,
        tension: 0.3
      }]
    },
    options: { scales: { y: { beginAtZero: true, max: 100 } } }
  });
}

// Рисуем почасовой выпуск продукции
async function drawHourlyProduction(productItemId) {
  const data = await fetchJson(`/Home/GetHourlyProductionData?productItemId=${productItemId}`);
  const ctx = document.getElementById('hourly-production-chart').getContext('2d');
  return new Chart(ctx, {
    type: 'bar',
    data: {
      labels: data.map(x => `${x.hour}:00`),
      datasets: [{ label: 'Выпуск (шт)', data: data.map(x => x.count) }]
    },
    options: { scales: { y: { beginAtZero: true } } }
  });
}

// Создаём графики по очереди с паузой (для плавности)
async function createChartsWithDelay(productItemId, productivityItemId) {
  hourlyEquipmentChart = await drawHourlyEquipment(productItemId);
  await new Promise(r => setTimeout(r, 50));
  weeklyOeeChart       = await drawWeeklyOee(productItemId, productivityItemId);
  await new Promise(r => setTimeout(r, 50));
  hourlyProductionChart = await drawHourlyProduction(productItemId);
}

// Основная функция для перезагрузки всех трёх графиков
async function reloadAllCharts(productItemId, productivityItemId) {
  destroyCharts();
  // чуть даём браузеру очистить DOM
  await new Promise(r => setTimeout(r, 50));
  await createChartsWithDelay(productItemId, productivityItemId);
}

// Экспортим в глобальную область
window.reloadAllCharts = reloadAllCharts;
