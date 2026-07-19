import { api } from '../utils/api.js';
import { navigate } from '../utils/router.js';
import { setAuth } from '../utils/auth.js';

export function renderLogin(container) {
  container.innerHTML = `
    <div class="min-h-screen flex flex-col items-center justify-center p-6 bg-surface">
      <div class="w-full max-w-sm">
        <div class="text-center mb-8">
          <h1 class="text-3xl font-bold text-primary">QuéVendecion</h1>
          <p class="text-primary-light mt-2">Inicia sesión en tu tienda</p>
        </div>

        <div class="card">
          <form id="form-login" class="space-y-4">
            <div>
              <input type="email" id="email" placeholder="Email" class="input-field" required />
            </div>
            <div>
              <input type="password" id="password" placeholder="Contraseña" class="input-field" required />
            </div>
            <div>
              <input type="number" id="tiendaId" placeholder="ID de Tienda" class="input-field" required min="1" />
            </div>
            <div id="error-msg" class="text-red-500 text-sm hidden"></div>
            <button type="submit" class="btn-primary w-full">Entrar</button>
          </form>
        </div>

        <p class="text-center mt-4 text-sm text-primary-light">
          ¿No tienes cuenta? <a href="/registrar" data-link class="text-accent hover:underline">Regístrate</a>
        </p>
        <p class="text-center mt-2 text-sm text-primary-light">
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
        tiendaId: parseInt(container.querySelector('#tiendaId').value)
      });

      if (result) {
        setAuth(result);
        navigate('/tienda');
      } else {
        errorMsg.textContent = 'Credenciales inválidas';
        errorMsg.classList.remove('hidden');
      }
    } catch (error) {
      errorMsg.textContent = 'Error al conectar con el servidor';
      errorMsg.classList.remove('hidden');
    }
  });
}
