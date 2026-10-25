import { api } from '../utils/api.js';
import { getAuth, clearAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';
const escapar = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

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

export function renderMenu(container, params) {
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
          <h1 class="text-xl font-bold mt-0.5">Menú</h1>
        </div>
        <button id="btn-logout" class="btn-icon bg-white/10 text-white/80 hover:bg-white/20">
          <i data-lucide="log-out" class="w-5 h-5"></i>
        </button>
      </div>
      <main class="page-padding page-padding-bottom" style="margin-top:-8px">
        <div id="menu-actions" class="mb-4">
          <button id="btn-agregar" class="btn-accent w-full">
            <i data-lucide="plus" class="w-5 h-5"></i> Agregar Producto
          </button>
        </div>
        <div id="form-agregar" class="hidden mb-4">
          <div class="card-elevated animate-scale-in">
            <h3 class="font-semibold text-primary mb-3 flex items-center gap-2">
              <i data-lucide="plus-circle" class="w-5 h-5 text-accent"></i>
              Nuevo Producto
            </h3>
            <form id="form-producto" class="space-y-3">
              <input type="text" id="prod-nombre" placeholder="Nombre del producto" class="input-field" required />
              <input type="text" id="prod-descripcion" placeholder="Descripción (opcional)" class="input-field" />
              <input type="number" id="prod-precio" placeholder="Precio" step="0.01" min="0" class="input-field" required />
              <label class="block text-sm">Costo unitario de referencia (opcional)<input type="number" id="prod-costo" step="0.0001" min="0" class="input-field" placeholder="Desconocido"></label>
              <label class="block text-sm text-text-secondary">Forma de entrega del producto
                <select id="prod-entrega" class="input-field" required><option value="">Selecciona una opción</option><option value="Recoleccion">Solo recoger</option><option value="Domicilio">Solo domicilio</option><option value="Ambas">Ambas al mismo precio</option></select>
              </label>
              <label class="block text-sm text-text-secondary">Fechas de servicio (opcional; una AAAA-MM-DD por línea). Vacío = disponible habitualmente.
                <textarea id="prod-fechas" class="input-field" rows="2" placeholder="2026-10-24"></textarea>
              </label>
              <label class="flex items-center justify-between gap-3 text-sm text-text-secondary cursor-pointer">
                <span>Controlar inventario</span>
                <input type="checkbox" id="prod-tiene-inventario" class="toggle-input" />
              </label>
              <div id="prod-stock-wrap" class="hidden"><input type="number" id="prod-stock" placeholder="Stock" step="1" min="0" class="input-field" /></div>
              <label class="flex items-center gap-2 text-sm"><input type="checkbox" id="prod-consume-papel"> Este servicio consume papel</label>
              <div id="prod-papel-wrap" class="hidden space-y-2 text-sm"><label>Papel del mismo inventario<select id="prod-papel" class="input-field w-full"><option value="">Selecciona papel</option></select></label><label class="flex items-center gap-2"><input type="checkbox" id="prod-doble"> Admite ambos lados</label><p>Precio y costo estimado por cara; el costo ya incluye papel. El stock del insumo se mide en hojas.</p></div>
              <label class="flex items-center gap-2 text-sm"><input type="checkbox" id="prod-pdf"> Impresión de PDF, blanco y negro, tamaño carta (solo recolección)</label>
              <label class="block text-sm">Existencia mínima (opcional)<input type="number" id="prod-minimo" step="1" min="0" class="input-field"></label>
              <div id="prod-msg" class="text-sm hidden text-center"></div>
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar" class="btn-ghost flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">
                  <i data-lucide="plus" class="w-4 h-4"></i> Agregar
                </button>
              </div>
            </form>
          </div>
        </div>
        <div id="menu-list" class="space-y-3">
          <div class="space-y-3">
            <div class="skeleton h-20 w-full"></div>
            <div class="skeleton h-20 w-full"></div>
            <div class="skeleton h-20 w-full"></div>
          </div>
        </div>
      </main>
      ${renderAdminNav('menu')}
    </div>
  `;

  if (window.lucide) lucide.createIcons();

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
    syncInventory();
    syncPapel();
    container.querySelector('#prod-msg').classList.add('hidden');
  });

  const syncInventory = bindInventory(container, 'prod');
  const syncPapel = bindPaper(container, 'prod');

  container.querySelector('#form-producto').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msg = container.querySelector('#prod-msg');
    try {
      await api.productos.create({
        nombre: container.querySelector('#prod-nombre').value,
        descripcion: container.querySelector('#prod-descripcion').value || null,
        precio: parseFloat(container.querySelector('#prod-precio').value),
        costoUnitario: container.querySelector('#prod-costo').value === '' ? null : Number(container.querySelector('#prod-costo').value),
        existenciaMinima: container.querySelector('#prod-tiene-inventario').checked && container.querySelector('#prod-minimo').value !== '' ? Number(container.querySelector('#prod-minimo').value) : null,
        disponible: true,
        entregaPermitida: container.querySelector('#prod-entrega').value,
        fechasServicio: leerFechas(container.querySelector('#prod-fechas').value),
        tieneInventario: container.querySelector('#prod-tiene-inventario').checked,
        stock: container.querySelector('#prod-tiene-inventario').checked
          ? container.querySelector('#prod-stock').valueAsNumber
          : null,
        papelInsumoId: container.querySelector('#prod-consume-papel').checked ? Number(container.querySelector('#prod-papel').value) : null,
        admiteDobleCara: container.querySelector('#prod-consume-papel').checked && container.querySelector('#prod-doble').checked,
        requierePdf: container.querySelector('#prod-pdf').checked
      });
      msg.textContent = '¡Producto agregado! Agrega otro o cierra.';
      msg.className = 'text-sm text-success';
      container.querySelector('#form-producto').reset();
      syncInventory();
      syncPapel();
      container.querySelector('#prod-nombre').value = '';
      container.querySelector('#prod-descripcion').value = '';
      container.querySelector('#prod-precio').value = '';
      container.querySelector('#prod-nombre').focus();
      loadProductos(container.querySelector('#menu-list'));
    } catch (err) {
      msg.textContent = err.message || 'Error al agregar producto';
      msg.className = 'text-sm text-error';
    }
  });

  loadProductos(container.querySelector('#menu-list'));
}

async function loadProductos(container) {
  try {
    const productos = await api.productos.getByMiTienda();
    const crearPapel = container.closest('.min-h-screen')?.querySelector('#prod-papel');
    if (crearPapel) crearPapel.innerHTML = `<option value="">Selecciona papel</option>${productos.filter(p => p.tieneInventario).map(p => `<option value="${p.id}">${escapar(p.nombre)}</option>`).join('')}`;

    if (productos.length === 0) {
      container.innerHTML = `
        <div class="empty-state">
          <div class="empty-state-icon"><i data-lucide="utensils-crossed"></i></div>
          <h3>Sin productos</h3>
          <p>Agrega tu primer producto para que tus clientes puedan ver tu menú</p>
        </div>
      `;
      if (window.lucide) lucide.createIcons();
      return;
    }

    container.innerHTML = `
      <div class="space-y-3">
        <a href="/inventario" data-link class="card block text-primary font-semibold">Inventario, compras y ventas →</a>
        ${productos.map((p, i) => `
          <div class="card flex justify-between items-center animate-fade-in" style="animation-delay:${i * 40}ms" data-id="${p.id}">
            <div class="flex-1 mr-3">
              <h3 class="font-semibold text-primary text-[0.9375rem]">${p.nombre}</h3>
              ${p.descripcion ? `<p class="text-sm text-text-secondary mt-0.5">${p.descripcion}</p>` : ''}
              <div class="flex flex-wrap gap-1.5 mt-2">
                <span class="badge badge-accent">$${p.precio.toFixed(2)}</span>
                <span class="badge ${p.entregaPermitida ? 'badge-info' : 'badge-error'}">${p.entregaPermitida ? (p.entregaPermitida === 'Ambas' ? 'Recoger y domicilio' : p.entregaPermitida === 'Recoleccion' ? 'Solo recoger' : 'Solo domicilio') : 'Asignar entrega antes de vender'}</span>
                ${p.fechasServicio?.length ? `<span class="badge badge-info">${p.fechasServicio.length} fechas</span>` : ''}
                ${p.tieneInventario
                  ? `<span class="badge ${(p.stock ?? 0) > 0 ? 'bg-surface' : 'bg-error/10 text-error'}">
                      <i data-lucide="package" class="w-3.5 h-3.5"></i>
                      ${(p.stock ?? 0) > 0 ? `${p.stock} en stock` : 'Agotado'}
                    </span>`
                  : ''}
              </div>
            </div>
            <div class="flex gap-1">
              <button class="btn-icon bg-accent/10 text-accent hover:bg-accent/20 btn-edit" data-id="${p.id}" data-nombre="${p.nombre}" data-descripcion="${p.descripcion || ''}" data-precio="${p.precio}">
                <i data-lucide="pencil" class="w-5 h-5"></i>
              </button>
              <button class="btn-icon bg-error/10 text-error hover:bg-error/20 btn-delete" data-id="${p.id}">
                <i data-lucide="trash-2" class="w-5 h-5"></i>
              </button>
            </div>
          </div>
        `).join('')}
      </div>

      <div id="modal-editar" class="modal-overlay hidden">
        <div class="modal-sheet">
          <div class="flex justify-between items-center mb-5">
            <h2 class="text-lg font-bold text-primary">Editar Producto</h2>
            <button id="btn-close-edit" class="btn-icon bg-surface">
              <i data-lucide="x" class="w-5 h-5 text-text-secondary"></i>
            </button>
          </div>
          <form id="form-editar" class="space-y-3">
            <input type="hidden" id="edit-id" />
            <input type="text" id="edit-nombre" placeholder="Nombre" class="input-field" required />
            <input type="text" id="edit-descripcion" placeholder="Descripción" class="input-field" />
            <input type="number" id="edit-precio" placeholder="Precio" step="0.01" min="0" class="input-field" required />
            <label class="block text-sm">Costo unitario de referencia (opcional)<input type="number" id="edit-costo" step="0.0001" min="0" class="input-field"></label>
            <label class="block text-sm text-text-secondary">Forma de entrega del producto
              <select id="edit-entrega" class="input-field" required><option value="">Selecciona una opción</option><option value="Recoleccion">Solo recoger</option><option value="Domicilio">Solo domicilio</option><option value="Ambas">Ambas al mismo precio</option></select>
            </label>
            <label class="block text-sm text-text-secondary">Fechas de servicio (una AAAA-MM-DD por línea; vacío = habitual)
              <textarea id="edit-fechas" class="input-field" rows="2"></textarea>
            </label>
            <label class="flex items-center justify-between gap-3 text-sm text-text-secondary cursor-pointer">
              <span>Controlar inventario</span>
              <input type="checkbox" id="edit-tiene-inventario" class="toggle-input" />
            </label>
            <div id="edit-stock-wrap" class="hidden">
              <input type="number" id="edit-stock" placeholder="Stock" step="1" min="0" class="input-field" />
            </div>
            <label class="flex items-center gap-2 text-sm"><input type="checkbox" id="edit-consume-papel"> Este servicio consume papel</label>
            <div id="edit-papel-wrap" class="hidden space-y-2 text-sm"><label>Papel del mismo inventario<select id="edit-papel" class="input-field w-full"><option value="">Selecciona papel</option>${productos.filter(p => p.tieneInventario).map(p => `<option value="${p.id}">${escapar(p.nombre)}</option>`).join('')}</select></label><label class="flex items-center gap-2"><input type="checkbox" id="edit-doble"> Admite ambos lados</label><p>Precio y costo estimado por cara; el costo ya incluye papel. El stock del insumo se mide en hojas.</p></div>
            <label class="flex items-center gap-2 text-sm"><input type="checkbox" id="edit-pdf"> Impresión de PDF, blanco y negro, tamaño carta (solo recolección)</label>
            <label class="block text-sm">Existencia mínima (opcional)<input type="number" id="edit-minimo" step="1" min="0" class="input-field"></label>
            <button type="submit" class="btn-accent w-full">
              <i data-lucide="check" class="w-4 h-4"></i> Guardar
            </button>
          </form>
        </div>
      </div>
    `;

    if (window.lucide) lucide.createIcons();

    const syncEditInventory = bindInventory(container, 'edit');
    const syncEditPapel = bindPaper(container, 'edit');

    container.querySelectorAll('.btn-edit').forEach(btn => {
      btn.addEventListener('click', () => {
        const producto = productos.find(p => p.id === Number(btn.dataset.id));
        container.querySelector('#edit-tiene-inventario').checked = producto.tieneInventario;
        container.querySelector('#edit-stock').value = producto.stock ?? '';
        container.querySelector('#edit-stock').readOnly = producto.tieneInventario;
        container.querySelector('#edit-tiene-inventario').disabled = producto.tieneInventario;
        container.querySelector('#edit-costo').value = producto.costoUnitario ?? '';
        container.querySelector('#edit-minimo').value = producto.existenciaMinima ?? '';
        syncEditInventory();
        container.querySelector('#edit-consume-papel').checked = producto.consumePapel;
        container.querySelector('#edit-papel').value = producto.papelInsumoId ?? '';
        container.querySelector('#edit-doble').checked = producto.admiteDobleCara;
        container.querySelector('#edit-pdf').checked = producto.requierePdf;
        syncEditPapel();
        container.querySelector('#edit-id').value = btn.dataset.id;
        container.querySelector('#edit-nombre').value = btn.dataset.nombre;
        container.querySelector('#edit-descripcion').value = btn.dataset.descripcion;
        container.querySelector('#edit-precio').value = btn.dataset.precio;
        container.querySelector('#edit-entrega').value = producto.entregaPermitida || '';
        container.querySelector('#edit-fechas').value = (producto.fechasServicio || []).join('\n');
        container.querySelector('#modal-editar').classList.remove('hidden');
      });
    });

    container.querySelector('#btn-close-edit')?.addEventListener('click', () => {
      container.querySelector('#modal-editar').classList.add('hidden');
    });

    container.querySelector('#form-editar').addEventListener('submit', async (e) => {
      e.preventDefault();
      const id = container.querySelector('#edit-id').value;
      const productoActual = productos.find(p => p.id === Number(id));
      try {
        await api.productos.update(id, {
          nombre: container.querySelector('#edit-nombre').value,
          descripcion: container.querySelector('#edit-descripcion').value || null,
          precio: parseFloat(container.querySelector('#edit-precio').value),
          costoUnitario: container.querySelector('#edit-costo').value === '' ? null : Number(container.querySelector('#edit-costo').value),
          existenciaMinima: container.querySelector('#edit-tiene-inventario').checked && container.querySelector('#edit-minimo').value !== '' ? Number(container.querySelector('#edit-minimo').value) : null,
          disponible: productoActual.disponible,
          entregaPermitida: container.querySelector('#edit-entrega').value,
          fechasServicio: leerFechas(container.querySelector('#edit-fechas').value),
          tieneInventario: container.querySelector('#edit-tiene-inventario').checked,
          stock: container.querySelector('#edit-tiene-inventario').checked
            ? container.querySelector('#edit-stock').valueAsNumber
            : null,
          papelInsumoId: container.querySelector('#edit-consume-papel').checked ? Number(container.querySelector('#edit-papel').value) : null,
          admiteDobleCara: container.querySelector('#edit-consume-papel').checked && container.querySelector('#edit-doble').checked,
          requierePdf: container.querySelector('#edit-pdf').checked
        });
        container.querySelector('#modal-editar').classList.add('hidden');
        loadProductos(container);
      } catch (err) {
        alert(err.message || 'Error al guardar');
      }
    });

    container.querySelectorAll('.btn-delete').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (confirm('¿Eliminar este producto?')) {
          await api.productos.delete(btn.dataset.id);
          loadProductos(container);
        }
      });
    });
  } catch (error) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
        <h3>Error</h3>
        <p>No se pudieron cargar los productos</p>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }
}

function bindInventory(container, prefix) {
  const toggle = container.querySelector('#' + prefix + '-tiene-inventario');
  const wrap = container.querySelector('#' + prefix + '-stock-wrap');
  const stock = container.querySelector('#' + prefix + '-stock');
  const sync = () => {
    wrap.classList.toggle('hidden', !toggle.checked);
    stock.required = toggle.checked;
    stock.disabled = !toggle.checked;
    if (!toggle.checked) stock.value = '';
  };
  toggle.addEventListener('change', sync);
  sync();
  return sync;
}

function bindPaper(container, prefix) {
  const toggle = container.querySelector(`#${prefix}-consume-papel`);
  const inventario = container.querySelector(`#${prefix}-tiene-inventario`);
  const wrap = container.querySelector(`#${prefix}-papel-wrap`);
  const select = container.querySelector(`#${prefix}-papel`);
  const doble = container.querySelector(`#${prefix}-doble`);
  const pdf = container.querySelector(`#${prefix}-pdf`);
  const sync = () => {
    toggle.disabled = inventario.checked;
    if (inventario.checked) toggle.checked = false;
    wrap.classList.toggle('hidden', !toggle.checked);
    select.required = toggle.checked;
    if (!toggle.checked) { select.value = ''; doble.checked = false; }
    pdf.disabled = !toggle.checked;
    if (!toggle.checked) pdf.checked = false;
  };
  toggle.addEventListener('change', sync);
  inventario.addEventListener('change', sync);
  sync();
  return sync;
}

function leerFechas(texto) {
  return [...new Set(texto.split('\n').map(x => x.trim()).filter(Boolean))].map(fecha => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(fecha) || Number.isNaN(Date.parse(`${fecha}T00:00:00Z`)))
      throw new Error('Usa una fecha AAAA-MM-DD por línea.');
    return fecha;
  });
}
