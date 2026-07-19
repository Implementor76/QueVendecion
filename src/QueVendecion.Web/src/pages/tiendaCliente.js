import { api } from '../utils/api.js';
import { getClient, setClient, removeClient } from '../utils/clientAuth.js';

export function renderTiendaCliente(container, params) {
  const slug = params?.slug;
  if (!slug) {
    container.innerHTML = `<p class="text-center py-8 text-primary-light">Tienda no encontrada</p>`;
    return;
  }

  container.innerHTML = `
    <div class="min-h-screen bg-surface">
      <header class="bg-primary text-white p-4">
        <h1 class="text-xl font-bold">Cargando tienda...</h1>
      </header>
      <main class="p-4">
        <p class="text-center text-primary-light py-8">Cargando menú...</p>
      </main>
    </div>
  `;

  loadTiendaCliente(container, slug);
}

async function loadTiendaCliente(container, slug) {
  try {
    const tienda = await api.public.getTienda(slug);
    const cliente = getClient(slug);

    if (!cliente) {
      renderLogin(container, slug, tienda);
      return;
    }

    renderApp(container, slug, tienda, cliente);
  } catch (error) {
    container.innerHTML = `
      <div class="min-h-screen flex items-center justify-center p-6">
        <div class="text-center">
          <h1 class="text-2xl font-bold text-primary mb-2">Tienda no encontrada</h1>
          <p class="text-primary-light">La tienda "${slug}" no existe o no está disponible.</p>
        </div>
      </div>
    `;
  }
}

function renderLogin(container, slug, tienda) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface flex flex-col items-center justify-center p-6">
      <div class="text-center mb-6">
        <h1 class="text-2xl font-bold text-primary">${tienda.nombre}</h1>
        ${tienda.direccion ? `<p class="text-sm text-primary-light mt-1">${tienda.direccion}</p>` : ''}
      </div>
      <div class="card w-full max-w-sm">
        <h2 class="text-lg font-bold text-primary mb-4">Bienvenido</h2>
        <p class="text-sm text-primary-light mb-4">Ingresa tus datos para hacer pedidos</p>
        <div class="mb-3">
          <label class="block text-sm font-medium text-primary mb-1">Tu nombre</label>
          <input type="text" id="login-nombre" placeholder="Ej: María" class="input-field" />
        </div>
        <div class="mb-4">
          <label class="block text-sm font-medium text-primary mb-1">Teléfono</label>
          <input type="tel" id="login-telefono" placeholder="Ej: 555-123-4567" class="input-field" />
        </div>
        <button id="btn-login" class="btn-accent w-full font-bold text-lg py-3">Entrar</button>
      </div>
    </div>
  `;

  container.querySelector('#btn-login').addEventListener('click', async () => {
    const nombre = container.querySelector('#login-nombre').value.trim();
    const telefono = container.querySelector('#login-telefono').value.trim();

    if (!nombre) { alert('Ingresa tu nombre'); return; }
    if (!telefono) { alert('Ingresa tu teléfono'); return; }

    try {
      const cliente = await api.public.registrarCliente(slug, { nombre, telefono });
      setClient(slug, { id: cliente.id, nombre: cliente.nombre, telefono: cliente.telefono });
      renderApp(container, slug, tienda, cliente);
    } catch (error) {
      alert('Error al registrar. Intenta de nuevo.');
    }
  });
}

function renderApp(container, slug, tienda, cliente) {
  container.innerHTML = `
    <div class="min-h-screen bg-surface pb-20">
      <header class="bg-primary text-white p-4 sticky top-0 z-10 flex justify-between items-center">
        <div>
          <h1 class="text-xl font-bold">${tienda.nombre}</h1>
          ${tienda.direccion ? `<p class="text-xs text-accent opacity-80">${tienda.direccion}</p>` : ''}
        </div>
        <span class="text-sm text-accent">${cliente.nombre}</span>
      </header>

      <main id="client-content"></main>

      <nav class="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 z-20">
        <div class="flex justify-around max-w-lg mx-auto">
          <button data-tab="menu" class="tab-btn flex flex-col items-center py-2 px-4 text-primary-light">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"/></svg>
            <span class="text-xs mt-1">Menú</span>
          </button>
          <button data-tab="pedidos" class="tab-btn flex flex-col items-center py-2 px-4 text-primary-light">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>
            <span class="text-xs mt-1">Mis Pedidos</span>
          </button>
          <button data-tab="cuenta" class="tab-btn flex flex-col items-center py-2 px-4 text-primary-light">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/></svg>
            <span class="text-xs mt-1">Mi Cuenta</span>
          </button>
        </div>
      </nav>
    </div>
  `;

  let currentTab = 'menu';
  let carrito = {};
  let productos = [];

  function switchTab(tab) {
    currentTab = tab;
    container.querySelectorAll('.tab-btn').forEach(btn => {
      if (btn.dataset.tab === tab) {
        btn.classList.add('text-primary', 'font-semibold');
        btn.classList.remove('text-primary-light');
      } else {
        btn.classList.remove('text-primary', 'font-semibold');
        btn.classList.add('text-primary-light');
      }
    });

    const content = container.querySelector('#client-content');
    if (tab === 'menu') renderMenuTab(content, slug, cliente, carrito, (c) => { carrito = c; });
    else if (tab === 'pedidos') renderPedidosTab(content, slug, cliente);
    else if (tab === 'cuenta') renderCuentaTab(container, slug, tienda, cliente);
  }

  container.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', () => switchTab(btn.dataset.tab));
  });

  switchTab('menu');
}

function renderMenuTab(content, slug, cliente, carrito, updateCarrito) {
  content.innerHTML = `<p class="text-center text-primary-light py-8">Cargando menú...</p>`;

  api.public.getMenu(slug).then(productos => {
    if (productos.length === 0) {
      content.innerHTML = `<p class="text-center text-primary-light py-8">Esta tienda aún no tiene productos</p>`;
      return;
    }

    content.innerHTML = `
      <div class="p-4 space-y-3">
        ${productos.map(p => `
          <div class="card flex justify-between items-center" data-id="${p.id}" data-nombre="${p.nombre}" data-precio="${p.precio}">
            <div class="flex-1 mr-3">
              <h3 class="font-semibold text-primary">${p.nombre}</h3>
              ${p.descripcion ? `<p class="text-sm text-primary-light">${p.descripcion}</p>` : ''}
            </div>
            <div class="text-right">
              <span class="text-accent font-bold text-lg">$${p.precio.toFixed(2)}</span>
              <div class="flex items-center gap-2 mt-1">
                <button class="qty-btn minus w-7 h-7 rounded-full bg-primary text-white font-bold text-sm">-</button>
                <span class="qty-value w-6 text-center font-semibold">${carrito[p.id]?.cantidad || 0}</span>
                <button class="qty-btn plus w-7 h-7 rounded-full bg-primary text-white font-bold text-sm">+</button>
              </div>
            </div>
          </div>
        `).join('')}
      </div>

      <div id="carrito-bar" class="fixed bottom-16 left-0 right-0 bg-primary text-white p-4 hidden z-10 max-w-lg mx-auto">
        <div class="flex justify-between items-center mb-2">
          <span class="font-semibold">Tu pedido</span>
          <span id="carrito-count" class="text-accent font-bold">0 items</span>
        </div>
        <div class="flex justify-between items-center mb-3">
          <span>Total:</span>
          <span id="carrito-total" class="text-accent font-bold text-xl">$0.00</span>
        </div>
        <button id="btn-hacer-pedido" class="btn-accent w-full text-lg py-3 font-bold">Hacer Pedido</button>
      </div>

      <div id="modal-pedido" class="fixed inset-0 bg-black/50 z-30 hidden flex items-end justify-center">
        <div class="bg-white rounded-t-2xl w-full max-w-lg p-6">
          <h2 class="text-lg font-bold text-primary mb-4">Tu Pedido</h2>
          <div id="resumen-pedido" class="space-y-2 mb-4"></div>
          <div class="mb-4">
            <label class="block text-sm font-medium text-primary mb-1">Forma de pago</label>
            <select id="forma-pago" class="input-field">
              <option value="Efectivo">Efectivo</option>
              <option value="Transferencia">Transferencia</option>
              <option value="Fiado">Fiado</option>
            </select>
          </div>
          <div class="mb-4">
            <label class="block text-sm font-medium text-primary mb-1">Observaciones (opcional)</label>
            <textarea id="observaciones" placeholder="Ej: Sin cebolla, extra salsa..." class="input-field" rows="2"></textarea>
          </div>
          <div class="flex gap-2">
            <button id="btn-cancelar" class="btn-primary flex-1">Cancelar</button>
            <button id="btn-confirmar" class="btn-accent flex-1 font-bold">Confirmar</button>
          </div>
        </div>
      </div>
    `;

    setupCarrito(content, slug, cliente, carrito, updateCarrito);
  });
}

function setupCarrito(content, slug, cliente, carrito, updateCarrito) {
  content.querySelectorAll('.qty-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const card = btn.closest('[data-id]');
      const id = card.dataset.id;
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

  btnHacerPedido?.addEventListener('click', () => {
    mostrarResumen(content, carrito);
    modal.classList.remove('hidden');
  });

  btnCancelar?.addEventListener('click', () => {
    modal.classList.add('hidden');
  });

  btnConfirmar?.addEventListener('click', async () => {
    const items = Object.values(carrito).map(c => ({
      productoId: parseInt(c.id),
      cantidad: c.cantidad
    }));

    if (items.length === 0) return;

    const formaPago = content.querySelector('#forma-pago').value;
    const observaciones = content.querySelector('#observaciones').value;

    try {
      await api.public.crearPedido(slug, {
        clienteId: cliente.id,
        clienteNombre: cliente.nombre,
        formaPago,
        observaciones: observaciones || null,
        items
      });

      modal.classList.add('hidden');
      content.innerHTML = `
        <div class="min-h-screen flex items-center justify-center p-6">
          <div class="text-center">
            <div class="text-6xl mb-4">✓</div>
            <h1 class="text-2xl font-bold text-primary mb-2">¡Pedido Enviado!</h1>
            <p class="text-primary-light mb-6">Tu pedido ha sido enviado a la tienda. Espera confirmación.</p>
            <button id="btn-volver-menu" class="btn-primary">Volver al Menú</button>
          </div>
        </div>
      `;
      content.querySelector('#btn-volver-menu').addEventListener('click', () => {
        Object.keys(carrito).forEach(k => delete carrito[k]);
        updateCarrito(carrito);
        const menuBtn = content.closest('#app').querySelector('[data-tab="menu"]');
        if (menuBtn) menuBtn.click();
        else window.location.reload();
      });
    } catch (error) {
      alert('Error al enviar el pedido. Intenta de nuevo.');
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
    <div class="flex justify-between text-sm">
      <span>${c.nombre} x${c.cantidad}</span>
      <span class="font-semibold">$${(c.precio * c.cantidad).toFixed(2)}</span>
    </div>
  `).join('') + `
    <div class="flex justify-between font-bold text-primary border-t pt-2 mt-2">
      <span>Total</span>
      <span class="text-accent text-lg">$${total.toFixed(2)}</span>
    </div>
  `;
}

function renderPedidosTab(content, slug, cliente) {
  content.innerHTML = `<p class="text-center text-primary-light py-8">Cargando pedidos...</p>`;

  api.public.getPedidosCliente(slug, cliente.telefono).then(pedidos => {
    if (pedidos.length === 0) {
      content.innerHTML = `
        <div class="p-4 text-center">
          <div class="text-4xl mb-3">📋</div>
          <h2 class="text-lg font-bold text-primary mb-1">Sin pedidos aún</h2>
          <p class="text-sm text-primary-light">Tus pedidos aparecerán aquí cuando hagas uno.</p>
        </div>
      `;
      return;
    }

    const activos = pedidos.filter(p => p.estado !== 'Entregado' && p.estado !== 'Cancelado');
    const historial = pedidos.filter(p => p.estado === 'Entregado' || p.estado === 'Cancelado');

    const estadoColor = (estado) => {
      switch (estado) {
        case 'Recibido': return 'bg-blue-100 text-blue-700';
        case 'Confirmado': return 'bg-yellow-100 text-yellow-700';
        case 'Preparando': return 'bg-orange-100 text-orange-700';
        case 'Listo': return 'bg-green-100 text-green-700';
        case 'Entregado': return 'bg-gray-100 text-gray-500';
        case 'Cancelado': return 'bg-red-100 text-red-700';
        default: return 'bg-gray-100 text-gray-500';
      }
    };

    const renderPedidoCard = (p) => `
      <div class="card mb-3">
        <div class="flex justify-between items-start mb-2">
          <span class="text-xs text-primary-light">#${p.id}</span>
          <span class="text-xs px-2 py-0.5 rounded-full font-medium ${estadoColor(p.estado)}">${p.estado}</span>
        </div>
        <div class="space-y-1 mb-2">
          ${p.items.map(i => `
            <div class="flex justify-between text-sm">
              <span>${i.productoNombre} x${i.cantidad}</span>
              <span class="font-medium">$${i.subtotal.toFixed(2)}</span>
            </div>
          `).join('')}
        </div>
        <div class="flex justify-between items-center border-t pt-2">
          <span class="text-xs text-primary-light">${formatDate(p.creadoEn)}</span>
          <span class="font-bold text-accent">$${p.total.toFixed(2)}</span>
        </div>
        ${p.observaciones ? `<p class="text-xs text-primary-light mt-2 italic">📝 ${p.observaciones}</p>` : ''}
      </div>
    `;

    content.innerHTML = `
      <div class="p-4">
        ${activos.length > 0 ? `
          <h2 class="text-sm font-bold text-primary-light uppercase tracking-wide mb-3">Activos</h2>
          ${activos.map(renderPedidoCard).join('')}
        ` : ''}
        ${historial.length > 0 ? `
          <h2 class="text-sm font-bold text-primary-light uppercase tracking-wide mb-3 mt-6">Historial</h2>
          ${historial.map(renderPedidoCard).join('')}
        ` : ''}
        ${activos.length === 0 && historial.length === 0 ? `
          <div class="text-center">
            <div class="text-4xl mb-3">📋</div>
            <h2 class="text-lg font-bold text-primary mb-1">Sin pedidos aún</h2>
            <p class="text-sm text-primary-light">Tus pedidos aparecerán aquí cuando hagas uno.</p>
          </div>
        ` : ''}
      </div>
    `;
  }).catch(() => {
    content.innerHTML = `<p class="text-center text-primary-light py-8">Error al cargar pedidos</p>`;
  });
}

function renderCuentaTab(container, slug, tienda, cliente) {
  const content = container.querySelector('#client-content');
  content.innerHTML = `
    <div class="p-4">
      <div class="card mb-4">
        <h2 class="text-lg font-bold text-primary mb-3">Mi Cuenta</h2>
        <div class="space-y-2">
          <div>
            <label class="text-xs text-primary-light">Nombre</label>
            <p class="font-medium text-primary">${cliente.nombre}</p>
          </div>
          <div>
            <label class="text-xs text-primary-light">Teléfono</label>
            <p class="font-medium text-primary">${cliente.telefono}</p>
          </div>
          <div>
            <label class="text-xs text-primary-light">Tienda</label>
            <p class="font-medium text-primary">${tienda.nombre}</p>
          </div>
        </div>
      </div>
      <button id="btn-logout" class="w-full bg-red-50 text-red-600 py-3 rounded-lg font-medium hover:bg-red-100 transition">
        Cambiar de cuenta
      </button>
    </div>
  `;

  content.querySelector('#btn-logout').addEventListener('click', () => {
    removeClient(slug);
    window.location.reload();
  });
}

function formatDate(dateStr) {
  const d = new Date(dateStr);
  const day = d.getDate().toString().padStart(2, '0');
  const month = (d.getMonth() + 1).toString().padStart(2, '0');
  const hours = d.getHours().toString().padStart(2, '0');
  const mins = d.getMinutes().toString().padStart(2, '0');
  return `${day}/${month} ${hours}:${mins}`;
}
