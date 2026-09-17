import { navigate } from '../utils/router.js';
import { isLoggedIn } from '../utils/auth.js';

export function renderHome(container, params) {
  if (isLoggedIn()) {
    navigate('/mi-tienda');
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen flex flex-col items-center justify-center p-8 bg-surface">
      <div class="text-center max-w-sm animate-fade-in">
        <div class="w-20 h-20 rounded-[24px] bg-primary mx-auto mb-6 flex items-center justify-center">
          <i data-lucide="store" class="w-10 h-10 text-accent"></i>
        </div>
        <h1 class="text-[2rem] font-bold text-primary mb-2 leading-tight">QuéVendecion</h1>
        <p class="text-[0.9375rem] text-text-secondary mb-10 leading-relaxed">Organiza tus pedidos<br>de forma simple y rápida</p>
        
        <div class="space-y-3">
          <button id="btn-admin" class="btn-primary w-full">
            <i data-lucide="lock" class="w-5 h-5"></i>
            Soy dueño de tienda
          </button>
          <button id="btn-cliente" class="btn-accent w-full">
            <i data-lucide="shopping-bag" class="w-5 h-5"></i>
            Quiero hacer un pedido
          </button>
          <button id="btn-superadmin" class="btn-ghost w-full text-sm mt-4">
            <i data-lucide="shield" class="w-4 h-4"></i>
            SuperAdmin
          </button>
        </div>
      </div>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  container.querySelector('#btn-admin').addEventListener('click', () => navigate('/login'));
  container.querySelector('#btn-cliente').addEventListener('click', () => {
    const slug = prompt('¿Cuál es el nombre de la tienda? (ej: maylin)');
    if (slug) navigate(`/tienda/${slug}`);
  });
  container.querySelector('#btn-superadmin').addEventListener('click', () => navigate('/superadmin'));
}
