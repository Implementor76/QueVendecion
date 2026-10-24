import { api } from '../utils/api.js';
import { getAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';
import { fechaHoraEnTienda, fechaHoyEnTienda } from '../utils/fechaTienda.js';

const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const money = value => `$${Number(value).toFixed(2)}`;
const today = () => new Date().toISOString().slice(0, 10);

export async function renderInventario(container) {
  if (!getAuth()) { navigate('/login'); return; }
  container.innerHTML = `<div class="min-h-screen bg-surface page-padding-bottom">
    <header class="header-primary"><a href="/menu" data-link class="text-white">← Menú</a><h1 class="text-xl font-bold mt-2">Inventario y ventas</h1></header>
    <main class="page-padding space-y-4" style="margin-top:16px">
      <section class="card-elevated"><h2 class="font-semibold mb-3">Ventas entregadas</h2>
        <form id="rango" class="grid grid-cols-2 gap-2 text-sm"><label>Desde<input id="desde" class="input-field" type="date" required value="${today()}"></label>
        <label>Hasta<input id="hasta" class="input-field" type="date" required value="${today()}"></label>
        <button class="btn-accent col-span-2" type="submit">Consultar</button></form><div id="resumen" class="mt-3 text-sm"></div></section>
      <section id="productos" class="space-y-3" aria-live="polite">Cargando productos…</section>
      <p id="mensaje" class="text-sm text-error" role="alert"></p>
    </main></div>`;
  const mensaje = container.querySelector('#mensaje');
  const cargar = async () => {
    try {
      const [productos, tienda] = await Promise.all([api.productos.getByMiTienda(), api.tiendas.getMiTienda()]);
      const zona = tienda.operacion.zonaHoraria;
      if (!container.dataset.fechaInicializada) {
        const hoy = fechaHoyEnTienda(zona);
        container.querySelector('#desde').value = hoy;
        container.querySelector('#hasta').value = hoy;
        container.dataset.fechaInicializada = '1';
      }
      const lista = container.querySelector('#productos');
      lista.innerHTML = productos.map(p => `<article class="card" data-id="${p.id}">
        <h3 class="font-semibold">${escapeHtml(p.nombre)}</h3>
        <p class="text-sm">Venta ${money(p.precio)} · Costo ${p.costoUnitario == null ? 'Desconocido' : money(p.costoUnitario)} · Diferencia ${p.costoUnitario == null ? 'No calculable' : money(p.precio - p.costoUnitario)}</p>
        <p class="text-sm">${p.tieneInventario ? `Disponible: ${p.stock} · Mínimo: ${p.existenciaMinima ?? 'Sin definir'}${p.existenciaMinima != null && p.stock <= p.existenciaMinima ? ' · Stock bajo' : ''}` : 'Sin control de existencias'}</p>
        ${p.tieneInventario ? `<div class="flex flex-wrap gap-2 mt-3"><button class="btn-accent btn-compra" type="button">Entrada por compra</button><button class="btn-ghost btn-ajuste" type="button">Ajuste</button><button class="btn-ghost btn-movimientos" type="button">Movimientos</button></div>
          <div class="detalle mt-3"></div>` : ''}
      </article>`).join('') || '<p class="card">No hay productos.</p>';
      lista.querySelectorAll('article').forEach(card => {
        const id = Number(card.dataset.id);
        const detalle = card.querySelector('.detalle');
        if (!detalle) return;
        card.querySelector('.btn-compra').onclick = () => {
          detalle.innerHTML = `<form class="space-y-2"><label class="block text-sm">Unidades<input name="cantidad" type="number" min="1" step="1" class="input-field" required></label><label class="block text-sm">Costo unitario<input name="costo" type="number" min="0" step="0.0001" class="input-field" required></label><label class="block text-sm">Observación<input name="observacion" maxlength="200" class="input-field"></label><button class="btn-accent" type="submit">Registrar compra</button></form>`;
          detalle.querySelector('form').onsubmit = async e => { e.preventDefault(); const f = e.target.elements; try { await api.productos.compra(id, { cantidad: Number(f.namedItem('cantidad').value), costoUnitario: Number(f.namedItem('costo').value), observaciones: f.namedItem('observacion').value }); mensaje.textContent = ''; await cargar(); } catch (err) { mensaje.textContent = err.message; } };
        };
        card.querySelector('.btn-ajuste').onclick = () => {
          detalle.innerHTML = `<form class="space-y-2"><label class="block text-sm">Variación de unidades (negativa para salida)<input name="cantidad" type="number" step="1" class="input-field" required></label><label class="block text-sm">Motivo<input name="motivo" maxlength="200" class="input-field" required></label><button class="btn-accent" type="submit">Registrar ajuste</button></form>`;
          detalle.querySelector('form').onsubmit = async e => { e.preventDefault(); const f = e.target.elements; try { await api.productos.ajuste(id, { cantidad: Number(f.namedItem('cantidad').value), motivo: f.namedItem('motivo').value }); mensaje.textContent = ''; await cargar(); } catch (err) { mensaje.textContent = err.message; } };
        };
        card.querySelector('.btn-movimientos').onclick = async () => {
          try {
            const rows = await api.productos.movimientos(id);
            detalle.innerHTML = rows.length ? `<div class="space-y-2">${rows.map(m => `<div class="border-b py-2 text-sm"><strong>${escapeHtml(m.tipo)}</strong> ${m.cantidad > 0 ? '+' : ''}${m.cantidad} · ${m.stockAnterior} → ${m.stockNuevo}<br>${fechaHoraEnTienda(m.fechaUtc, zona)}${m.pedidoId ? ` · Pedido #${m.pedidoId}` : ''}${m.usuarioId ? ` · ${escapeHtml(m.usuarioNombre || `Usuario #${m.usuarioId}`)}` : ''}${m.observaciones ? `<br>${escapeHtml(m.observaciones)}` : ''}</div>`).join('')}</div>` : '<p class="text-sm">Sin movimientos registrados. Las existencias heredadas no tienen historial anterior.</p>';
          } catch (err) { mensaje.textContent = err.message; }
        };
      });
    } catch (err) { mensaje.textContent = err.message; }
  };
  container.querySelector('#rango').onsubmit = async e => {
    e.preventDefault();
    try {
      const r = await api.pedidos.resumen(container.querySelector('#desde').value, container.querySelector('#hasta').value);
      container.querySelector('#resumen').innerHTML = `<p>${r.pedidosEntregados} ${r.pedidosEntregados === 1 ? 'pedido entregado' : 'pedidos entregados'} · Ventas ${money(r.ventasEntregadas)}</p><p>Costo registrado ${money(r.costoRegistrado)} · Utilidad bruta estimada ${money(r.utilidadBrutaEstimada)}</p><p>${r.lineasSinCosto} líneas sin costo (${money(r.importeSinCosto)} de venta fuera del cálculo)</p><p>Entregado y pagado ${money(r.entregadoPagado)} · Pendiente de cobro ${money(r.entregadoPendienteCobro)}</p><p>Pedidos por atender ${r.pedidosPorAtender}${r.entregadosSinInstante ? ` · ${r.entregadosSinInstante} entregados históricos sin instante verificable` : ''}</p>`;
      mensaje.textContent = '';
    } catch (err) { mensaje.textContent = err.message; }
  };
  await cargar();
}
