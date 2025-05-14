document.addEventListener('DOMContentLoaded', () => {
  const saveBtn = document.getElementById('saveDbBtn');
  const testBtn = document.getElementById('testDbBtn');
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

  const buildConnectionString = () => {
    const dbType   = document.getElementById('dbType').value;
    const host     = document.getElementById('dbHost').value.trim();
    const port     = document.getElementById('dbPort').value.trim();
    const dbName   = document.getElementById('dbName').value.trim();
    const user     = document.getElementById('dbUser').value.trim();
    const password = document.getElementById('dbPassword').value.trim();

    if (!dbType || !host || !port || !dbName || !user || !password) {
      alert('Заполните все поля');
      return null;
    }

    switch (dbType) {
      case 'MySQL':
        return `mysql://${user}:${password}@${host}:${port}/${dbName}`;
      case 'PostgreSQL':
        return `postgres://${user}:${password}@${host}:${port}/${dbName}`;
      case 'SQL Server':
        return `mssql://${user}:${password}@${host}:${port}/${dbName}?encrypt=true`;
      default:
        alert('Выберите корректный тип СУБД');
        return null;
    }
  };

  saveBtn.addEventListener('click', () => {
    console.log("Кнопка 'Сохранить' нажата");
    const connectionString = buildConnectionString();
    if (!connectionString) return;

    fetch('/Home/SaveDbConfig', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': token
      },
      body: JSON.stringify({ connectionString })
    })
    .then(res => {
      if (res.ok) {
        alert('Строка подключения сохранена');
      } else {
        return res.text().then(t => Promise.reject(t));
      }
    })
    .catch(err => alert('Ошибка сохранения: ' + err));
  });

  testBtn.addEventListener('click', () => {
    console.log("Кнопка 'Тест подключения' нажата");
    const connectionString = buildConnectionString();
    if (!connectionString) return;

    fetch('/Home/TestDbConnection', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': token
      },
      body: JSON.stringify({ connectionString })
    })
    .then(async res => {
      if (res.ok) {
        const msg = await res.text();
        alert('Успешно: ' + msg);
      } else {
        const err = await res.text();
        alert('Ошибка соединения: ' + err);
      }
    })
    .catch(err => alert('Ошибка: ' + err));
  });
});
