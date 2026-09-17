import { api } from '../utils/api.js';
import { navigate } from '../utils/router.js';

const SA_KEY = 'qv_superadmin';

function getSuperAdmin() {
  const data = localStorage.getItem(SA_KEY);
  return data ? JSON.parse(data) : null;
}

function setSuperAdmin(data) {
  localStorage.setItem(SA_KEY, JSON.stringify(data));
}

function clearSuperAdmin() {
  localStorage.removeItem(SA_KEY);
}

export function renderSuperAdmin(container, params) {
  const sa = getSuperAdmin();
  if (sa && sa.rol === 'SuperAdmin') {
    renderDashboard(container, sa);
    return;
  }
  renderLogin(container);
}

function renderLogin(container) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface flex flex-col items-center justify-center p-8">
      <div class="w-full max-w-sm animate-fade-in">
        <div class="text-center mb-8">
          <div class="w-16 h-16 rounded-[20px] bg-primary mx-auto mb-4 flex items-center justify-center">
            <i data-lucide="shield" class="w-8 h-8 text-accent"></i>
          </div>
          <h1 class="text-2xl font-bold text-primary">SuperAdmin</h1>
          <p class="text-sm text-text-secondary mt-1">Gestión de tiendas</p>
        </div>
        <div class="card-elevated">
          <div id="sa-msg" class="text-sm text-error mb-3 hidden text-center"></div>
          <form id="sa-form" class="space-y-3">
            <input type="email" id="sa-email" placeholder="Email" class="input-field" value="marco.cisneros@gmail.com" required />
            <input type="password" id="sa-password" placeholder="Password" class="input-field" required />
            <button type="submit" class="btn-primary w-full">
              <i data-lucide="log-in" class="w-5 h-5"></i> Entrar
            </button>
          </form>
        </div>
      </div>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  container.querySelector('#sa-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msg = container.querySelector('#sa-msg');
    msg.classList.add('hidden');

    try {
      const res = await api.auth.login({
        email: container.querySelector('#sa-email').value,
        password: container.querySelector('#sa-password').value
      });

      if (res.rol !== 'SuperAdmin') {
        msg.textContent = 'Este usuario no es SuperAdmin';
        msg.classList.remove('hidden');
        return;
      }

      setSuperAdmin({ token: res.token, nombre: res.nombre, email: res.email, rol: res.rol });
      renderDashboard(container, getSuperAdmin());
    } catch (err) {
      msg.textContent = 'Email o password incorrectos';
      msg.classList.remove('hidden');
    }
  });
}

function renderDashboard(container, sa) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface page-padding-bottom">
      <div class="header-primary flex justify-between items-center">
        <div>
          <p class="text-accent text-[0.8125rem] font-medium">SuperAdmin</p>
          <h1 class="text-xl font-bold mt-0.5">Gestión de Tiendas</h1>
        </div>
        <button id="btn-logout" class="btn-icon bg-white/10 text-white/80 hover:bg-white/20">
          <i data-lucide="log-out" class="w-5 h-5"></i>
        </button>
      </div>
      <main class="page-padding" style="margin-top:-8px">
        <div class="mb-4">
          <button id="btn-nueva-tienda" class="btn-accent w-full">
            <i data-lucide="plus" class="w-5 h-5"></i> Nueva Tienda
          </button>
        </div>
        <div id="form-tienda" class="hidden mb-4">
          <div class="card-elevated animate-scale-in">
            <h3 class="font-semibold text-primary mb-3 flex items-center gap-2">
              <i data-lucide="store" class="w-5 h-5 text-accent"></i>
              Crear Tienda
            </h3>
            <form id="form-crear" class="space-y-3">
              <input type="text" id="t-nombre" placeholder="Nombre de la tienda" class="input-field" required />
              <input type="text" id="t-slug" placeholder="Slug (ej: mi-tienda)" class="input-field" required />
              <input type="text" id="t-direccion" placeholder="Dirección (opcional)" class="input-field" />
              <input type="text" id="t-telefono" placeholder="Teléfono (opcional)" class="input-field" />
              <div class="border-t pt-3 mt-3" style="border-color:#eeece8">
                <p class="text-sm font-semibold text-primary mb-2">Administrador de la tienda</p>
              </div>
              <input type="text" id="t-admin-nombre" placeholder="Nombre del admin" class="input-field" required />
              <input type="email" id="t-admin-email" placeholder="Email del admin" class="input-field" required />
              <input type="password" id="t-admin-password" placeholder="Password del admin" class="input-field" required />
              <div id="t-msg" class="text-sm hidden text-center"></div>
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar" class="btn-ghost flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">
                  <i data-lucide="plus" class="w-4 h-4"></i> Crear
                </button>
              </div>
            </form>
          </div>
        </div>
        <div id="tiendas-list" class="space-y-3">
          <div class="space-y-3">
            <div class="skeleton h-20 w-full"></div>
            <div class="skeleton h-20 w-full"></div>
          </div>
        </div>
      </main>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  container.querySelector('#btn-logout').addEventListener('click', () => {
    clearSuperAdmin();
    renderLogin(container);
  });

  const formSection = container.querySelector('#form-tienda');
  container.querySelector('#btn-nueva-tienda').addEventListener('click', () => {
    formSection.classList.toggle('hidden');
    if (!formSection.classList.contains('hidden')) {
      container.querySelector('#t-nombre').focus();
    }
  });
  container.querySelector('#btn-cancelar').addEventListener('click', () => {
    formSection.classList.add('hidden');
    container.querySelector('#form-crear').reset();
    container.querySelector('#t-msg').classList.add('hidden');
  });

  container.querySelector('#t-nombre').addEventListener('input', (e) => {
    const slug = e.target.value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    container.querySelector('#t-slug').value = slug;
  });

  container.querySelector('#form-crear').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msg = container.querySelector('#t-msg');
    try {
      const res = await apiWithAuth('/superadmin/tienda', {
        method: 'POST',
        body: JSON.stringify({
          nombreTienda: container.querySelector('#t-nombre').value,
          slug: container.querySelector('#t-slug').value,
          direccion: container.querySelector('#t-direccion').value || null,
          telefono: container.querySelector('#t-telefono').value || null,
          adminNombre: container.querySelector('#t-admin-nombre').value,
          adminEmail: container.querySelector('#t-admin-email').value,
          adminPassword: container.querySelector('#t-admin-password').value
        })
      });
      msg.textContent = `¡Tienda "${res.nombre}" creada! Admin: ${res.admin.email}`;
      msg.className = 'text-sm text-success text-center';
      container.querySelector('#form-crear').reset();
      loadTiendas(container.querySelector('#tiendas-list'), sa);
    } catch (err) {
      msg.textContent = 'Error al crear la tienda';
      msg.className = 'text-sm text-error text-center';
    }
  });

  loadTiendas(container.querySelector('#tiendas-list'), sa);
}

async function loadTiendas(container, sa) {
  try {
    const tiendas = await apiWithAuth('/superadmin/tiendas');

    if (tiendas.length === 0) {
      container.innerHTML = `
        <div class="empty-state">
          <div class="empty-state-icon"><i data-lucide="store-x"></i></div>
          <h3>Sin tiendas</h3>
          <p>Crea tu primera tienda para empezar</p>
        </div>
      `;
      if (window.lucide) lucide.createIcons();
      return;
    }

    container.innerHTML = `
      <p class="section-title">Tiendas (${tiendas.length})</p>
      <div class="space-y-3">
        ${tiendas.map((t, i) => `
          <div class="card animate-fade-in" style="animation-delay:${i * 40}ms">
            <div class="flex justify-between items-start">
              <div class="flex-1 mr-3 cursor-pointer btn-ver-tienda" data-id="${t.id}">
                <h3 class="font-semibold text-primary text-[0.9375rem]">${t.nombre}</h3>
                <p class="text-sm text-text-secondary mt-0.5">/${t.slug}</p>
                ${t.direccion ? `<p class="text-sm text-text-secondary flex items-center gap-1 mt-1"><i data-lucide="map-pin" class="w-3 h-3"></i> ${t.direccion}</p>` : ''}
                ${t.telefono ? `<p class="text-sm text-text-secondary flex items-center gap-1 mt-0.5"><i data-lucide="phone" class="w-3 h-3"></i> ${t.telefono}</p>` : ''}
              </div>
              <div class="text-right flex flex-col items-end gap-1">
                <span class="badge badge-info">${t.usuarios} usuario${t.usuarios !== 1 ? 's' : ''}</span>
                <span class="badge badge-warning">${t.pedidos} pedido${t.pedidos !== 1 ? 's' : ''}</span>
                <p class="text-xs text-text-secondary mt-1">${new Date(t.creadaEn).toLocaleDateString('es-MX')}</p>
              </div>
            </div>
          </div>
        `).join('')}
      </div>
    `;

    if (window.lucide) lucide.createIcons();

    container.querySelectorAll('.btn-ver-tienda').forEach(btn => {
      btn.addEventListener('click', () => {
        renderTiendaDetalle(container, sa, parseInt(btn.dataset.id));
      });
    });
  } catch (error) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
        <h3>Error</h3>
        <p>No se pudieron cargar las tiendas</p>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }
}

async function renderTiendaDetalle(container, sa, tiendaId) {
  try {
    const tienda = await apiWithAuth(`/superadmin/tiendas/${tiendaId}`);

    container.innerHTML = `
      <div class="min-h-screen bg-surface page-padding-bottom">
        <div class="header-primary flex justify-between items-center">
          <div class="flex items-center gap-3">
            <button id="btn-volver" class="btn-icon bg-white/10 text-white/80 hover:bg-white/20">
              <i data-lucide="arrow-left" class="w-5 h-5"></i>
            </button>
            <div>
              <p class="text-accent text-[0.8125rem] font-medium">Tienda</p>
              <h1 class="text-xl font-bold mt-0.5">${tienda.nombre}</h1>
            </div>
          </div>
          <button id="btn-logout" class="btn-icon bg-white/10 text-white/80 hover:bg-white/20">
            <i data-lucide="log-out" class="w-5 h-5"></i>
          </button>
        </div>
        <main class="page-padding" style="margin-top:-8px">
          <div class="card-elevated mb-4 animate-fade-in">
            <div class="space-y-2">
              <div class="flex justify-between">
                <span class="text-sm text-text-secondary">Slug</span>
                <span class="text-sm font-medium text-primary">/${tienda.slug}</span>
              </div>
              ${tienda.direccion ? `
              <div class="flex justify-between">
                <span class="text-sm text-text-secondary">Dirección</span>
                <span class="text-sm font-medium text-primary">${tienda.direccion}</span>
              </div>` : ''}
              ${tienda.telefono ? `
              <div class="flex justify-between">
                <span class="text-sm text-text-secondary">Teléfono</span>
                <span class="text-sm font-medium text-primary">${tienda.telefono}</span>
              </div>` : ''}
              <div class="flex justify-between">
                <span class="text-sm text-text-secondary">Creada</span>
                <span class="text-sm font-medium text-primary">${new Date(tienda.creadaEn).toLocaleDateString('es-MX')}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-sm text-text-secondary">Pedidos</span>
                <span class="badge badge-warning">${tienda.pedidos}</span>
              </div>
            </div>
          </div>

          ${tienda.usuarios.length > 0 ? `
          <div class="mb-4">
            <p class="section-title">Usuarios</p>
            <div class="space-y-2">
              ${tienda.usuarios.map(u => `
                <div class="card flex justify-between items-center">
                  <div>
                    <p class="font-medium text-primary text-sm">${u.nombre}</p>
                    <p class="text-xs text-text-secondary">${u.email}</p>
                  </div>
                  <span class="badge ${u.rol === 'Admin' ? 'badge-accent' : 'badge-info'}">${u.rol}</span>
                </div>
              `).join('')}
            </div>
          </div>
          ` : ''}

          <button id="btn-reset-pedidos" class="btn-danger w-full">
            <i data-lucide="trash-2" class="w-5 h-5"></i> Eliminar todos los pedidos
          </button>
          <div id="reset-msg" class="text-sm hidden text-center mt-3"></div>
        </main>
      </div>
    `;

    if (window.lucide) lucide.createIcons();

    container.querySelector('#btn-volver').addEventListener('click', () => {
      renderDashboard(container, sa);
    });

    container.querySelector('#btn-logout').addEventListener('click', () => {
      clearSuperAdmin();
      renderLogin(container);
    });

    container.querySelector('#btn-reset-pedidos').addEventListener('click', async () => {
      const msg = container.querySelector('#reset-msg');
      if (!confirm(`¿Eliminar TODOS los pedidos de "${tienda.nombre}"?\n\nEsta acción no se puede deshacer.`)) return;

      try {
        const res = await apiWithAuth(`/superadmin/tiendas/${tiendaId}/pedidos`, { method: 'DELETE' });
        msg.textContent = `Se eliminaron ${res.eliminados} pedido(s)`;
        msg.className = 'text-sm text-success text-center';
        renderTiendaDetalle(container, sa, tiendaId);
      } catch (err) {
        msg.textContent = 'Error al eliminar pedidos';
        msg.className = 'text-sm text-error text-center';
      }
    });
  } catch (error) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
        <h3>Error</h3>
        <p>No se pudo cargar la tienda</p>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }
}

async function apiWithAuth(url, options = {}) {
  const sa = getSuperAdmin();
  const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${sa.token}`,
    ...options.headers
  };

  const response = await fetch(`/api${url}`, { ...options, headers });

  if (!response.ok) {
    if (response.status === 401) {
      clearSuperAdmin();
      window.location.reload();
    }
    throw new Error(`Error: ${response.status}`);
  }

  return response.json();
}
