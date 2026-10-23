import { api } from '../utils/api.js';
import { getClient, setClient, removeClient } from '../utils/clientAuth.js';
import { fechaHoraEnTienda } from '../utils/fechaTienda.js';

const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

export function renderTiendaCliente(container, params) {
  const slug = params?.slug;
  if (!slug) {
    container.innerHTML = `
      <div class="min-h-screen flex items-center justify-center p-8">
        <div class="empty-state">
          <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
          <h3>Tienda no encontrada</h3>
        </div>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen bg-surface">
      <div class="header-primary">
        <h1 class="text-xl font-bold animate-pulse">Cargando...</h1>
      </div>
      <main class="page-padding">
        <div class="space-y-3">
          <div class="skeleton h-20 w-full"></div>
          <div class="skeleton h-20 w-full"></div>
          <div class="skeleton h-20 w-full"></div>
        </div>
      </main>
    </div>
  `;

  loadTiendaCliente(container, slug);
}

async function loadTiendaCliente(container, slug) {
  try {
    const tienda = await api.public.getTienda(slug);
    const opciones = await api.public.getOpciones(slug);
    const cliente = getClient(slug);

    if (!cliente) {
      renderLogin(container, slug, tienda, opciones);
      return;
    }

    renderApp(container, slug, tienda, cliente, opciones);
  } catch (error) {
    container.innerHTML = `
      <div class="min-h-screen flex items-center justify-center p-8">
        <div class="empty-state animate-fade-in">
          <div class="empty-state-icon"><i data-lucide="store-x"></i></div>
          <h3>Tienda no encontrada</h3>
          <p>La tienda "${slug}" no existe o no está disponible.</p>
        </div>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }
}

function renderLogin(container, slug, tienda, opciones) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface flex flex-col items-center justify-center p-8">
      <div class="w-full max-w-sm animate-fade-in">
        <div class="text-center mb-8">
          <div class="w-16 h-16 rounded-[20px] bg-primary mx-auto mb-4 flex items-center justify-center">
            <i data-lucide="store" class="w-8 h-8 text-accent"></i>
          </div>
          <h1 class="text-2xl font-bold text-primary">${tienda.nombre}</h1>
          ${tienda.direccion ? `<p class="text-sm text-text-secondary mt-1 flex items-center justify-center gap-1"><i data-lucide="map-pin" class="w-3.5 h-3.5"></i> ${tienda.direccion}</p>` : ''}
        </div>
        <div class="card-elevated">
          <h2 class="text-lg font-bold text-primary mb-1">Bienvenido</h2>
          <p class="text-sm text-text-secondary mb-5">Ingresa tus datos para hacer pedidos</p>
          <div class="mb-3">
            <input type="text" id="login-nombre" placeholder="Tu nombre" class="input-field" />
          </div>
          <div class="mb-5">
            <input type="tel" id="login-telefono" placeholder="Tu teléfono" class="input-field" />
          </div>
          <button id="btn-login" class="btn-accent w-full">
            <i data-lucide="log-in" class="w-5 h-5"></i> Entrar
          </button>
        </div>
      </div>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  container.querySelector('#btn-login').addEventListener('click', async () => {
    const nombre = container.querySelector('#login-nombre').value.trim();
    const telefono = container.querySelector('#login-telefono').value.trim();

    if (!nombre) { alert('Ingresa tu nombre'); return; }
    if (!telefono) { alert('Ingresa tu teléfono'); return; }

    try {
      const cliente = await api.public.registrarCliente(slug, { nombre, telefono });
      setClient(slug, { id: cliente.id, nombre: cliente.nombre, telefono: cliente.telefono });
      renderApp(container, slug, tienda, cliente, opciones);
    } catch (error) {
      alert('Error al registrar. Intenta de nuevo.');
    }
  });
}
function renderApp(container, slug, tienda, cliente, opciones) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface" style="padding-bottom:5.5rem">
      <div class="header-primary flex justify-between items-center">
        <div>
          <h1 class="text-xl font-bold">${tienda.nombre}</h1>
          ${tienda.direccion ? `<p class="text-xs text-accent/80 mt-0.5 flex items-center gap-1"><i data-lucide="map-pin" class="w-3 h-3"></i> ${tienda.direccion}</p>` : ''}
        </div>
        <span class="badge badge-accent">${cliente.nombre}</span>
      </div>

      <main id="client-content" style="margin-top:-8px"></main>

      <div class="bottom-nav">
        <div class="nav-items">
          <button data-tab="menu" class="nav-item active">
            <i data-lucide="utensils"></i>
            <span>Menú</span>
          </button>
          <button data-tab="pedidos" class="nav-item">
            <i data-lucide="clipboard-list"></i>
            <span>Mis Pedidos</span>
          </button>
          <button data-tab="cuenta" class="nav-item">
            <i data-lucide="user"></i>
            <span>Mi Cuenta</span>
          </button>
        </div>
      </div>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  let currentTab = 'menu';
  let carrito = {};
  const seleccion = { modalidad: opciones.recepcionDisponible ? (opciones.alMomentoDisponible ? 'AlMomento' : (tienda.operacion.pedidosProgramados ? 'Programado' : null)) : null,
    entrega: tienda.operacion.recoleccion && tienda.operacion.domicilio ? '' : tienda.operacion.recoleccion ? 'Recoleccion' : 'Domicilio', fecha: '', ventana: '' };

  function switchTab(tab) {
    currentTab = tab;
    container.querySelectorAll('.nav-item').forEach(btn => {
      if (btn.dataset.tab === tab) {
        btn.classList.add('active');
      } else {
        btn.classList.remove('active');
      }
    });

    const content = container.querySelector('#client-content');
    if (tab === 'menu') renderMenuTab(content, slug, cliente, carrito, (c) => { carrito = c; }, tienda, opciones, seleccion);
    else if (tab === 'pedidos') renderPedidosTab(content, slug, cliente, tienda.operacion.zonaHoraria);
    else if (tab === 'cuenta') renderCuentaTab(container, slug, tienda, cliente);
  }

  container.querySelectorAll('.nav-item').forEach(btn => {
    btn.addEventListener('click', () => switchTab(btn.dataset.tab));
  });

  switchTab('menu');
}

async function renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion) {
  if (!seleccion.modalidad) {
    content.innerHTML = `<div class="page-padding"><div class="card-elevated"><h2 class="font-bold text-primary">Pedidos cerrados por ahora</h2><p>${escapeHtml(tienda.operacion.mensajePublico || 'Consulta el horario de atención de la tienda.')}</p></div></div>`;
    return;
  }
  content.innerHTML = `
    <div class="page-padding">
      <div class="space-y-3">
        <div class="skeleton h-24 w-full"></div>
        <div class="skeleton h-24 w-full"></div>
      </div>
    </div>
  `;

  const todos = await api.public.getMenu(slug);
  const necesitaFecha = todos.some(p => p.fechasServicio?.length);
  const fechaMenu = seleccion.modalidad === 'Programado' ? seleccion.fecha : opciones.hoy;
  const productos = necesitaFecha && !fechaMenu || !seleccion.entrega ? [] : await api.public.getMenu(slug, fechaMenu, seleccion.modalidad, seleccion.entrega);
  {
    const selector = `
      <div class="card-elevated mb-4 space-y-3">
        ${tienda.operacion.mensajePublico ? `<p class="text-primary font-medium">${escapeHtml(tienda.operacion.mensajePublico)}</p>` : '<p class="text-primary font-medium">Consulta las condiciones y el horario de entrega antes de pedir.</p>'}
        <p class="text-sm text-text-secondary">Elige cuándo deseas recibir tu pedido. Espera la confirmación de la tienda antes de recogerlo.</p>
        <label class="block text-sm">Forma de entrega<select id="entrega-catalogo" class="input-field"><option value="">Selecciona entrega</option>
          ${tienda.operacion.recoleccion ? '<option value="Recoleccion">Recoger</option>' : ''}
          ${tienda.operacion.domicilio ? '<option value="Domicilio">Domicilio</option>' : ''}
        </select></label>
        <label class="block text-sm">Tipo de pedido<select id="modalidad-pedido" class="input-field">
          ${opciones.alMomentoDisponible ? '<option value="AlMomento">Al momento</option>' : ''}
          ${tienda.operacion.pedidosProgramados ? '<option value="Programado">Programado</option>' : ''}
        </select></label>
        ${!opciones.alMomentoDisponible && tienda.operacion.pedidosAlMomento ? '<p class="text-sm text-text-secondary">Fuera del horario para pedidos al momento.</p>' : ''}
        ${seleccion.modalidad === 'Programado' ? `<label class="block text-sm">Fecha de servicio<select id="fecha-servicio" class="input-field"><option value="">Selecciona fecha</option>
          ${opciones.fechas.map(o => `<option value="${o.fecha}">${o.fecha}</option>`).join('')}
        </select></label>
        <label class="block text-sm">Ventana solicitada<select id="ventana-servicio" class="input-field"><option value="">Selecciona ventana</option>
          ${(opciones.fechas.find(o => o.fecha === seleccion.fecha)?.ventanas || []).map(v => `<option value="${v}">${v}</option>`).join('')}
        </select></label>` : ''}
        <p class="text-sm text-text-secondary">${!seleccion.entrega ? 'Selecciona recoger o domicilio para ver los precios y productos correspondientes.' : necesitaFecha && !fechaMenu ? 'Selecciona una fecha para ver los productos disponibles.' : `Productos para ${fechaMenu}.`}</p>
      </div>`;
    if (productos.length === 0) {
      content.innerHTML = `<div class="page-padding">${selector}<div class="empty-state"><h3>${!seleccion.entrega ? 'Selecciona la forma de entrega' : necesitaFecha && !fechaMenu ? 'Selecciona una fecha' : 'Sin productos para esta fecha'}</h3><p>${!seleccion.entrega ? 'Así verás los productos y precios correctos.' : necesitaFecha && !fechaMenu ? 'Elige una fecha de servicio para ver el catálogo.' : 'Elige otra fecha de servicio.'}</p></div></div>`;
      bindSeleccion();
      return;
    }

    content.innerHTML = `
      <div class="page-padding page-padding-bottom" style="padding-bottom:8rem">
        ${selector}
        <div class="space-y-3">
          ${productos.map((p, i) => {
            const agotado = p.tieneInventario && (p.stock ?? 0) <= 0;
            return `
                  <div class="card animate-fade-in" style="animation-delay:${i * 30}ms" data-product-id="${p.id}" data-nombre="${p.nombre}" data-precio="${p.precio}" ${agotado ? 'data-agotado="1"' : ''}>
                    <div class="flex-1 mr-4">
                      <h3 class="font-semibold text-primary text-[0.9375rem]">${p.nombre}</h3>
                      ${p.descripcion ? `<p class="text-sm text-text-secondary mt-0.5 leading-relaxed">${p.descripcion}</p>` : ''}
                      <div class="flex flex-wrap items-center gap-2 mt-2">
                        <span class="inline-block font-bold text-accent text-lg">$${p.precio.toFixed(2)}</span>
                        ${p.tieneInventario
                          ? `<span class="badge ${agotado ? 'bg-error/10 text-error' : 'bg-surface text-text-secondary'}">
                              ${agotado ? '<i data-lucide="package-x" class="w-3.5 h-3.5"></i> Agotado' : `<i data-lucide="package" class="w-3.5 h-3.5"></i> ${p.stock} disponibles`}
                            </span>`
                          : ''}
                      </div>
                    </div>
                    <div class="flex flex-col items-center gap-2">
                      <div class="flex items-center gap-2 ${agotado ? 'opacity-40 pointer-events-none' : ''}">
                    <button class="qty-btn minus" data-id="${p.id}">
                      <i data-lucide="minus" class="w-4 h-4"></i>
                    </button>
                    <span class="qty-value w-8 text-center font-bold text-primary text-[0.9375rem]">${carrito[p.id]?.cantidad || 0}</span>
                    <button class="qty-btn plus" data-id="${p.id}">
                      <i data-lucide="plus" class="w-4 h-4"></i>
                    </button>
                  </div>
                </div>
              </div>
            </div>
          `; }).join('')}
        </div>
      </div>

      <div id="carrito-bar" class="hidden" style="position:fixed;bottom:5.5rem;left:12px;right:12px;z-index:40;max-width:420px;margin:0 auto">
        <div class="card-elevated" style="padding:1rem 1.25rem">
          <div class="flex justify-between items-center mb-1.5">
            <span class="font-semibold text-primary text-sm">Tu pedido</span>
            <span id="carrito-count" class="badge badge-accent">0 items</span>
          </div>
          <div class="flex justify-between items-center mb-3">
            <span class="text-text-secondary text-sm">Total</span>
            <span id="carrito-total" class="font-bold text-accent text-xl">$0.00</span>
          </div>
          <button id="btn-hacer-pedido" class="btn-accent w-full">
            <i data-lucide="shopping-bag" class="w-5 h-5"></i> Hacer Pedido
          </button>
        </div>
      </div>

      <div id="modal-pedido" class="modal-overlay hidden">
        <div class="modal-sheet">
          <div class="flex justify-between items-center mb-5">
            <h2 class="text-lg font-bold text-primary">Tu Pedido</h2>
            <button id="btn-cancelar" class="btn-icon bg-surface">
              <i data-lucide="x" class="w-5 h-5 text-text-secondary"></i>
            </button>
          </div>
          <div id="resumen-pedido" class="space-y-2 mb-5"></div>
          <p class="text-sm mb-4">${seleccion.entrega === 'Domicilio' ? 'Entrega a domicilio' : 'Recoger en tienda'}</p>
          <label id="direccion-entrega-wrap" class="block text-sm mb-4 hidden">Dirección para entrega a domicilio
            <textarea id="direccion-entrega" class="input-field" rows="2" maxlength="300" placeholder="Calle, número y referencias"></textarea>
          </label>
          <div class="mb-4">
            <label class="block text-sm font-medium text-primary mb-2">Forma de pago</label>
            <div class="flex gap-2">
              <button type="button" class="chip active pay-option" data-value="Efectivo">
                <i data-lucide="banknote" class="w-4 h-4"></i> Efectivo
              </button>
              <button type="button" class="chip pay-option" data-value="Transferencia">
                <i data-lucide="smartphone" class="w-4 h-4"></i> Transferencia
              </button>
              <button type="button" class="chip pay-option" data-value="Fiado">
                <i data-lucide="clock" class="w-4 h-4"></i> Fiado
              </button>
            </div>
          </div>
          <div class="mb-5">
            <textarea id="observaciones" placeholder="Instrucciones para tu pedido (opcional)" class="input-field" rows="2" style="resize:none"></textarea>
          </div>
          <button id="btn-confirmar" class="btn-primary w-full">
            <i data-lucide="check" class="w-5 h-5"></i> Confirmar Pedido
          </button>
        </div>
      </div>
    `;

    if (window.lucide) lucide.createIcons();
    bindSeleccion();
    setupCarrito(content, slug, cliente, carrito, updateCarrito, seleccion, opciones);
    function bindSeleccion() {
      const entrega = content.querySelector('#entrega-catalogo');
      entrega.value = seleccion.entrega;
      entrega.addEventListener('change', () => {
        seleccion.entrega = entrega.value;
        Object.keys(carrito).forEach(k => delete carrito[k]);
        renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion);
      });
      const modalidad = content.querySelector('#modalidad-pedido');
      modalidad.value = seleccion.modalidad;
      modalidad.addEventListener('change', () => {
        seleccion.modalidad = modalidad.value;
        seleccion.fecha = ''; seleccion.ventana = '';
        Object.keys(carrito).forEach(k => delete carrito[k]);
        renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion);
      });
      const fecha = content.querySelector('#fecha-servicio');
      if (fecha) {
        fecha.value = seleccion.fecha;
        fecha.addEventListener('change', () => {
          seleccion.fecha = fecha.value; seleccion.ventana = '';
          Object.keys(carrito).forEach(k => delete carrito[k]);
          renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion);
        });
      }
      const ventana = content.querySelector('#ventana-servicio');
      if (ventana) { ventana.value = seleccion.ventana; ventana.addEventListener('change', () => { seleccion.ventana = ventana.value; }); }
    }
  }
}

function setupCarrito(content, slug, cliente, carrito, updateCarrito, seleccion, opciones) {
  content.querySelectorAll('.qty-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const card = btn.closest('[data-product-id]');
      const id = card.dataset.productId;
      const nombre = card.dataset.nombre;
      const precio = parseFloat(card.dataset.precio);
      const qtyEl = card.querySelector('.qty-value');
      let qty = parseInt(qtyEl.textContent);

      if (btn.classList.contains('plus')) {
        qty++;
      } else if (btn.classList.contains('minus') && qty > 0) {
        qty--;
      }

      qtyEl.textContent = qty;

      if (qty > 0) {
        carrito[id] = { id, nombre, precio, cantidad: qty };
      } else {
        delete carrito[id];
      }

      updateCarrito(carrito);
      actualizarBarra(content, carrito);
    });
  });

  const btnHacerPedido = content.querySelector('#btn-hacer-pedido');
  const modal = content.querySelector('#modal-pedido');
  const btnCancelar = content.querySelector('#btn-cancelar');
  const btnConfirmar = content.querySelector('#btn-confirmar');
  const syncDireccion = () => {
    const domicilio = seleccion.entrega === 'Domicilio';
    content.querySelector('#direccion-entrega-wrap').classList.toggle('hidden', !domicilio);
    content.querySelector('#direccion-entrega').required = domicilio;
  };
  syncDireccion();

  btnHacerPedido?.addEventListener('click', () => {
    if (seleccion.modalidad === 'Programado' && (!seleccion.fecha || !seleccion.ventana)) {
      alert('Selecciona fecha y ventana de servicio.'); return;
    }
    mostrarResumen(content, carrito);
    content.querySelector('#resumen-pedido').insertAdjacentHTML('beforeend', `<p class="text-sm text-primary">${seleccion.modalidad === 'Programado' ? `Horario solicitado: ${seleccion.fecha} ${seleccion.ventana}` : `Al momento: ${opciones.hoy}. La tienda confirmará el horario estimado.`}</p><p class="text-sm text-text-secondary">${seleccion.entrega === 'Domicilio' ? 'Entrega a domicilio' : 'Recolección en tienda'} · pendiente de confirmación.</p>`);
    modal.classList.remove('hidden');
  });

  btnCancelar?.addEventListener('click', () => {
    modal.classList.add('hidden');
  });

  content.querySelectorAll('.pay-option').forEach(btn => {
    btn.addEventListener('click', () => {
      content.querySelectorAll('.pay-option').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
    });
  });

  btnConfirmar?.addEventListener('click', async () => {
    const items = Object.values(carrito).map(c => ({
      productoId: parseInt(c.id),
      cantidad: c.cantidad
    }));

    if (items.length === 0) return;

    const activePay = content.querySelector('.pay-option.active');
    const formaPago = activePay ? activePay.dataset.value : 'Efectivo';
    const observaciones = content.querySelector('#observaciones').value;
    const entrega = seleccion.entrega;
    const direccionEntrega = content.querySelector('#direccion-entrega').value.trim();
    if (entrega === 'Domicilio' && !direccionEntrega) { alert('Captura la dirección de entrega.'); return; }

    try {
      await api.public.crearPedido(slug, {
        telefonoCliente: cliente.telefono,
        formaPago,
        observaciones: observaciones || null,
        modalidad: seleccion.modalidad,
        entrega,
        direccionEntrega: entrega === 'Domicilio' ? direccionEntrega : null,
        fechaServicio: seleccion.modalidad === 'Programado' ? seleccion.fecha : opciones.hoy,
        ventanaSolicitada: seleccion.modalidad === 'Programado' ? seleccion.ventana : null,
        items
      });

      modal.classList.add('hidden');
      content.innerHTML = `
        <div class="min-h-screen flex items-center justify-center p-8">
          <div class="text-center animate-scale-in">
            <div class="w-20 h-20 rounded-full bg-success/15 mx-auto mb-5 flex items-center justify-center">
              <i data-lucide="check" class="w-10 h-10 text-success"></i>
            </div>
            <h1 class="text-2xl font-bold text-primary mb-2">¡Pedido Enviado!</h1>
            <p class="text-text-secondary mb-8 leading-relaxed">Tu pedido ha sido enviado a la tienda.<br>Espera confirmación.</p>
            <button id="btn-volver-menu" class="btn-primary">
              <i data-lucide="utensils" class="w-5 h-5"></i> Volver al Menú
            </button>
          </div>
        </div>
      `;
      if (window.lucide) lucide.createIcons();
      content.querySelector('#btn-volver-menu').addEventListener('click', () => {
        Object.keys(carrito).forEach(k => delete carrito[k]);
        updateCarrito(carrito);
        const menuBtn = content.closest('#app').querySelector('[data-tab="menu"]');
        if (menuBtn) menuBtn.click();
        else window.location.reload();
      });
    } catch (error) {
      alert(error.message || 'Error al enviar el pedido. Intenta de nuevo.');
    }
  });

  actualizarBarra(content, carrito);
}

function actualizarBarra(content, carrito) {
  const items = Object.values(carrito);
  const total = items.reduce((sum, c) => sum + (c.precio * c.cantidad), 0);
  const count = items.reduce((sum, c) => sum + c.cantidad, 0);

  const bar = content.querySelector('#carrito-bar');
  const countEl = content.querySelector('#carrito-count');
  const totalEl = content.querySelector('#carrito-total');

  if (count > 0) {
    bar.classList.remove('hidden');
    countEl.textContent = `${count} items`;
    totalEl.textContent = `$${total.toFixed(2)}`;
  } else {
    bar.classList.add('hidden');
  }
}

function mostrarResumen(content, carrito) {
  const resumen = content.querySelector('#resumen-pedido');
  const items = Object.values(carrito);
  const total = items.reduce((sum, c) => sum + (c.precio * c.cantidad), 0);

  resumen.innerHTML = items.map(c => `
    <div class="flex justify-between text-sm py-1">
      <span class="text-primary">${c.nombre} <span class="text-text-secondary">×${c.cantidad}</span></span>
      <span class="font-semibold text-primary">$${(c.precio * c.cantidad).toFixed(2)}</span>
    </div>
  `).join('') + `
    <div class="flex justify-between font-bold text-primary mt-3 pt-3" style="border-top:1px solid #eeece8">
      <span>Total</span>
      <span class="text-accent text-lg">$${total.toFixed(2)}</span>
    </div>
  `;
}

function renderPedidosTab(content, slug, cliente, zonaHoraria) {
  content.innerHTML = `
    <div class="page-padding">
      <div class="space-y-3">
        <div class="skeleton h-28 w-full"></div>
        <div class="skeleton h-28 w-full"></div>
      </div>
    </div>
  `;

  api.public.getPedidosCliente(slug, cliente.telefono).then(pedidos => {
    if (pedidos.length === 0) {
      content.innerHTML = `
        <div class="empty-state">
          <div class="empty-state-icon"><i data-lucide="clipboard-list"></i></div>
          <h3>Sin pedidos aún</h3>
          <p>Tus pedidos aparecerán aquí cuando hagas uno</p>
        </div>
      `;
      if (window.lucide) lucide.createIcons();
      return;
    }

    const activos = pedidos.filter(p => p.estado !== 'Entregado' && p.estado !== 'Cancelado');
    const historial = pedidos.filter(p => p.estado === 'Entregado' || p.estado === 'Cancelado');

    const estadoBadge = (estado) => {
      switch (estado) {
        case 'Recibido': return 'badge-info';
        case 'Confirmado': return 'badge-warning';
        case 'Preparando': case 'EnPreparacion': return 'badge-warning';
        case 'Listo': return 'badge-success';
        case 'Entregado': return 'badge-info';
        case 'Cancelado': return 'badge-error';
        default: return 'badge-info';
      }
    };

    const renderPedidoCard = (p) => `
      <div class="card animate-fade-in">
        <div class="flex justify-between items-start mb-3">
          <span class="text-[0.8125rem] text-text-secondary flex items-center gap-1">
            <i data-lucide="hash" class="w-3.5 h-3.5"></i> ${p.id}
          </span>
          <span class="badge ${estadoBadge(p.estado)}">${p.estado}</span>
        </div>
        <div class="space-y-1.5 mb-3">
          ${p.fechaServicio ? `<p class="text-sm text-primary">${p.modalidad === 'Programado' ? 'Programado' : 'Al momento'} · ${p.fechaServicio} · ${p.entrega === 'Domicilio' ? 'Domicilio' : 'Recoger'}</p>` : '<p class="text-sm text-text-secondary">Pedido anterior sin fecha de servicio registrada</p>'}
          ${p.ventanaSolicitada ? `<p class="text-sm text-text-secondary">Horario solicitado: ${p.ventanaSolicitada}</p>` : ''}
          ${p.direccionEntrega ? `<p class="text-sm text-text-secondary">Dirección de entrega: ${escapeHtml(p.direccionEntrega)}</p>` : ''}
          ${p.horarioConfirmado ? `<p class="text-sm text-primary">Horario confirmado o estimado: ${escapeHtml(p.horarioConfirmado)}</p>` : ''}
          ${p.estado === 'Recibido' ? '<p class="text-sm text-text-secondary">Pendiente de confirmación de la tienda</p>' : ''}
          ${p.estado === 'Listo' ? '<p class="text-sm text-success">Pedido listo para recoger o entregar</p>' : ''}
          ${p.items.map(i => `
            <div class="flex justify-between text-sm">
              <span class="text-primary">${i.productoNombre} <span class="text-text-secondary">×${i.cantidad}</span></span>
              <span class="font-medium text-primary">$${i.subtotal.toFixed(2)}</span>
            </div>
          `).join('')}
        </div>
        <div class="flex justify-between items-center pt-3" style="border-top:1px solid #eeece8">
          <span class="text-text-secondary text-[0.8125rem] flex items-center gap-1">
            <i data-lucide="clock" class="w-3.5 h-3.5"></i>
            ${fechaHoraEnTienda(p.creadoEn, zonaHoraria)}
          </span>
          <span class="font-bold text-accent">$${p.total.toFixed(2)}</span>
        </div>
        ${p.observaciones ? `<p class="text-[0.8125rem] text-text-secondary mt-2.5 italic flex items-center gap-1"><i data-lucide="message-square" class="w-3.5 h-3.5"></i> ${p.observaciones}</p>` : ''}
      </div>
    `;

    content.innerHTML = `
      <div class="page-padding">
        ${activos.length > 0 ? `
          <p class="section-title">Activos</p>
          <div class="space-y-3 mb-6">${activos.map(renderPedidoCard).join('')}</div>
        ` : ''}
        ${historial.length > 0 ? `
          <p class="section-title">Historial</p>
          <div class="space-y-3">${historial.map(renderPedidoCard).join('')}</div>
        ` : ''}
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }).catch(() => {
    content.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
        <h3>Error</h3>
        <p>No se pudieron cargar tus pedidos</p>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  });
}

function renderCuentaTab(container, slug, tienda, cliente) {
  const content = container.querySelector('#client-content');
  content.innerHTML = `
    <div class="page-padding">
      <div class="card-elevated mb-4 animate-fade-in">
        <div class="flex items-center gap-4 mb-5">
          <div class="w-14 h-14 rounded-[18px] bg-primary/10 flex items-center justify-center">
            <span class="text-xl font-bold text-primary">${cliente.nombre.charAt(0).toUpperCase()}</span>
          </div>
          <div>
            <h2 class="text-lg font-bold text-primary">${cliente.nombre}</h2>
            <p class="text-sm text-text-secondary flex items-center gap-1"><i data-lucide="phone" class="w-3.5 h-3.5"></i> ${cliente.telefono}</p>
          </div>
        </div>
        <div class="space-y-3">
          <div class="flex items-center gap-3 p-3 rounded-2xl bg-surface">
            <i data-lucide="store" class="w-5 h-5 text-accent"></i>
            <div>
              <p class="text-[0.8125rem] text-text-secondary">Tienda</p>
              <p class="font-medium text-primary text-sm">${tienda.nombre}</p>
            </div>
          </div>
        </div>
      </div>
      <button id="btn-logout" class="btn-danger w-full justify-center">
        <i data-lucide="log-out" class="w-4 h-4"></i> Cambiar de cuenta
      </button>
    </div>
  `;

  if (window.lucide) lucide.createIcons();

  content.querySelector('#btn-logout').addEventListener('click', () => {
    removeClient(slug);
    window.location.reload();
  });
}
