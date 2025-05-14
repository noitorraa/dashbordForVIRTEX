async function updateEquipmentData() {
    try {
        const response = await fetch('/Home/GetEquipmentTimeData');
        const data = await response.json();

        const totalMin = Math.round(data.totalSpan?.minutes ?? 0);
        const runMin   = Math.round(data.runTime?.minutes   ?? 0);
        const idleMin  = Math.round(data.idleTime?.minutes  ?? 0);

        document.getElementById('totalTime').textContent = `${totalMin} мин`;
        document.getElementById('runTime').textContent   = `${runMin} мин`;
        document.getElementById('idleTime').textContent  = `${idleMin} мин`;

    } catch (error) {
        console.error('Ошибка загрузки данных оборудования:', error);
    }
}

// Обновление данных продукции
async function updateProductionData() {
    try {
        const response = await fetch('/Home/GetProductionData');
        const data = await response.json();
        
        document.getElementById('productCount').textContent = `${Math.round(data.productCount)} шт`;
        document.getElementById('productivity').textContent = `${Math.round(data.productivity)} шт/ч`;
    } catch (error) {
        console.error('Ошибка загрузки данных продукции:', error);
    }
}

// Обновление всех данных
function updateAllData() {
    updateEquipmentData();
    updateProductionData();
}

// Первоначальная загрузка
document.addEventListener('DOMContentLoaded', updateAllData);

// Обновление каждые 30 секунд
setInterval(updateAllData, 30000);