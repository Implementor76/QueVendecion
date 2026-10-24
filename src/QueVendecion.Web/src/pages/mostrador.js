import { api } from '../utils/api.js';
import { getAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';

const escapar = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const dinero = value => Number(value).toLocaleString('es-MX', { style: 'currency', currency: 'MXN' });

export async function renderMostrador(container) {
  const auth = getAuth();
  if (!auth) return navigate('/login');
  if (auth.rol !== 'Admin') return navigate('/mi-tienda');
  const storageKey = `qv_mostrador_pendiente_${auth.tiendaId}_${auth.id || auth.email || 'admin'}`;
  let pendiente;
  try { pendiente = JSON.parse(sessionStorage.getItem(storageKey)); } catch { sessionStorage.removeItem(storageKey); }
  container.innerHTML = `
    <div class="min-h-screen bg-surface page-padding-bottom">
      <div class="header-primary"><a href="/mi-tienda" data-link class="text-white">← Mi tienda</a><h1 class="text-xl font-bold mt-2">Venta de mostrador</h1></div>
      <main class="page-padding max-w-xl mx-auto space-y-4">
        <p class="text-sm text-text-secondary">Esta primera versión lista productos disponibles sin control de existencias. El servicio de copias se da de alta en el catálogo.</p>
        <form id="venta-form" class="card space-y-4">
          <label class="block">Servicio<select id="venta-producto" class="input-field w-full" required><option value="">Cargando servicios…</option></select></label>
          <label class="block">Cantidad de caras copiadas<input id="venta-cantidad" class="input-field w-full" type="number" min="1" step="1" inputmode="numeric" required value="1"></label>
          <p class="text-sm text-text-secondary">La cantidad corresponde a caras copiadas. Una hoja copiada por ambos lados cuenta como 2.</p>
          <div id="venta-importes" class="text-sm"></div>
          <label class="block">Medio de pago<select id="venta-pago" class="input-field w-full"><option value="Efectivo">Efectivo</option><option value="Transferencia">Transferencia</option></select></label>
          <label class="block">Nota opcional<textarea id="venta-nota" class="input-field w-full" maxlength="500" rows="2"></textarea></label>
          <button type="submit" class="btn-accent w-full">Revisar venta</button>
        </form>
        <section id="venta-revision" class="card hidden space-y-3" aria-live="polite"></section>
        <p id="venta-error" class="text-error" role="alert"></p>
        <section id="venta-exito" class="card hidden" aria-live="polite"></section>
      </main>
    </div>`;
  const form = container.querySelector('#venta-form');
  const select = container.querySelector('#venta-producto');
  const cantidad = container.querySelector('#venta-cantidad');
  const pago = container.querySelector('#venta-pago');
  const nota = container.querySelector('#venta-nota');
  const revision = container.querySelector('#venta-revision');
  const error = container.querySelector('#venta-error');
  const exito = container.querySelector('#venta-exito');
  let productos = [];
  let enviando = false;
  const seleccionado = () => productos.find(p => p.id === Number(select.value));
  const actualizar = () => {
    const producto = seleccionado();
    const n = Number(cantidad.value);
    container.querySelector('#venta-importes').textContent = producto && Number.isSafeInteger(n) && n > 0
      ? `Precio unitario: ${dinero(producto.precio)} · Subtotal y total: ${dinero(producto.precio * n)}` : '';
    if (!pendiente) revision.classList.add('hidden');
  };
  const bloquear = valor => {
    for (const campo of [select, cantidad, pago, nota]) campo.disabled = valor;
  };
  const mostrarRevision = payload => {
    const producto = productos.find(p => p.id === payload.productoId);
    revision.classList.remove('hidden');
    revision.innerHTML = `<h2 class="font-bold">Revisa el cobro recibido</h2>
      <p>${escapar(producto?.nombre || `Servicio #${payload.productoId}`)} · ${payload.cantidad} caras</p>
      <p>Total: <strong>${dinero(payload.precioEsperado * payload.cantidad)}</strong> · ${escapar(payload.formaPago)}</p>
      <button id="venta-confirmar" type="button" class="btn-accent w-full">Registrar venta y cobro</button>`;
    revision.querySelector('#venta-confirmar').addEventListener('click', confirmar);
  };
  const confirmar = async () => {
    if (enviando || !pendiente) return;
    enviando = true;
    const boton = revision.querySelector('#venta-confirmar');
    boton.disabled = true;
    error.textContent = '';
    try {
      const venta = await api.pedidos.registrarMostrador(pendiente);
      sessionStorage.removeItem(storageKey);
      pendiente = null;
      revision.classList.add('hidden');
      exito.classList.remove('hidden');
      exito.innerHTML = `<h2 class="font-bold">Venta #${venta.id} registrada</h2><p>Cobro recibido: ${dinero(venta.total)}. Entregada y pagada.</p><a href="/pedidos" data-link class="btn-ghost">Ver historial</a><button id="venta-nueva" class="btn-accent w-full mt-3">Nueva venta</button>`;
      exito.querySelector('#venta-nueva').addEventListener('click', () => { exito.classList.add('hidden'); form.reset(); actualizar(); });
      bloquear(false);
    } catch (err) {
      error.textContent = `${err.message} Puedes reintentar esta misma venta sin duplicarla.`;
      boton.disabled = false;
      if (['cantidad_invalida', 'producto_no_disponible', 'inventario_no_permitido', 'pago_invalido', 'nota_invalida', 'precio_actualizado', 'precio_invalido', 'importe_invalido'].includes(err.code)) {
        error.textContent = err.message;
        revision.querySelector('.btn-editar-captura')?.remove();
        const editar = document.createElement('button');
        editar.type = 'button'; editar.className = 'btn-ghost w-full btn-editar-captura'; editar.textContent = 'Editar captura';
        editar.addEventListener('click', async () => {
          sessionStorage.removeItem(storageKey); pendiente = null; bloquear(false); revision.classList.add('hidden');
          productos = (await api.productos.getByMiTienda()).filter(p => p.disponible && !p.tieneInventario);
          select.innerHTML = `<option value="">Selecciona un servicio</option>${productos.map(p => `<option value="${p.id}">${escapar(p.nombre)} · ${dinero(p.precio)}</option>`).join('')}`;
          actualizar();
        });
        revision.append(editar);
      }
    } finally { enviando = false; }
  };
  form.addEventListener('submit', event => {
    event.preventDefault();
    if (pendiente) return;
    const producto = seleccionado();
    const n = Number(cantidad.value);
    if (!producto || !Number.isSafeInteger(n) || n <= 0) { error.textContent = 'Selecciona un servicio y una cantidad entera mayor a cero.'; return; }
    pendiente = { claveIdempotencia: crypto.randomUUID(), productoId: producto.id, cantidad: n, precioEsperado: producto.precio, formaPago: pago.value, nota: nota.value.trim() || null };
    sessionStorage.setItem(storageKey, JSON.stringify(pendiente));
    bloquear(true);
    error.textContent = '';
    mostrarRevision(pendiente);
  });
  for (const campo of [select, cantidad]) campo.addEventListener('input', actualizar);
  try {
    productos = (await api.productos.getByMiTienda()).filter(p => p.disponible && !p.tieneInventario);
    select.innerHTML = `<option value="">Selecciona un servicio</option>${productos.map(p => `<option value="${p.id}">${escapar(p.nombre)} · ${dinero(p.precio)}</option>`).join('')}`;
    if (!productos.length) error.textContent = 'Da de alta un servicio disponible sin control de existencias en el catálogo.';
    if (pendiente) {
      select.value = String(pendiente.productoId);
      cantidad.value = String(pendiente.cantidad);
      pago.value = pendiente.formaPago;
      nota.value = pendiente.nota || '';
      bloquear(true);
      mostrarRevision(pendiente);
      error.textContent = 'Hay un registro pendiente. Reinténtalo con la misma clave para verificar si se cobró.';
    }
    actualizar();
  } catch (err) { error.textContent = err.message; }
}
