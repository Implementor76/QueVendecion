import { api } from '../utils/api.js';

export function renderMenu(container) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface pb-20">
      <header class="bg-primary text-white p-4">
        <h1 class="text-xl font-bold">Menú</h1>
      </header>
      
      <main class="p-4">
        <div id="menu-list" class="space-y-3">
          <p class="text-center text-primary-light py-8">Cargando productos...</p>
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
          <a href="/menu" data-link class="flex flex-col items-center p-2 text-accent">
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

  loadProductos(container.querySelector('#menu-list'));
}

async function loadProductos(container) {
  try {
    const tiendas = await api.tiendas.getAll();
    if (tiendas.length === 0) {
      container.innerHTML = `<p class="text-center text-primary-light py-8">No hay tiendas configuradas</p>`;
      return;
    }

    const productos = await api.productos.getByTienda(tiendas[0].id);
    
    if (productos.length === 0) {
      container.innerHTML = `
        <div class="text-center py-8">
          <p class="text-primary-light mb-4">No hay productos en el menú</p>
          <button id="btn-agregar" class="btn-primary">Agregar Producto</button>
        </div>
      `;
      container.querySelector('#btn-agregar').addEventListener('click', () => showAgregarProducto(container, tiendas[0].id));
      return;
    }

    container.innerHTML = `
      <div class="space-y-3">
        ${productos.map(p => `
          <div class="card flex justify-between items-center">
            <div>
              <h3 class="font-semibold text-primary">${p.nombre}</h3>
              ${p.descripcion ? `<p class="text-sm text-primary-light">${p.descripcion}</p>` : ''}
            </div>
            <span class="text-accent font-bold">$${p.precio.toFixed(2)}</span>
          </div>
        `).join('')}
      </div>
    `;
  } catch (error) {
    container.innerHTML = `<p class="text-center text-red-500 py-8">Error al cargar productos</p>`;
  }
}

function showAgregarProducto(container, tiendaId) {
  container.innerHTML = `
    <div class="card">
      <h2 class="text-lg font-semibold text-primary mb-4">Agregar Producto</h2>
      <form id="form-producto" class="space-y-4">
        <input type="text" id="nombre" placeholder="Nombre del producto" class="input-field" required />
        <input type="text" id="descripcion" placeholder="Descripción (opcional)" class="input-field" />
        <input type="number" id="precio" placeholder="Precio" step="0.01" min="0" class="input-field" required />
        <button type="submit" class="btn-primary w-full">Agregar</button>
      </form>
    </div>
  `;

  container.querySelector('#form-producto').addEventListener('submit', async (e) => {
    e.preventDefault();
    const data = {
      nombre: container.querySelector('#nombre').value,
      descripcion: container.querySelector('#descripcion').value || null,
      precio: parseFloat(container.querySelector('#precio').value),
      disponible: true
    };
    await api.productos.create(tiendaId, data);
    loadProductos(container);
  });
}
