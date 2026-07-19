import { api } from '../utils/api.js';
import { navigate } from '../utils/router.js';

export function renderTienda(container) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface">
      <header class="bg-primary text-white p-4">
        <h1 class="text-xl font-bold">Mi Tienda</h1>
      </header>
      
      <main class="p-4">
        <div id="tienda-content">
          <p class="text-center text-primary-light py-8">Cargando...</p>
        </div>
      </main>

      <nav class="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 p-2">
        <div class="flex justify-around">
          <a href="/pedidos" data-link class="flex flex-col items-center p-2 text-primary-light">
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
          <a href="/tienda" data-link class="flex flex-col items-center p-2 text-accent">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
            </svg>
            <span class="text-xs">Tienda</span>
          </a>
        </div>
      </nav>
    </div>
  `;

  loadTienda(container.querySelector('#tienda-content'));
}

async function loadTienda(container) {
  try {
    const tiendas = await api.tiendas.getAll();
    if (tiendas.length === 0) {
      container.innerHTML = `
        <div class="text-center py-8">
          <p class="text-primary-light mb-4">No hay tiendas registradas</p>
          <button id="btn-crear" class="btn-primary">Crear mi Tienda</button>
        </div>
      `;
      container.querySelector('#btn-crear').addEventListener('click', () => showCrearTienda(container));
      return;
    }

    container.innerHTML = `
      <div class="space-y-3">
        ${tiendas.map(t => `
          <div class="card cursor-pointer hover:shadow-md transition-shadow" data-tienda-id="${t.id}">
            <h3 class="font-semibold text-primary">${t.nombre}</h3>
            ${t.direccion ? `<p class="text-sm text-primary-light">${t.direccion}</p>` : ''}
            ${t.telefono ? `<p class="text-sm text-primary-light">${t.telefono}</p>` : ''}
          </div>
        `).join('')}
      </div>
    `;
  } catch (error) {
    container.innerHTML = `<p class="text-center text-red-500 py-8">Error al cargar tiendas</p>`;
  }
}

function showCrearTienda(container) {
  container.innerHTML = `
    <div class="card">
      <h2 class="text-lg font-semibold text-primary mb-4">Crear Tienda</h2>
      <form id="form-tienda" class="space-y-4">
        <input type="text" id="nombre" placeholder="Nombre de la tienda" class="input-field" required />
        <input type="text" id="direccion" placeholder="Dirección (opcional)" class="input-field" />
        <input type="tel" id="telefono" placeholder="Teléfono (opcional)" class="input-field" />
        <button type="submit" class="btn-primary w-full">Crear</button>
      </form>
    </div>
  `;

  container.querySelector('#form-tienda').addEventListener('submit', async (e) => {
    e.preventDefault();
    const data = {
      nombre: container.querySelector('#nombre').value,
      direccion: container.querySelector('#direccion').value || null,
      telefono: container.querySelector('#telefono').value || null
    };
    await api.tiendas.create(data);
    loadTienda(container);
  });
}
