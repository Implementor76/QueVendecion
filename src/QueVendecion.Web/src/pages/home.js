import { navigate } from '../utils/router.js';

export function renderHome(container) {
  container.innerHTML = `
    <div class="min-h-screen flex flex-col items-center justify-center p-6">
      <div class="text-center max-w-md">
        <h1 class="text-3xl font-bold text-primary mb-2">QuéVendecion</h1>
        <p class="text-primary-light mb-8">Organiza tus pedidos fácilmente</p>
        
        <div class="space-y-4">
          <button id="btn-tienda" class="btn-primary w-full text-lg py-3">
            Entrar a mi Tienda
          </button>
          <button id="btn-ordenar" class="btn-accent w-full text-lg py-3">
            Hacer un Pedido
          </button>
        </div>
      </div>
    </div>
  `;

  container.querySelector('#btn-tienda').addEventListener('click', () => navigate('/tienda'));
  container.querySelector('#btn-ordenar').addEventListener('click', () => navigate('/pedidos'));
}
