import { api } from '../utils/api.js';
import { getAuth, clearAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';

const ESTADO_COLORS = {
  Recibido: 'bg-yellow-100 text-yellow-800',
  EnPreparacion: 'bg-blue-100 text-blue-800',
  Listo: 'bg-green-100 text-green-800',
  Entregado: 'bg-gray-100 text-gray-800',
  Cancelado: 'bg-red-100 text-red-800'
};

const ESTADO_SIGUIENTE = {
  Recibido: 'EnPreparacion',
  EnPreparacion: 'Listo',
  Listo: 'Entregado'
};

export function renderPedidos(container, params) {
  const auth = getAuth();
  if (!auth) {
    navigate('/login');
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen bg-surface pb-20">
      <header class="bg-primary text-white p-4 flex justify-between items-center">
        <h1 class="text-xl font-bold">Pedidos</h1>
        <button id="btn-logout" class="text-sm text-accent hover:underline">Salir</button>
      </header>
      <main class="p-4">
        <div class="flex gap-2 mb-4 overflow-x-auto">
          <button class="filter-btn btn-primary text-sm px-3 py-1 whitespace-nowrap" data-estado="">Todos</button>
          <button class="filter-btn btn-accent text-sm px-3 py-1 whitespace-nowrap" data-estado="Recibido">Recibidos</button>
          <button class="filter-btn btn-accent text-sm px-3 py-1 whitespace-nowrap" data-estado="EnPreparacion">En Preparación</button>
          <button class="filter-btn btn-accent text-sm px-3 py-1 whitespace-nowrap" data-estado="Listo">Listos</button>
        </div>
        <div id="pedidos-list" class="space-y-3">
          <p class="text-center text-primary-light py-8">Cargando pedidos...</p>
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
          <a href="/mi-tienda" data-link class="flex flex-col items-center p-2 text-primary-light">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
            </svg>
            <span class="text-xs">Tienda</span>
          </a>
        </div>
      </nav>
    </div>
  `;

  container.querySelector('#btn-logout').addEventListener('click', () => {
    clearAuth();
    navigate('/');
  });

  let filtroActual = '';
  const filterBtns = container.querySelectorAll('.filter-btn');
  filterBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      filterBtns.forEach(b => b.className = 'filter-btn btn-accent text-sm px-3 py-1 whitespace-nowrap');
      btn.className = 'filter-btn btn-primary text-sm px-3 py-1 whitespace-nowrap';
      filtroActual = btn.dataset.estado;
      loadPedidos(container.querySelector('#pedidos-list'), filtroActual);
    });
  });

  loadPedidos(container.querySelector('#pedidos-list'), filtroActual);
}

async function loadPedidos(container, estado) {
  try {
    const pedidos = await api.pedidos.getMisPedidos(estado || undefined);

    if (pedidos.length === 0) {
      container.innerHTML = `<p class="text-center text-primary-light py-8">No hay pedidos${estado ? ' con este filtro' : ''}</p>`;
      return;
    }

    container.innerHTML = pedidos.map(p => `
      <div class="card" data-id="${p.id}">
        <div class="flex justify-between items-start mb-2">
          <div>
            <span class="font-bold text-primary">#${p.id}</span>
            ${p.clienteNombre ? `<span class="text-primary-light ml-2">${p.clienteNombre}</span>` : ''}
          </div>
          <span class="text-xs px-2 py-1 rounded-full ${ESTADO_COLORS[p.estado] || 'bg-gray-100'}">${p.estado}</span>
        </div>
        <div class="text-sm text-primary-light mb-2">
          ${p.items.map(i => `${i.productoNombre} x${i.cantidad}`).join(', ')}
        </div>
        <div class="flex justify-between items-center text-sm">
          <span class="text-accent font-bold">$${p.total.toFixed(2)}</span>
          <span class="text-primary-light">${p.formaPago} · ${new Date(p.creadoEn).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' })}</span>
        </div>
        <div class="flex gap-2 mt-3">
          ${ESTADO_SIGUIENTE[p.estado] ? `
            <button class="btn-avanzar btn-accent flex-1 text-sm" data-id="${p.id}" data-estado="${ESTADO_SIGUIENTE[p.estado]}">
              → ${ESTADO_SIGUIENTE[p.estado]}
            </button>
          ` : ''}
          ${p.estado !== 'Cancelado' && p.estado !== 'Entregado' ? `
            <button class="btn-cancelar-pedido text-red-400 hover:text-red-600 text-sm px-3" data-id="${p.id}">Cancelar</button>
          ` : ''}
          ${p.estadoPago === 'Pendiente' ? `
            <button class="btn-pagar text-green-600 hover:text-green-800 text-sm px-3" data-id="${p.id}">Marcar Pagado</button>
          ` : `<span class="text-xs text-green-600">✓ Pagado</span>`}
        </div>
      </div>
    `).join('');

    container.querySelectorAll('.btn-avanzar').forEach(btn => {
      btn.addEventListener('click', async () => {
        await api.pedidos.updateEstado(btn.dataset.id, btn.dataset.estado);
        loadPedidos(container, estado);
      });
    });

    container.querySelectorAll('.btn-cancelar-pedido').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (confirm('¿Cancelar este pedido?')) {
          await api.pedidos.updateEstado(btn.dataset.id, 'Cancelado');
          loadPedidos(container, estado);
        }
      });
    });

    container.querySelectorAll('.btn-pagar').forEach(btn => {
      btn.addEventListener('click', async () => {
        await api.pedidos.updatePago(btn.dataset.id, 'Pagado');
        loadPedidos(container, estado);
      });
    });
  } catch (error) {
    container.innerHTML = `<p class="text-center text-red-500 py-8">Error al cargar pedidos</p>`;
  }
}
