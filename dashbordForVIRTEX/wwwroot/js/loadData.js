// loadData.js
const MAX_PRODUCTIVITY = 15000;
let equipmentData = null;
let productionData = null;

async function updateEquipmentData(productId) {
  const res = await fetch(`/Home/GetEquipmentTimeData?archiveItemId=${productId}`);
  equipmentData = res.ok ? await res.json() : {};
  document.getElementById('totalTime').textContent = `${Math.round(equipmentData.totalMinutes)} мин`;
  document.getElementById('runTime').textContent   = `${Math.round(equipmentData.runMinutes)} мин`;
  document.getElementById('idleTime').textContent  = `${Math.round(equipmentData.idleMinutes)} мин`;
}

async function updateProductionData(productId, productivityId) {
  const res = await fetch(
    `/Home/GetProductionData?productItemId=${productId}&productivityItemId=${productivityId}`
  );
  productionData = res.ok ? await res.json() : {};
  document.getElementById('productCount').textContent = `${Math.round(productionData.productCount)} шт`;
  document.getElementById('productivity').textContent = `${Math.round(productionData.productivity)} шт/ч`;
}

function calculateOEE() {
  if (!equipmentData || !productionData) return;
  const availability = equipmentData.runMinutes / equipmentData.totalMinutes;
  const performance = productionData.productivity / MAX_PRODUCTIVITY;
  const oee = availability * performance;
  document.getElementById('availability').textContent = `${(availability * 100).toFixed(1)}%`;
  document.getElementById('performance').textContent  = `${(performance * 100).toFixed(1)}%`;
  document.getElementById('oee').textContent          = `${(oee * 100).toFixed(1)}%`;
}

async function updateAllData(productId, productivityId) {
  await updateEquipmentData(productId);
  await updateProductionData(productId, productivityId);
  calculateOEE();
}

window.updateAllData = updateAllData;
