import { api } from '../utils/api.js';
import { getAuth, clearAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';
import { horaEnTienda, fechaHoraEnTienda } from '../utils/fechaTienda.js';

const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const ESTADO_BADGE = {
  Recibido: 'badge-info',
  Confirmado: 'badge-warning',
  EnPreparacion: 'badge-warning',
  Preparando: 'badge-warning',
  Listo: 'badge-success',
  Entregado: 'badge-info',
  Cancelado: 'badge-error'
};

const ESTADO_SIGUIENTE = {
  Recibido: 'Confirmado',
  Confirmado: 'EnPreparacion',
  EnPreparacion: 'Listo',
  Preparando: 'Listo',
  Listo: 'Entregado'
};

function renderAdminNav(active) {
  const items = [
    { href: '/pedidos', icon: 'clipboard-list', label: 'Pedidos', id: 'pedidos' },
    { href: '/menu', icon: 'utensils', label: 'Menú', id: 'menu' },
    { href: '/mi-tienda', icon: 'store', label: 'Tienda', id: 'tienda' },
  ];
  return `
    <div class="bottom-nav">
      <div class="nav-items">
        ${items.map(i => `
          <a href="${i.href}" data-link class="nav-item ${i.id === active ? 'active' : ''}">
            <i data-lucide="${i.icon}"></i>
            <span>${i.label}</span>
          </a>
        `).join('')}
      </div>
    </div>
  `;
}

export function renderPedidos(container, params) {
  const auth = getAuth();
  if (!auth) {
    navigate('/login');
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen bg-surface page-padding-bottom">
      <div class="header-primary flex justify-between items-center">
        <div>
          <p class="text-accent text-[0.8125rem] font-medium">Mi Tienda</p>
          <h1 class="text-xl font-bold mt-0.5">Pedidos</h1>
        </div>
        <button id="btn-logout" class="btn-icon bg-white/10 text-white/80 hover:bg-white/20">
          <i data-lucide="log-out" class="w-5 h-5"></i>
        </button>
      </div>
      <main class="page-padding page-padding-bottom" style="margin-top:-8px">
        <div class="flex gap-2 mb-5 overflow-x-auto pb-1 -mx-1 px-1">
          <button class="chip active" data-estado="">Todos</button>
          <button class="chip" data-estado="Recibido">Recibidos</button>
          <button class="chip" data-estado="Confirmado">Confirmados</button>
          <button class="chip" data-estado="EnPreparacion">En Preparación</button>
          <button class="chip" data-estado="Listo">Listos</button>
          <button class="chip" data-estado="Entregado">Entregados</button>
          <button class="chip" data-estado="Cancelado">Cancelados</button>
        </div>
        <label class="block text-sm text-primary mb-4">Fecha de servicio (vacío = todas, incluidos pedidos anteriores)
          <input id="filtro-fecha" type="date" class="input-field">
        </label>
        <div id="pedidos-list" class="space-y-3">
          <div class="space-y-3">
            <div class="skeleton h-32 w-full"></div>
            <div class="skeleton h-32 w-full"></div>
          </div>
        </div>
      </main>
      ${renderAdminNav('pedidos')}
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  container.querySelector('#btn-logout').addEventListener('click', () => {
    clearAuth();
    navigate('/');
  });

  let filtroActual = '';
  const filterBtns = container.querySelectorAll('.chip');
  filterBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      filterBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      filtroActual = btn.dataset.estado;
      loadPedidos(container.querySelector('#pedidos-list'), filtroActual, container.querySelector('#filtro-fecha').value);
    });
  });

  container.querySelector('#filtro-fecha').addEventListener('change', () => loadPedidos(container.querySelector('#pedidos-list'), filtroActual, container.querySelector('#filtro-fecha').value));
  loadPedidos(container.querySelector('#pedidos-list'), filtroActual);
}

async function loadPedidos(container, estado, fechaServicio = '') {
  try {
    const [pedidos, tienda] = await Promise.all([
      api.pedidos.getMisPedidos(estado || undefined, fechaServicio || undefined),
      api.tiendas.getMiTienda()
    ]);
    const zonaHoraria = tienda.operacion.zonaHoraria;

    if (pedidos.length === 0) {
      container.innerHTML = `
        <div class="empty-state">
          <div class="empty-state-icon"><i data-lucide="inbox"></i></div>
          <h3>Sin pedidos</h3>
          <p>${estado ? 'No hay pedidos con este filtro' : 'Cuando llegue un pedido, aparecerá aquí'}</p>
        </div>
      `;
      if (window.lucide) lucide.createIcons();
      return;
    }

    container.innerHTML = pedidos.map((p, i) => `
      <div class="card animate-fade-in" style="animation-delay:${i * 40}ms" data-id="${p.id}">
        <div class="flex justify-between items-start mb-3">
          <div class="flex items-center gap-2">
            <span class="font-bold text-primary text-[0.9375rem]">#${p.id}</span>
            ${p.clienteNombre ? `<span class="text-text-secondary text-sm">${p.clienteNombre}</span>` : ''}
          </div>
          <span class="badge ${ESTADO_BADGE[p.estado] || 'badge-info'}">${p.estado}</span>
        </div>
        <div class="flex flex-wrap gap-1.5 mb-3">
          <p class="text-sm text-primary w-full">${p.fechaServicio ? `Servicio ${p.fechaServicio} · ${p.modalidad === 'Programado' ? 'Programado' : 'Al momento'} · ${p.entrega === 'Domicilio' ? 'Domicilio' : 'Recoger'}` : 'Pedido anterior sin fecha de servicio registrada'}</p>
          ${p.ventanaSolicitada ? `<p class="text-sm text-text-secondary w-full">Solicitado: ${p.ventanaSolicitada}</p>` : ''}
          ${p.direccionEntrega ? `<p class="text-sm text-text-secondary w-full">Dirección: ${escapeHtml(p.direccionEntrega)}</p>` : ''}
          ${p.horarioConfirmado ? `<p class="text-sm text-primary w-full">Confirmado o estimado: ${escapeHtml(p.horarioConfirmado)}</p>` : ''}
          ${p.items.map(i => `
            <span class="chip text-[0.75rem] py-1 px-2.5" style="cursor:default">${i.productoNombre} ×${i.cantidad}</span>
          `).join('')}
        </div>
        <div class="flex justify-between items-center mb-3">
          <span class="font-bold text-accent text-lg">$${p.total.toFixed(2)}</span>
          <span class="text-text-secondary text-[0.8125rem] flex items-center gap-1">
            <i data-lucide="clock" class="w-3.5 h-3.5"></i>
            ${p.formaPago} · ${horaEnTienda(p.creadoEn, zonaHoraria)}
          </span>
        </div>
        ${p.estado !== 'Cancelado' && p.estado !== 'Entregado' ? `<div class="flex gap-2 mb-3"><input class="input-field min-w-0 flex-1 horario-confirmado" data-id="${p.id}" placeholder="Hora estimada o confirmada" value="${escapeHtml(p.horarioConfirmado || '')}"><button class="btn-ghost btn-horario shrink-0" data-id="${p.id}">Guardar</button></div>` : ''}
        <div class="flex flex-wrap gap-2">
          ${ESTADO_SIGUIENTE[p.estado] ? `
            <button class="btn-accent flex-1 text-sm btn-avanzar" data-id="${p.id}" data-estado="${ESTADO_SIGUIENTE[p.estado]}">
              <i data-lucide="arrow-right" class="w-4 h-4"></i> ${ESTADO_SIGUIENTE[p.estado]}
            </button>
          ` : ''}
          ${p.estado === 'Confirmado' || p.estado === 'EnPreparacion' ? `
            <button class="btn-success flex-1 text-sm btn-entregar" data-id="${p.id}">Entregar ahora</button>
          ` : ''}
          ${p.estado !== 'Cancelado' && p.estado !== 'Entregado' ? `
            <button class="btn-danger text-sm btn-cancelar-pedido" data-id="${p.id}">
              <i data-lucide="x" class="w-4 h-4"></i>
            </button>
          ` : ''}
          ${p.estadoPago === 'Pendiente' ? `
            <button class="btn-success text-sm btn-pagar" data-id="${p.id}">
              <i data-lucide="check-circle" class="w-4 h-4"></i> Pagado
            </button>
          ` : `
            <span class="badge badge-success flex items-center gap-1"><i data-lucide="check" class="w-3 h-3"></i> Pagado</span>
          `}
        </div>
        ${p.historialEstados?.length ? `<details class="mt-3 text-xs text-text-secondary"><summary>Historial de estados</summary>${p.historialEstados.map(h => `<p>${h.anterior || 'Inicio'} → ${h.nuevo} · ${fechaHoraEnTienda(h.fechaUtc, zonaHoraria)}</p>`).join('')}</details>` : ''}
      </div>
    `).join('');

    if (window.lucide) lucide.createIcons();

    container.querySelectorAll('.btn-avanzar').forEach(btn => {
      btn.addEventListener('click', async () => {
        await api.pedidos.updateEstado(btn.dataset.id, btn.dataset.estado);
        loadPedidos(container, estado, fechaServicio);
      });
    });

    container.querySelectorAll('.btn-cancelar-pedido').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (confirm('¿Cancelar este pedido?')) {
          await api.pedidos.updateEstado(btn.dataset.id, 'Cancelado');
          loadPedidos(container, estado, fechaServicio);
        }
      });
    });
    container.querySelectorAll('.btn-entregar').forEach(btn => btn.addEventListener('click', async () => {
      if (!confirm('¿Confirmas que el pedido ya fue entregado?')) return;
      try { await api.pedidos.updateEstado(btn.dataset.id, 'Entregado'); loadPedidos(container, estado, fechaServicio); }
      catch (err) { alert(err.message); }
    }));

    container.querySelectorAll('.btn-pagar').forEach(btn => {
      btn.addEventListener('click', async () => {
        await api.pedidos.updatePago(btn.dataset.id, 'Pagado');
        loadPedidos(container, estado, fechaServicio);
      });
    });
    container.querySelectorAll('.btn-horario').forEach(btn => btn.addEventListener('click', async () => {
      try {
        const valor = container.querySelector(`.horario-confirmado[data-id="${btn.dataset.id}"]`).value.trim();
        await api.pedidos.confirmarHorario(btn.dataset.id, valor || null);
        loadPedidos(container, estado, fechaServicio);
      } catch (err) { alert(err.message); }
    }));
  } catch (error) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
        <h3>Error</h3>
        <p>No se pudieron cargar los pedidos</p>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }
}
