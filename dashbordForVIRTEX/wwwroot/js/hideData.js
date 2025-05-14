document.addEventListener('DOMContentLoaded', () => {
  // Находим все кнопки-переключатели
  document.querySelectorAll('.toggle-password').forEach(btn => {
    btn.addEventListener('click', () => {
      // находим соседнее поле ввода
      const input = btn.parentElement.querySelector('input');
      const icon  = btn.querySelector('i');

      // Переключаем тип поля
      if (input.type === 'password') {
        input.type = 'text';                  // делаем текст видимым :contentReference[oaicite:5]{index=5}
        icon.classList.remove('fa-eye-slash');
        icon.classList.add('fa-eye');
      } else {
        input.type = 'password';              // возвращаем скрытие
        icon.classList.remove('fa-eye');
        icon.classList.add('fa-eye-slash');
      }
    });
  });
});