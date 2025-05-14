document.addEventListener('DOMContentLoaded', () => {
  const STORAGE_KEY   = 'scadaLink';
  const input         = document.getElementById('scadaLinkInput');
  const saveBtn       = document.getElementById('saveScadaLinkBtn');
  const returnBtn     = document.getElementById('returnToScadaBtn');
  const settingsModal = document.getElementById('settingsModal');

  // Получаем анти‑фальсификационный токен из Razor
  function getAntiForgeryToken() {
    const el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  // Устанавливаем URL из localStorage или из БД
  async function initScadaLink() {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      applyLink(saved);
    } else {
      // если нет в localStorage — подгружаем с сервера
      try {
        const resp = await fetch('/Home/GetLatestScadaString');
        if (!resp.ok) throw new Error(`Ошибка ${resp.status}`);
        const data = await resp.json();
        if (data.scadaString) {
          localStorage.setItem(STORAGE_KEY, data.scadaString);
          applyLink(data.scadaString);
        }
      } catch (e) {
        console.error('Не удалось получить SCADA-строку из БД:', e);
      }
    }
  }

  // Применяем ссылку к input и к кнопке возврата
  function applyLink(url) {
    input.value = url;
    returnBtn.dataset.url = url;
  }

  // При открытии модалки подтягиваем актуальную ссылку из localStorage
  settingsModal.addEventListener('show.bs.modal', () => {
    const link = localStorage.getItem(STORAGE_KEY);
    if (link) input.value = link;
  });

  // Обработка клика «Сохранить»
  saveBtn.addEventListener('click', async () => {
    const url = input.value.trim();
    if (!url) {
      alert('Введите корректный URL');
      return;
    }

    // сохраняем локально
    localStorage.setItem(STORAGE_KEY, url);

    // отправляем на сервер
    try {
      const token = getAntiForgeryToken();
      const resp = await fetch('/Home/SaveScadaString', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': token
        },
        body: JSON.stringify({ scadaString: url })
      });
      if (!resp.ok) {
        const err = await resp.text();
        throw new Error(err);
      }
      const result = await resp.json();
      alert('Ссылка сохранена в базе (id=' + result.id + ')');
      applyLink(url);
      bootstrap.Modal.getInstance(settingsModal).hide();
    } catch (e) {
      console.error(e);
      alert('Ошибка при сохранении: ' + e.message);
    }
  });

  // Клик «Вернуться в SCADA»
  returnBtn.addEventListener('click', e => {
    e.preventDefault();
    const url = returnBtn.dataset.url;
    if (url) {
      window.location.href = url;
    } else {
      alert('Ссылка SCADA не задана');
    }
  });

  // Инициализация при загрузке
  initScadaLink();
});