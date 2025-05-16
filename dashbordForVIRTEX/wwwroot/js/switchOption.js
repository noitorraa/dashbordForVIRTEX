// switchOption.js

// Уведомления
function showNotification(message, type = 'success') {
  const n = document.createElement('div');
  n.className = `notification ${type}`;
  n.textContent = message;
  Object.assign(n.style, {
    position: 'fixed',
    bottom: '20px',
    right: '20px',
    padding: '15px',
    background: type === 'success' ? '#4CAF50' : '#f44336',
    color: 'white',
    borderRadius: '5px',
    zIndex: '1000'
  });
  document.body.appendChild(n);
  setTimeout(() => n.remove(), 3000);
}

document.addEventListener('DOMContentLoaded', () => {
  const buttons = document.querySelectorAll('.report-buttons .btn');
  if (!buttons.length) return;

  // Функция переключения активной кнопки
  function setActive(btn) {
    buttons.forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
  }

  // Общая функция обновления
  async function updateForButton(btn) {
    const product      = btn.dataset.product;
    const productivity = btn.dataset.productivity;

    try {
      // Обновляем данные
      await window.updateAllData(product, productivity);
      // Перерисовываем графики
      await window.reloadAllCharts(product, productivity);
    } catch (err) {
      console.error('Ошибка обновления данных/графиков:', err);
    }
  }

  // Привязываем на каждую кнопку
  buttons.forEach(btn => {
    btn.addEventListener('click', async () => {
      setActive(btn);
      await updateForButton(btn);
    });
  });

  // Первичный вызов для первой кнопки
  const first = buttons[0];
  setActive(first);
  updateForButton(first);
});