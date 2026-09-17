import { api } from '../utils/api.js';
import { navigate } from '../utils/router.js';
import { setAuth } from '../utils/auth.js';

export function renderLogin(container, params) {
  container.innerHTML = `
    <div class="min-h-screen flex flex-col items-center justify-center p-8 bg-surface">
      <div class="w-full max-w-sm animate-fade-in">
        <div class="text-center mb-8">
          <div class="w-16 h-16 rounded-[20px] bg-primary mx-auto mb-4 flex items-center justify-center">
            <i data-lucide="lock" class="w-8 h-8 text-accent"></i>
          </div>
          <h1 class="text-2xl font-bold text-primary">Bienvenido</h1>
          <p class="text-[0.9375rem] text-text-secondary mt-1">Inicia sesión en tu tienda</p>
        </div>
        <div class="card-elevated">
          <form id="form-login" class="space-y-3">
            <input type="email" id="email" placeholder="Email" class="input-field" required />
            <input type="password" id="password" placeholder="Contraseña" class="input-field" required />
            <div id="error-msg" class="text-error text-sm hidden text-center py-2"></div>
            <button type="submit" class="btn-primary w-full mt-1">
              <i data-lucide="log-in" class="w-5 h-5"></i>
              Entrar
            </button>
          </form>
        </div>
        <p class="text-center mt-5">
          <a href="/" data-link class="text-sm text-text-secondary hover:text-primary transition">Volver al inicio</a>
        </p>
      </div>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  container.querySelector('#form-login').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errorMsg = container.querySelector('#error-msg');
    errorMsg.classList.add('hidden');

    try {
      const result = await api.auth.login({
        email: container.querySelector('#email').value,
        password: container.querySelector('#password').value,
        tiendaId: 0
      });

      if (result) {
        setAuth(result);
        navigate('/mi-tienda');
      } else {
        errorMsg.textContent = 'Credenciales inválidas';
        errorMsg.classList.remove('hidden');
      }
    } catch (error) {
      errorMsg.textContent = 'Credenciales inválidas';
      errorMsg.classList.remove('hidden');
    }
  });
}
