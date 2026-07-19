import { api } from '../utils/api.js';
import { getAuth, clearAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';

export function renderTienda(container, params) {
  const auth = getAuth();
  if (!auth) {
    navigate('/login');
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen bg-surface pb-20">
      <header class="bg-primary text-white p-4 flex justify-between items-center">
        <h1 class="text-xl font-bold">Mi Tienda</h1>
        <button id="btn-logout" class="text-sm text-accent hover:underline">Salir</button>
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
          <a href="/mi-tienda" data-link class="flex flex-col items-center p-2 text-accent">
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

  loadTienda(container.querySelector('#tienda-content'));
}

async function loadTienda(container) {
  try {
    const tienda = await api.tiendas.getMiTienda();
    container.innerHTML = `
      <div class="space-y-4">
        <div class="card">
          <div class="flex justify-between items-start mb-3">
            <div>
              <h2 class="text-lg font-bold text-primary">${tienda.nombre}</h2>
              <p class="text-accent font-semibold">/${tienda.slug}</p>
            </div>
            <button id="btn-editar" class="text-sm text-primary-light hover:underline">Editar</button>
          </div>
          ${tienda.direccion ? `<p class="text-sm text-primary-light">📍 ${tienda.direccion}</p>` : ''}
          ${tienda.telefono ? `<p class="text-sm text-primary-light">📞 ${tienda.telefono}</p>` : ''}
        </div>

        <div class="card">
          <h3 class="font-semibold text-primary mb-2">Link de tu tienda</h3>
          <div class="flex items-center gap-2">
            <code class="bg-surface text-primary text-sm px-3 py-2 rounded flex-1 overflow-x-auto">/tienda/${tienda.slug}</code>
            <button id="btn-copiar" class="btn-accent text-sm px-3 py-2">Copiar</button>
          </div>
        </div>

        <div class="card">
          <h3 class="font-semibold text-primary mb-2">Acciones</h3>
          <div class="space-y-2">
            <a href="/menu" data-link class="btn-primary block text-center">Gestionar Menú</a>
            <a href="/pedidos" data-link class="btn-accent block text-center">Ver Pedidos</a>
            <button id="btn-cambiar-pass" class="block w-full text-center text-sm text-primary-light hover:underline py-2">Cambiar contraseña</button>
          </div>
        </div>

        <div id="editar-tienda" class="hidden">
          <div class="card">
            <h3 class="font-semibold text-primary mb-4">Editar Tienda</h3>
            <form id="form-editar" class="space-y-3">
              <input type="text" id="edit-nombre" value="${tienda.nombre}" class="input-field" />
              <input type="text" id="edit-slug" value="${tienda.slug}" class="input-field" />
              <input type="text" id="edit-direccion" value="${tienda.direccion || ''}" class="input-field" />
              <input type="tel" id="edit-telefono" value="${tienda.telefono || ''}" class="input-field" />
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar-edit" class="btn-primary flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">Guardar</button>
              </div>
            </form>
          </div>
        </div>

        <div id="cambiar-pass-section" class="hidden">
          <div class="card">
            <h3 class="font-semibold text-primary mb-4">Cambiar Contraseña</h3>
            <form id="form-pass" class="space-y-3">
              <input type="password" id="pass-actual" placeholder="Contraseña actual" class="input-field" required />
              <input type="password" id="pass-nueva" placeholder="Nueva contraseña" class="input-field" required />
              <input type="password" id="pass-confirmar" placeholder="Confirmar nueva contraseña" class="input-field" required />
              <div id="pass-msg" class="text-sm hidden"></div>
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar-pass" class="btn-primary flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">Actualizar</button>
              </div>
            </form>
          </div>
        </div>
      </div>
    `;

    container.querySelector('#btn-editar').addEventListener('click', () => {
      container.querySelector('#editar-tienda').classList.toggle('hidden');
    });
    container.querySelector('#btn-cancelar-edit').addEventListener('click', () => {
      container.querySelector('#editar-tienda').classList.add('hidden');
    });
    container.querySelector('#form-editar').addEventListener('submit', async (e) => {
      e.preventDefault();
      await api.tiendas.update(tienda.id, {
        nombre: container.querySelector('#edit-nombre').value,
        slug: container.querySelector('#edit-slug').value,
        direccion: container.querySelector('#edit-direccion').value || null,
        telefono: container.querySelector('#edit-telefono').value || null
      });
      loadTienda(container);
    });

    container.querySelector('#btn-copiar').addEventListener('click', () => {
      const url = `${window.location.origin}/tienda/${tienda.slug}`;
      navigator.clipboard.writeText(url);
      container.querySelector('#btn-copiar').textContent = '¡Copiado!';
      setTimeout(() => container.querySelector('#btn-copiar').textContent = 'Copiar', 2000);
    });

    container.querySelector('#btn-cambiar-pass').addEventListener('click', () => {
      container.querySelector('#cambiar-pass-section').classList.toggle('hidden');
    });
    container.querySelector('#btn-cancelar-pass').addEventListener('click', () => {
      container.querySelector('#cambiar-pass-section').classList.add('hidden');
    });
    container.querySelector('#form-pass').addEventListener('submit', async (e) => {
      e.preventDefault();
      const msg = container.querySelector('#pass-msg');
      const nueva = container.querySelector('#pass-nueva').value;
      const confirmar = container.querySelector('#pass-confirmar').value;

      if (nueva !== confirmar) {
        msg.textContent = 'Las contraseñas no coinciden';
        msg.className = 'text-sm text-red-500';
        return;
      }

      try {
        await api.auth.cambiarPassword({
          passwordActual: container.querySelector('#pass-actual').value,
          passwordNueva: nueva
        });
        msg.textContent = '¡Contraseña actualizada!';
        msg.className = 'text-sm text-green-600';
        container.querySelector('#form-pass').reset();
      } catch (err) {
        msg.textContent = 'La contraseña actual es incorrecta';
        msg.className = 'text-sm text-red-500';
      }
    });
  } catch (error) {
    container.innerHTML = `<p class="text-center text-red-500 py-8">Error al cargar tienda</p>`;
  }
}
