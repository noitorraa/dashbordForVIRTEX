// wwwroot/js/dashboard.js

const MAX_PRODUCTIVITY = 15000; // Замените на реальную максимальную производительность вашего оборудования (шт/ч)

let equipmentData = {};
let productionData = {};

async function updateEquipmentData() {
    try {
        const response = await fetch('/Home/GetEquipmentTimeData');
        if (!response.ok) throw new Error(response.statusText);
        equipmentData = await response.json();

        document.getElementById('totalTime').textContent = `${Math.round(equipmentData.totalMinutes)} мин`;
        document.getElementById('runTime').textContent = `${Math.round(equipmentData.runMinutes)} мин`;
        document.getElementById('idleTime').textContent = `${Math.round(equipmentData.idleMinutes)} мин`;
    } catch (err) {
        console.error('Ошибка загрузки данных оборудования:', err);
    }
}

async function updateProductionData() {
    try {
        const response = await fetch('/Home/GetProductionData');
        if (!response.ok) throw new Error(response.statusText);
        productionData = await response.json();

        document.getElementById('productCount').textContent = `${Math.round(productionData.productCount)} шт`;
        document.getElementById('productivity').textContent = `${Math.round(productionData.productivity)} шт/ч`;
    } catch (err) {
        console.error('Ошибка загрузки данных продукции:', err);
    }
}

function calculateOEE() {
    if (!equipmentData || !productionData) return;

    const { totalMinutes, runMinutes } = equipmentData;
    const { productCount, productivity } = productionData;

    if (!totalMinutes || !runMinutes) {
        console.warn('Недостаточно данных для расчета');
        return;
    }

    const availability = runMinutes / totalMinutes;

    const performance = MAX_PRODUCTIVITY > 0
        ? productivity / MAX_PRODUCTIVITY
        : 0;

    // OEE (без Quality)
    const oee = availability * performance;

    document.getElementById('availability').textContent = `${(availability * 100).toFixed(1)}%`;
    document.getElementById('performance').textContent  = `${(performance  * 100).toFixed(1)}%`;
    document.getElementById('oee').textContent          = `${(oee          * 100).toFixed(1)}%`;
}


async function updateAllData() {
    await updateEquipmentData(); // Сначала обновляем оборудование
    await updateProductionData(); // Затем продукцию
    calculateOEE();
}

// Инициализация
document.addEventListener('DOMContentLoaded', updateAllData);
setInterval(updateAllData, 30_000);