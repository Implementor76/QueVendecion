import { navigate } from '../utils/router.js';
import { isLoggedIn } from '../utils/auth.js';

export function renderHome(container, params) {
  if (isLoggedIn()) {
    navigate('/mi-tienda');
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen flex flex-col items-center justify-center p-6 bg-surface">
      <div class="text-center max-w-md">
        <h1 class="text-3xl font-bold text-primary mb-2">QuéVendecion</h1>
        <p class="text-primary-light mb-8">Organiza tus pedidos fácilmente</p>
        
        <div class="space-y-4">
          <button id="btn-admin" class="btn-primary w-full text-lg py-3">
            Soy dueño de tienda
          </button>
          <button id="btn-cliente" class="btn-accent w-full text-lg py-3">
            Quiero hacer un pedido
          </button>
        </div>
      </div>
    </div>
  `;

  container.querySelector('#btn-admin').addEventListener('click', () => navigate('/login'));
  container.querySelector('#btn-cliente').addEventListener('click', () => {
    const slug = prompt('¿Cuál es el nombre de la tienda? (ej: maylin)');
    if (slug) navigate(`/tienda/${slug}`);
  });
}
