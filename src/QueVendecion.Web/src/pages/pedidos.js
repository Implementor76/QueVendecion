import { api } from '../utils/api.js';

export function renderPedidos(container) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface pb-20">
      <header class="bg-primary text-white p-4">
        <h1 class="text-xl font-bold">Pedidos</h1>
      </header>
      
      <main class="p-4">
        <div class="flex gap-2 mb-4 overflow-x-auto">
          <button class="filter-btn btn-primary text-sm px-3 py-1" data-estado="">Todos</button>
          <button class="filter-btn btn-accent text-sm px-3 py-1" data-estado="Recibido">Recibidos</button>
          <button class="filter-btn btn-accent text-sm px-3 py-1" data-estado="EnPreparacion">En Preparación</button>
          <button class="filter-btn btn-accent text-sm px-3 py-1" data-estado="Listo">Listos</button>
        </div>

        <div id="pedidos-list" class="space-y-3">
          <p class="text-center text-primary-light py-8">Selecciona una tienda primero</p>
        </div>
      </main>

      <nav class="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 p-2">
        <div class="flex justify-around">
          <a href="/pedidos" data-link class="flex flex-col items-center p-2 text-accent">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/>
            </svg>
            <span class="text-xs">Pedidos</span>
          </a>
          <a href="/menu" data-link class="flex flex-col items-center p-2 text-primary-light">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/>
            </svg>
            <span class="text-xs">Menú</span>
          </a>
          <a href="/tienda" data-link class="flex flex-col items-center p-2 text-primary-light">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
            </svg>
            <span class="text-xs">Tienda</span>
          </a>
        </div>
      </nav>
    </div>
  `;

  const filterBtns = container.querySelectorAll('.filter-btn');
  filterBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      filterBtns.forEach(b => b.className = 'filter-btn btn-accent text-sm px-3 py-1');
      btn.className = 'filter-btn btn-primary text-sm px-3 py-1';
    });
  });
}
