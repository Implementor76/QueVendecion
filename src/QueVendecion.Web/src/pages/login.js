import { api } from '../utils/api.js';
import { navigate } from '../utils/router.js';
import { setAuth } from '../utils/auth.js';

export function renderLogin(container, params) {
  container.innerHTML = `
    <div class="min-h-screen flex flex-col items-center justify-center p-6 bg-surface">
      <div class="w-full max-w-sm">
        <div class="text-center mb-8">
          <h1 class="text-3xl font-bold text-primary">QuéVendecion</h1>
          <p class="text-primary-light mt-2">Inicia sesión en tu tienda</p>
        </div>
        <div class="card">
          <form id="form-login" class="space-y-4">
            <input type="email" id="email" placeholder="Email" class="input-field" required />
            <input type="password" id="password" placeholder="Contraseña" class="input-field" required />
            <div id="error-msg" class="text-red-500 text-sm hidden"></div>
            <button type="submit" class="btn-primary w-full">Entrar</button>
          </form>
        </div>
        <p class="text-center mt-4 text-sm text-primary-light">
          <a href="/" data-link class="hover:underline">Volver al inicio</a>
        </p>
      </div>
    </div>
  `;

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
