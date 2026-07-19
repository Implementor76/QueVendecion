import { api } from '../utils/api.js';
import { getAuth, clearAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';

export function renderMenu(container, params) {
  const auth = getAuth();
  if (!auth) {
    navigate('/login');
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen bg-surface pb-20">
      <header class="bg-primary text-white p-4 flex justify-between items-center">
        <h1 class="text-xl font-bold">Mi Menú</h1>
        <button id="btn-logout" class="text-sm text-accent hover:underline">Salir</button>
      </header>
      <main class="p-4">
        <div id="menu-actions" class="mb-4">
          <button id="btn-agregar" class="btn-accent w-full">+ Agregar Producto</button>
        </div>
        <div id="form-agregar" class="hidden mb-4">
          <div class="card">
            <h3 class="font-semibold text-primary mb-3">Nuevo Producto</h3>
            <form id="form-producto" class="space-y-3">
              <input type="text" id="prod-nombre" placeholder="Nombre del producto" class="input-field" required />
              <input type="text" id="prod-descripcion" placeholder="Descripción (opcional)" class="input-field" />
              <input type="number" id="prod-precio" placeholder="Precio" step="0.01" min="0" class="input-field" required />
              <div id="prod-msg" class="text-sm hidden"></div>
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar" class="btn-primary flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">Agregar</button>
              </div>
            </form>
          </div>
        </div>
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

  const formSection = container.querySelector('#form-agregar');
  container.querySelector('#btn-agregar').addEventListener('click', () => {
    formSection.classList.toggle('hidden');
    if (!formSection.classList.contains('hidden')) {
      container.querySelector('#prod-nombre').focus();
    }
  });
  container.querySelector('#btn-cancelar').addEventListener('click', () => {
    formSection.classList.add('hidden');
    container.querySelector('#form-producto').reset();
    container.querySelector('#prod-msg').classList.add('hidden');
  });

  container.querySelector('#form-producto').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msg = container.querySelector('#prod-msg');
    try {
      await api.productos.create({
        nombre: container.querySelector('#prod-nombre').value,
        descripcion: container.querySelector('#prod-descripcion').value || null,
        precio: parseFloat(container.querySelector('#prod-precio').value),
        disponible: true
      });
      msg.textContent = '✓ Producto agregado. Agrega otro o cancela.';
      msg.className = 'text-sm text-green-600';
      container.querySelector('#prod-nombre').value = '';
      container.querySelector('#prod-descripcion').value = '';
      container.querySelector('#prod-precio').value = '';
      container.querySelector('#prod-nombre').focus();
      loadProductos(container.querySelector('#menu-list'));
    } catch (err) {
      msg.textContent = 'Error al agregar producto';
      msg.className = 'text-sm text-red-500';
    }
  });

  loadProductos(container.querySelector('#menu-list'));
}

async function loadProductos(container) {
  try {
    const productos = await api.productos.getByMiTienda();

    if (productos.length === 0) {
      container.innerHTML = `<p class="text-center text-primary-light py-4">Aún no tienes productos. ¡Agrega el primero!</p>`;
      return;
    }

    container.innerHTML = `
      <div class="space-y-3">
        ${productos.map(p => `
          <div class="card flex justify-between items-center" data-id="${p.id}">
            <div class="flex-1 mr-3">
              <h3 class="font-semibold text-primary">${p.nombre}</h3>
              ${p.descripcion ? `<p class="text-sm text-primary-light">${p.descripcion}</p>` : ''}
              <p class="text-accent font-bold mt-1">$${p.precio.toFixed(2)}</p>
            </div>
            <button class="btn-delete text-red-400 hover:text-red-600 text-sm" data-id="${p.id}">Eliminar</button>
          </div>
        `).join('')}
      </div>
    `;

    container.querySelectorAll('.btn-delete').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (confirm('¿Eliminar este producto?')) {
          await api.productos.delete(btn.dataset.id);
          loadProductos(container);
        }
      });
    });
  } catch (error) {
    container.innerHTML = `<p class="text-center text-red-500 py-8">Error al cargar productos</p>`;
  }
}
