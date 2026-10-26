import { api } from '../utils/api.js';
import { getClient, setClient, removeClient } from '../utils/clientAuth.js';
import { fechaHoraEnTienda } from '../utils/fechaTienda.js';
import { crearRevisionPedido, crearIntentoPendiente, leerIntentoPendiente, clasificarErrorPedido } from '../utils/pedidoPendiente.js';

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
      setClient(slug, { id: cliente.id, nombre: cliente.nombre, telefono: cliente.telefono, sesionDocumentos: cliente.sesionDocumentos });
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

      <div id="aviso-pedido-anterior" class="page-padding hidden" role="status"></div>
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
  const pendienteKey = `qvd_pdf_pedido_pendiente_${slug}_${cliente.id}`;
  const avisoAnterior = container.querySelector('#aviso-pedido-anterior');
  const seleccion = { modalidad: opciones.recepcionDisponible ? (opciones.alMomentoDisponible ? 'AlMomento' : (tienda.operacion.pedidosProgramados ? 'Programado' : null)) : null,
    entrega: tienda.operacion.recoleccion && tienda.operacion.domicilio ? '' : tienda.operacion.recoleccion ? 'Recoleccion' : 'Domicilio', fecha: '', ventana: '' };

  const actualizarAviso = () => mostrarIntentoAnterior(avisoAnterior, pendienteKey, cliente, slug,
    () => switchTab('pedidos'), tipo => {
      if (tipo === 'pdf_vencido') {
        for (const [clave, partida] of Object.entries(carrito))
          if (partida.documentoPdfId) delete carrito[clave];
        container.querySelectorAll('[data-pdf="1"]').forEach(card => card.dispatchEvent(new Event('pdf-reset')));
        const contenido = container.querySelector('#client-content');
        if (contenido.querySelector('#carrito-bar')) actualizarBarra(contenido, carrito);
      }
    });

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
    if (tab === 'menu') renderMenuTab(content, slug, cliente, carrito, (c) => { carrito = c; }, tienda, opciones, seleccion, pendienteKey, actualizarAviso);
    else if (tab === 'pedidos') renderPedidosTab(content, slug, cliente, tienda.operacion.zonaHoraria);
    else if (tab === 'cuenta') renderCuentaTab(container, slug, tienda, cliente);
  }

  container.querySelectorAll('.nav-item').forEach(btn => {
    btn.addEventListener('click', () => switchTab(btn.dataset.tab));
  });

  switchTab('menu');
  actualizarAviso();
}

function mostrarIntentoAnterior(contenedor, clave, cliente, slug, verPedidos, alRechazar) {
  const estado = leerIntentoPendiente(sessionStorage.getItem(clave), cliente.telefono);
  if (estado.tipo === 'ninguno') { contenedor.classList.add('hidden'); contenedor.innerHTML = ''; return; }
  contenedor.classList.remove('hidden');
  const intento = estado.intento;
  const p = intento?.payload;
  const partidas = intento?.partidas || [];
  const detalle = p ? `<details class="text-sm"><summary class="cursor-pointer font-medium text-primary">Revisar el envío anterior</summary>
    <div class="mt-2 space-y-1 text-text-secondary">
      <p>${p.modalidad === 'Programado' ? 'Programado' : 'Al momento'} · ${escapeHtml(p.fechaServicio)}${p.ventanaSolicitada ? ` · ${escapeHtml(p.ventanaSolicitada)}` : ''} · ${p.entrega === 'Domicilio' ? 'Domicilio' : 'Recolección'}</p>
      ${partidas.map(i => `<p>${escapeHtml(i.nombre || `Producto #${i.id}`)} ×${escapeHtml(i.cantidad)}${i.nombrePdf ? ` · ${escapeHtml(i.nombrePdf)}${i.paginas ? ` · páginas ${escapeHtml(i.paginas)}` : ''}${i.juegos ? ` · ${escapeHtml(i.juegos)} juegos` : ''}${i.dobleCara ? ' · ambos lados' : ' · un lado'}` : ''}</p>`).join('')}
      <p>Pago: ${escapeHtml(p.formaPago)}${p.observaciones ? ` · Nota: ${escapeHtml(p.observaciones)}` : ''}</p>
      <p class="font-semibold text-primary">Importe: $${Number(p.totalEsperado).toFixed(2)}</p>
    </div></details>` : '';
  contenedor.innerHTML = `<div class="card-elevated space-y-3">
    <p class="font-semibold text-primary">Envío anterior sin resultado confirmado</p>
    <p class="text-sm">${intento ? 'No pudimos confirmar si se registró tu pedido. Consulta o reintenta el envío anterior antes de crear otro. Se enviarán exactamente los datos anteriores sin duplicar un pedido registrado.' : 'No se pudo leer el envío anterior guardado en este navegador. Para evitar duplicados, verifica con la tienda si se registró.'}</p>
    ${detalle}
    ${intento ? '<button type="button" class="btn-accent comprobar-anterior">Consultar o reintentar envío anterior</button>' : ''}
    <p class="estado-anterior text-sm" role="status"></p>
    <details class="text-sm"><summary class="cursor-pointer">Si la tienda confirmó que no existe el pedido</summary>
      <p class="my-2">Solo después de verificarlo con la tienda puedes liberar este navegador para crear otro pedido.</p>
      <button type="button" class="btn-ghost liberar-anterior">La tienda confirmó que no se registró</button>
    </details>
  </div>`;
  const mensaje = contenedor.querySelector('.estado-anterior');
  contenedor.querySelector('.liberar-anterior').addEventListener('click', () => {
    if (window.confirm('Confirma solo si la tienda verificó que este pedido NO se registró. Si existe, crear otro puede duplicarlo.')) {
      sessionStorage.removeItem(clave);
      contenedor.innerHTML = ''; contenedor.classList.add('hidden');
    }
  });
  contenedor.querySelector('.comprobar-anterior')?.addEventListener('click', async event => {
    const boton = event.currentTarget; boton.disabled = true;
    if (intento.payload.items.some(i => i.documentoPdfId) && !cliente.sesionDocumentos) {
      mensaje.textContent = 'Falta la sesión de documentos de este envío. Conservamos los datos; consulta a la tienda antes de crear otro pedido.';
      boton.disabled = false;
      return;
    }
    mensaje.textContent = 'Comprobando el envío anterior…';
    try {
      const pedido = await api.public.crearPedido(slug, intento.payload, cliente.sesionDocumentos);
      sessionStorage.removeItem(clave);
      contenedor.innerHTML = `<div class="card-elevated space-y-2"><p class="font-semibold text-success">Pedido #${pedido.id} registrado.</p><p class="text-sm">El envío anterior ya está registrado. Tu captura actual no se envió de nuevo.</p><button type="button" class="btn-ghost ver-pedido-anterior">Ver Mis pedidos</button></div>`;
      contenedor.querySelector('.ver-pedido-anterior').addEventListener('click', verPedidos);
    } catch (error) {
      const tipo = clasificarErrorPedido(error);
      if (tipo === 'horario' || tipo === 'rechazo' || tipo === 'pdf_vencido') {
        sessionStorage.removeItem(clave);
        alRechazar(tipo);
        const explicacion = tipo === 'pdf_vencido' ? 'El archivo temporal venció. Adjunta el PDF nuevamente y revisa otro pedido.'
          : tipo === 'horario' ? 'Este intento fue rechazado por el horario. Elige otra fecha y revisa nuevamente tu pedido.'
            : `Este intento fue rechazado sin registrar pedido: ${error.message}. Corrige la captura y revísala de nuevo.`;
        contenedor.innerHTML = `<div class="card-elevated text-sm text-error" role="status">${escapeHtml(explicacion)}</div>`;
      } else {
        mensaje.textContent = tipo === 'sesion' ? 'La sesión de documentos no permite comprobar este intento. Consérvalo y consulta a la tienda; no se borraron tus datos.'
          : tipo === 'conflicto' ? 'Este envío no coincide con el registro anterior. No se creó otro pedido; consulta a la tienda antes de continuar.'
            : 'Aún no pudimos confirmar el resultado. Conservamos el envío anterior para consultarlo o reintentarlo sin duplicarlo.';
        boton.disabled = false;
      }
    }
  });
}

async function renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion, pendienteKey, actualizarAviso) {
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
        <p id="aviso-opciones" class="text-sm text-error" role="status"></p>
      </div>`;
    if (productos.length === 0) {
      content.innerHTML = `<div class="page-padding">${selector}<div class="empty-state"><h3>${!seleccion.entrega ? 'Selecciona la forma de entrega' : necesitaFecha && !fechaMenu ? 'Selecciona una fecha' : 'Sin productos para esta fecha'}</h3><p>${!seleccion.entrega ? 'Así verás los productos y precios correctos.' : necesitaFecha && !fechaMenu ? 'Elige una fecha de servicio para ver el catálogo.' : 'Elige otra fecha de servicio.'}</p></div></div>`;
      bindSeleccion();
      return;
    }

    content.innerHTML = `
      <div class="page-padding catalogo-pagina">
        ${selector}
        <div class="space-y-3">
          ${productos.map((p, i) => {
            const agotado = p.tieneInventario && (p.stock ?? 0) <= 0;
            return `
                  <div class="card animate-fade-in" style="animation-delay:${i * 30}ms" data-product-id="${p.id}" data-nombre="${escapeHtml(p.nombre)}" data-precio="${p.precio}" data-papel="${p.consumePapel ? '1' : '0'}" data-pdf="${p.requierePdf ? '1' : '0'}" data-revision="${p.revisionImpresion || ''}" data-doble="${p.admiteDobleCara ? '1' : '0'}" ${agotado ? 'data-agotado="1"' : ''}>
                    <div class="min-w-0 flex-1 mr-4">
                      <h3 class="font-semibold text-primary text-[0.9375rem] break-words">${escapeHtml(p.nombre)}</h3>
                      ${p.descripcion ? `<p class="text-sm text-text-secondary mt-0.5 leading-relaxed break-words">${escapeHtml(p.descripcion)}</p>` : ''}
                      <div class="flex flex-wrap items-center gap-2 mt-2">
                        <span class="inline-block font-bold text-accent text-lg">$${p.precio.toFixed(2)}</span>
                        ${p.consumePapel ? '<span class="text-sm">por cara · papel sujeto a disponibilidad</span>' : ''}
                        ${p.tieneInventario
                          ? `<span class="badge ${agotado ? 'bg-error/10 text-error' : 'bg-surface text-text-secondary'}">
                              ${agotado ? '<i data-lucide="package-x" class="w-3.5 h-3.5"></i> Agotado' : `<i data-lucide="package" class="w-3.5 h-3.5"></i> ${p.stock} disponibles`}
                            </span>`
                          : ''}
                      </div>
                    </div>
                    <div class="flex flex-col items-center gap-2">
                      ${p.requierePdf ? `<div class="space-y-2 text-sm w-full"><p>Blanco y negro, papel carta. El contenido se ajustará al área imprimible conservando sus proporciones.</p><p>Los PDF se eliminan ${opciones.documentos?.diasRetencion ?? 30} días después de entregar o cancelar el pedido; cargas sin pedido vencen en ${opciones.documentos?.horasTemporal ?? 24} horas.</p><label>PDF obligatorio<input class="input-field pdf-archivo" type="file" accept=".pdf,application/pdf"></label><button class="btn-ghost pdf-subir" type="button">Validar y subir PDF</button><p class="pdf-estado" role="status">${cliente.sesionDocumentos ? 'Sube un PDF para contar sus páginas.' : 'Sal e ingresa de nuevo para activar la sesión segura de documentos.'}</p><label>Páginas<select class="input-field pdf-modo"><option value="todas">Todas las páginas</option><option value="rango">Rango</option></select></label><input class="input-field pdf-rango hidden" placeholder="1-3,5,8-10" aria-label="Rango de páginas"><p>La selección se normaliza en orden ascendente sin duplicados y se imprime en ese orden.</p><label>Juegos<input class="input-field pdf-juegos" type="number" min="1" max="100000" step="1" value="1"></label><label>Lados<select class="input-field pdf-lados"><option value="uno">Un lado</option>${p.admiteDobleCara ? '<option value="ambos">Ambos lados</option>' : ''}</select></label><p class="pdf-preview">Adjunta el PDF para calcular.</p><button class="btn-accent pdf-agregar" type="button">Agregar impresión</button></div>` : p.consumePapel ? `<div class="space-y-2 text-sm"><label>Caras por juego<input class="input-field papel-caras" type="number" min="1" max="100000" step="1" value="1"></label><label>Juegos<input class="input-field papel-juegos" type="number" min="1" max="100000" step="1" value="1"></label><label>Lados<select class="input-field papel-lados"><option value="uno">Un lado</option>${p.admiteDobleCara ? '<option value="ambos">Ambos lados</option>' : ''}</select></label><p class="papel-preview">1 cara · 1 hoja · $${p.precio.toFixed(2)}</p><button class="btn-accent papel-agregar" type="button">Agregar servicio</button></div>` : `
                      <div class="flex items-center gap-2 ${agotado ? 'opacity-40 pointer-events-none' : ''}">
                    <button class="qty-btn minus" data-id="${p.id}">
                      <i data-lucide="minus" class="w-4 h-4"></i>
                    </button>
                    <span class="qty-value w-8 text-center font-bold text-primary text-[0.9375rem]">${carrito[p.id]?.cantidad || 0}</span>
                    <button class="qty-btn plus" data-id="${p.id}">
                      <i data-lucide="plus" class="w-4 h-4"></i>
                    </button>
                  </div>`}
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
          <p id="detalle-revision" class="text-sm text-text-secondary mb-3"></p>
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
          <p id="estado-envio" class="text-sm mt-3" role="status"></p>
        </div>
      </div>
    `;

    if (window.lucide) lucide.createIcons();
    bindSeleccion();
    setupCarrito(content, slug, cliente, carrito, updateCarrito, seleccion, opciones, pendienteKey, actualizarAviso);
    function bindSeleccion() {
      const entrega = content.querySelector('#entrega-catalogo');
      entrega.value = seleccion.entrega;
      entrega.addEventListener('change', () => {
        seleccion.entrega = entrega.value;
        Object.keys(carrito).forEach(k => delete carrito[k]);
        renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion, pendienteKey, actualizarAviso);
      });
      const modalidad = content.querySelector('#modalidad-pedido');
      modalidad.value = seleccion.modalidad;
      modalidad.addEventListener('change', () => {
        seleccion.modalidad = modalidad.value;
        seleccion.fecha = ''; seleccion.ventana = '';
        Object.keys(carrito).forEach(k => delete carrito[k]);
        renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion, pendienteKey, actualizarAviso);
      });
      const fecha = content.querySelector('#fecha-servicio');
      if (fecha) {
        fecha.value = seleccion.fecha;
        fecha.addEventListener('change', () => {
          seleccion.fecha = fecha.value; seleccion.ventana = '';
          renderMenuTab(content, slug, cliente, carrito, updateCarrito, tienda, opciones, seleccion, pendienteKey, actualizarAviso);
        });
      }
      const ventana = content.querySelector('#ventana-servicio');
      if (ventana) { ventana.value = seleccion.ventana; ventana.addEventListener('change', () => { seleccion.ventana = ventana.value; }); }
    }
  }
}

function setupCarrito(content, slug, cliente, carrito, updateCarrito, seleccion, opciones, pendienteKey, actualizarAviso) {
  content.querySelectorAll('[data-pdf="1"]').forEach(card => {
    let documento = null;
    const archivo = card.querySelector('.pdf-archivo'), estado = card.querySelector('.pdf-estado');
    const modo = card.querySelector('.pdf-modo'), rango = card.querySelector('.pdf-rango');
    const juegos = card.querySelector('.pdf-juegos'), lados = card.querySelector('.pdf-lados');
    const preview = card.querySelector('.pdf-preview');
    card.addEventListener('pdf-reset', () => {
      documento = null; archivo.value = '';
      estado.textContent = 'El archivo temporal venció. Adjunta el PDF nuevamente.';
      preview.textContent = 'Adjunta el PDF para calcular.';
    });
    archivo.addEventListener('change', () => {
      documento = null;
      estado.textContent = 'Valida y sube el PDF seleccionado antes de agregarlo.';
      preview.textContent = 'Adjunta el PDF para calcular.';
    });
    const calcular = () => {
      if (!documento) return null;
      const j = Number(juegos.value);
      let paginas;
      try {
        if (modo.value === 'todas') paginas = Array.from({ length: documento.paginas }, (_, i) => i + 1);
        else {
          paginas = new Set();
          for (const parte of rango.value.split(',')) {
            const limites = parte.trim().split('-');
            if (limites.length > 2 || !limites.every(x => /^\d+$/.test(x))) throw Error();
            const a = Number(limites[0]), b = Number(limites.at(-1));
            if (a < 1 || b > documento.paginas || a > b) throw Error();
            for (let n = a; n <= b; n++) paginas.add(n);
          }
          paginas = [...paginas].sort((a, b) => a - b);
        }
        if (!paginas.length || !Number.isSafeInteger(j) || j < 1 || paginas.length * j > 100000) throw Error();
      } catch { preview.textContent = 'Revisa las páginas y los juegos.'; return null; }
      const caras = paginas.length * j;
      const hojas = (lados.value === 'ambos' ? Math.ceil(paginas.length / 2) : paginas.length) * j;
      preview.textContent = `${paginas.length} caras/juego · ${j} juegos · ${caras} caras cobradas · ${hojas} hojas · $${(Number(card.dataset.precio) * caras).toFixed(2)}`;
      return { caras, hojas, paginas: modo.value === 'todas' ? 'todas' : paginas.join(','), juegos: j, dobleCara: lados.value === 'ambos' };
    };
    card.querySelector('.pdf-subir').addEventListener('click', async () => {
      if (!cliente.sesionDocumentos || !archivo.files[0]) { estado.textContent = 'Selecciona un PDF e inicia sesión de nuevo si es necesario.'; return; }
      const boton = card.querySelector('.pdf-subir'); boton.disabled = true;
      try {
        documento = await api.documentos.subir(slug, archivo.files[0], cliente.sesionDocumentos);
        estado.textContent = `${documento.nombre} · ${(documento.tamano / 1048576).toFixed(2)} MiB · ${documento.paginas} páginas`;
        calcular();
      } catch (e) { documento = null; estado.textContent = e.message; }
      finally { boton.disabled = false; }
    });
    modo.addEventListener('change', () => { rango.classList.toggle('hidden', modo.value !== 'rango'); calcular(); });
    for (const campo of [rango, juegos, lados]) campo.addEventListener('input', calcular);
    card.querySelector('.pdf-agregar').addEventListener('click', () => {
      const c = calcular();
      if (!c) return;
      const clave = `${card.dataset.productId}:${documento.id}`;
      carrito[clave] = { id: card.dataset.productId, nombre: card.dataset.nombre, precio: Number(card.dataset.precio),
        cantidad: c.caras, carasPorJuego: c.caras / c.juegos, juegos: c.juegos, dobleCara: c.dobleCara, hojas: c.hojas,
        documentoPdfId: documento.id, nombrePdf: documento.nombre, paginas: c.paginas,
        paginasTotalesPdf: documento.paginas, revisionImpresion: card.dataset.revision };
      updateCarrito(carrito); actualizarBarra(content, carrito);
      estado.textContent = 'Impresión agregada al pedido.';
    });
  });
  content.querySelectorAll('[data-papel="1"]:not([data-pdf="1"])').forEach(card => {
    const caras = card.querySelector('.papel-caras');
    const juegos = card.querySelector('.papel-juegos');
    const lados = card.querySelector('.papel-lados');
    const preview = card.querySelector('.papel-preview');
    const calcular = () => {
      const c = Number(caras.value), j = Number(juegos.value);
      if (!Number.isSafeInteger(c) || !Number.isSafeInteger(j) || c <= 0 || j <= 0 || c * j > 100000) {
        preview.textContent = 'Captura enteros positivos; máximo 100000 caras.'; return null;
      }
      const cantidad = c * j;
      const hojas = (lados.value === 'ambos' ? Math.ceil(c / 2) : c) * j;
      preview.textContent = `${cantidad} caras · ${hojas} hojas · $${(Number(card.dataset.precio) * cantidad).toFixed(2)}`;
      return { cantidad, hojas, carasPorJuego: c, juegos: j, dobleCara: lados.value === 'ambos' };
    };
    for (const campo of [caras, juegos, lados]) campo.addEventListener('input', calcular);
    card.querySelector('.papel-agregar').addEventListener('click', () => {
      const resultado = calcular();
      if (!resultado) return;
      const id = card.dataset.productId;
      carrito[id] = { id, nombre: card.dataset.nombre, precio: Number(card.dataset.precio), ...resultado };
      updateCarrito(carrito);
      actualizarBarra(content, carrito);
    });
  });
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
  const estadoEnvio = content.querySelector('#estado-envio');
  const avisoOpciones = content.querySelector('#aviso-opciones');
  let revision = null;
  let enviando = false;
  const informar = (mensaje, error = false) => {
    estadoEnvio.textContent = mensaje;
    estadoEnvio.className = `text-sm mt-3 ${error ? 'text-error' : 'text-primary'}`;
  };
  const refrescarOpciones = async () => {
    const frescas = await api.public.getOpciones(slug);
    Object.assign(opciones, frescas);
    if (seleccion.modalidad === 'Programado') {
      const fecha = content.querySelector('#fecha-servicio');
      const ventana = content.querySelector('#ventana-servicio');
      const dias = frescas.fechas || [];
      const dia = dias.find(d => d.fecha === seleccion.fecha);
      fecha.innerHTML = '<option value="">Selecciona fecha</option>' +
        dias.map(d => `<option value="${escapeHtml(d.fecha)}">${escapeHtml(d.fecha)}</option>`).join('') +
        (seleccion.fecha && !dia ? `<option value="${escapeHtml(seleccion.fecha)}" disabled>${escapeHtml(seleccion.fecha)} · ya no disponible</option>` : '');
      fecha.value = seleccion.fecha || '';
      ventana.innerHTML = '<option value="">Selecciona ventana</option>' +
        (dia?.ventanas || []).map(v => `<option value="${escapeHtml(v)}">${escapeHtml(v)}</option>`).join('') +
        (seleccion.ventana && !dia?.ventanas?.includes(seleccion.ventana)
          ? `<option value="${escapeHtml(seleccion.ventana)}" disabled>${escapeHtml(seleccion.ventana)} · ya no disponible</option>` : '');
      ventana.value = seleccion.ventana || '';
    }
    return frescas;
  };
  const fechaValida = (r, frescas) => r.modalidad === 'AlMomento'
    ? frescas.alMomentoDisponible && r.fechaServicio === frescas.hoy
    : (frescas.fechas || []).some(d => d.fecha === r.fechaServicio && d.ventanas.includes(r.ventanaSolicitada));
  const actualizarPrecios = async () => {
    const fecha = seleccion.modalidad === 'Programado' ? seleccion.fecha : opciones.hoy;
    const productos = await api.public.getMenu(slug, fecha, seleccion.modalidad, seleccion.entrega);
    for (const [clave, partida] of Object.entries(carrito)) {
      const producto = productos.find(p => p.id === Number(partida.id));
      if (!producto || (partida.documentoPdfId && producto.revisionImpresion !== partida.revisionImpresion)) {
        delete carrito[clave];
        continue;
      }
      partida.precio = producto.precio;
      const card = content.querySelector(`[data-product-id="${partida.id}"]`);
      if (card) {
        card.dataset.precio = producto.precio;
        const precio = card.querySelector('.text-accent.text-lg');
        if (precio) precio.textContent = `$${Number(producto.precio).toFixed(2)}`;
      }
    }
    actualizarBarra(content, carrito);
  };
  const detalleRevision = () => {
    const pago = content.querySelector('.pay-option.active')?.dataset.value || 'Efectivo';
    const nota = content.querySelector('#observaciones').value.trim();
    const direccion = content.querySelector('#direccion-entrega').value.trim();
    content.querySelector('#detalle-revision').textContent =
      `Pago: ${pago}${nota ? ` · Nota: ${nota}` : ''}${revision?.entrega === 'Domicilio' ? ` · Dirección: ${direccion || 'pendiente'}` : ''}`;
    return { pago, nota, direccion };
  };
  const syncDireccion = () => {
    const domicilio = seleccion.entrega === 'Domicilio';
    content.querySelector('#direccion-entrega-wrap').classList.toggle('hidden', !domicilio);
    content.querySelector('#direccion-entrega').required = domicilio;
  };
  syncDireccion();

  btnHacerPedido?.addEventListener('click', async () => {
    if (btnHacerPedido.disabled) return;
    btnHacerPedido.disabled = true;
    avisoOpciones.textContent = '';
    try {
      const frescas = await refrescarOpciones();
      const propuesta = crearRevisionPedido(carrito, seleccion, frescas.hoy);
      if (!propuesta.items.length) return;
      if (!frescas.recepcionDisponible || !fechaValida(propuesta, frescas)) {
        avisoOpciones.textContent = 'La fecha o ventana ya no está disponible. Elige otra y revisa nuevamente tu pedido.';
        return;
      }
      revision = propuesta;
      mostrarResumen(content, revision.partidas);
      content.querySelector('#resumen-pedido').insertAdjacentHTML('beforeend', `<p class="text-sm text-primary">${revision.modalidad === 'Programado' ? `Horario solicitado: ${escapeHtml(revision.fechaServicio)} ${escapeHtml(revision.ventanaSolicitada)}` : `Al momento: ${escapeHtml(revision.fechaServicio)}. La tienda confirmará el horario estimado.`}</p><p class="text-sm text-text-secondary">${revision.entrega === 'Domicilio' ? 'Entrega a domicilio' : 'Recolección en tienda'} · pendiente de confirmación.</p>`);
      detalleRevision(); informar('Revisa los datos antes de confirmar.');
      modal.classList.remove('hidden');
    } catch {
      avisoOpciones.textContent = 'No pudimos actualizar los horarios. Revisa tu conexión y vuelve a abrir el resumen.';
    } finally { btnHacerPedido.disabled = false; }
  });

  btnCancelar?.addEventListener('click', () => {
    modal.classList.add('hidden');
  });

  content.querySelectorAll('.pay-option').forEach(btn => {
    btn.addEventListener('click', () => {
      content.querySelectorAll('.pay-option').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      detalleRevision();
    });
  });
  for (const campo of ['#observaciones', '#direccion-entrega'])
    content.querySelector(campo).addEventListener('input', detalleRevision);

  btnConfirmar?.addEventListener('click', async () => {
    if (enviando || !revision?.items.length) return;
    const anterior = leerIntentoPendiente(sessionStorage.getItem(pendienteKey), cliente.telefono);
    if (anterior.tipo !== 'ninguno') {
      informar('Consulta el envío anterior antes de crear otro pedido. Tu resumen actual no se enviará.', true);
      actualizarAviso();
      return;
    }
    const campos = detalleRevision();
    if (revision.entrega === 'Domicilio' && !campos.direccion) {
      informar('Captura la dirección de entrega y revisa nuevamente.', true); return;
    }
    if (revision.items.some(i => i.documentoPdfId) && !cliente.sesionDocumentos) {
      informar('La sesión de documentos no está disponible. Conserva tu captura y vuelve a identificarte antes de enviar.', true);
      return;
    }
    enviando = true; btnConfirmar.disabled = true;
    let intento = null;
    try {
      const frescas = await refrescarOpciones();
      if (!frescas.recepcionDisponible || !fechaValida(revision, frescas) ||
          revision.modalidad !== seleccion.modalidad || revision.entrega !== seleccion.entrega ||
          (revision.modalidad === 'Programado' && (revision.fechaServicio !== seleccion.fecha || revision.ventanaSolicitada !== seleccion.ventana))) {
        informar('Cambió la fecha o ventana disponible. Elige una opción vigente y abre nuevamente el resumen.', true);
        avisoOpciones.textContent = 'Elige una fecha y ventana vigentes; tu carrito se conserva.';
        modal.classList.add('hidden'); revision = null;
        return;
      }
      const datos = {
        claveIdempotencia: crypto.randomUUID(), totalEsperado: revision.totalEsperado,
        telefonoCliente: cliente.telefono, formaPago: campos.pago,
        observaciones: campos.nota || null, modalidad: revision.modalidad,
        entrega: revision.entrega, direccionEntrega: revision.entrega === 'Domicilio' ? campos.direccion : null,
        fechaServicio: revision.fechaServicio, ventanaSolicitada: revision.ventanaSolicitada,
        items: revision.items
      };
      const preparado = crearIntentoPendiente(datos, revision.partidas);
      sessionStorage.setItem(pendienteKey, JSON.stringify(preparado));
      intento = preparado;
      informar('Enviando el pedido…');
      const pedido = await api.public.crearPedido(slug, intento.payload, cliente.sesionDocumentos);
      sessionStorage.removeItem(pendienteKey); actualizarAviso();
      modal.classList.add('hidden');
      content.innerHTML = `<div class="min-h-screen flex items-center justify-center p-8"><div class="text-center animate-scale-in">
        <div class="w-20 h-20 rounded-full bg-success/15 mx-auto mb-5 flex items-center justify-center"><i data-lucide="check" class="w-10 h-10 text-success"></i></div>
        <h1 class="text-2xl font-bold text-primary mb-2">¡Pedido #${pedido.id} enviado!</h1>
        <p class="text-text-secondary mb-8 leading-relaxed">Importe registrado: $${Number(pedido.total).toFixed(2)}.<br>Espera confirmación de la tienda.</p>
        <button id="btn-volver-menu" class="btn-primary"><i data-lucide="utensils" class="w-5 h-5"></i> Volver al Menú</button>
        </div></div>`;
      if (window.lucide) lucide.createIcons();
      content.querySelector('#btn-volver-menu').addEventListener('click', () => {
        Object.keys(carrito).forEach(k => delete carrito[k]); updateCarrito(carrito);
        content.closest('#app').querySelector('[data-tab="menu"]')?.click();
      });
    } catch (error) {
      if (!intento) {
        informar('No se envió el pedido. No pudimos comprobar horarios o guardar el intento; revisa la conexión e inténtalo de nuevo.', true);
        return;
      }
      const tipo = clasificarErrorPedido(error);
      if (tipo === 'horario' || tipo === 'rechazo' || tipo === 'pdf_vencido') {
        sessionStorage.removeItem(pendienteKey); actualizarAviso();
        if (tipo === 'pdf_vencido') {
          for (const [clave, partida] of Object.entries(carrito)) if (partida.documentoPdfId) delete carrito[clave];
          content.querySelectorAll('[data-pdf="1"]').forEach(card => card.dispatchEvent(new Event('pdf-reset')));
          actualizarBarra(content, carrito);
        }
        if (error.code === 'precio_actualizado') {
          try { await actualizarPrecios(); }
          catch { /* La captura sigue disponible; se consultará el catálogo al revisar. */ }
        }
        const mensaje = tipo === 'pdf_vencido' ? 'El archivo temporal venció. Adjunta el PDF nuevamente; conservamos las demás partidas.'
          : tipo === 'horario' ? 'Este intento fue rechazado por el horario. Elige otra fecha y revisa de nuevo; tu carrito se conserva.'
            : error.code === 'precio_actualizado' ? 'Cambió el precio. Actualizamos el carrito disponible; revisa el nuevo importe antes de confirmar. Si faltaba una impresión, agrégala de nuevo.'
            : `El pedido no se registró: ${error.message}. Corrige los datos y vuelve a revisar.`;
        informar(mensaje, true);
        modal.classList.add('hidden'); revision = null;
        avisoOpciones.textContent = mensaje;
      } else {
        actualizarAviso();
        informar(tipo === 'sesion' ? 'La sesión de documentos no permite comprobar el resultado. Conservamos el envío anterior; consulta a la tienda.'
          : tipo === 'conflicto' ? 'El envío no coincide con el intento anterior. Conservamos sus datos; consulta a la tienda.'
            : 'No pudimos confirmar si se registró. Consulta o reintenta el envío anterior antes de crear otro.', true);
      }
    } finally { enviando = false; if (content.contains(btnConfirmar)) btnConfirmar.disabled = false; }
  });

  actualizarBarra(content, carrito);
}

function actualizarBarra(content, carrito) {
  const items = Object.values(carrito);
  const total = items.reduce((sum, c) => sum + (c.precio * c.cantidad), 0);
  const count = items.reduce((sum, c) => sum + c.cantidad, 0);

  const bar = content.querySelector('#carrito-bar');
  content.querySelector('.catalogo-pagina')?.classList.toggle('catalogo-con-carrito', count > 0);
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
      <span class="text-primary">${escapeHtml(c.nombre)} <span class="text-text-secondary">×${c.cantidad}${c.nombrePdf ? ` · ${escapeHtml(c.nombrePdf)} (${c.paginasTotalesPdf} páginas; selección ${escapeHtml(c.paginas)}), ${c.juegos} juegos, ${c.dobleCara ? 'ambos lados' : 'un lado'}, ${c.hojas} hojas · blanco y negro, carta, ajuste proporcional` : c.carasPorJuego ? ` · ${c.carasPorJuego} caras/juego, ${c.juegos} juegos, ${c.dobleCara ? 'ambos lados' : 'un lado'}, ${c.hojas} hojas` : ''}</span></span>
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

  api.public.getPedidosCliente(slug, cliente.telefono, cliente.sesionDocumentos).then(pedidos => {
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
              <span class="text-primary">${escapeHtml(i.productoNombre)} <span class="text-text-secondary">×${i.cantidad}${i.nombrePdf ? ` · ${escapeHtml(i.nombrePdf)} (${i.paginasTotalesPdf} páginas; selección ${escapeHtml(i.paginasSeleccionadas)}), ${i.juegos} juegos, ${i.dobleCara ? 'ambos lados' : 'un lado'}, ${i.hojasConsumidas} hojas · blanco y negro, carta, ajuste proporcional` : i.carasPorJuego ? ` · ${i.carasPorJuego} caras/juego, ${i.juegos} juegos, ${i.dobleCara ? 'ambos lados' : 'un lado'}` : ''}</span></span>
              <span class="font-medium text-primary">$${i.subtotal.toFixed(2)}</span>
            </div>
            ${i.documentoPdfId ? `<div>${i.archivoPdfDisponible ? `<button class="btn-ghost cliente-descargar-pdf" data-id="${i.documentoPdfId}" data-nombre="${escapeHtml(i.nombrePdf)}">Descargar PDF</button>` : '<span class="text-sm">Archivo eliminado por política de retención</span>'}</div>` : ''}
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
    content.querySelectorAll('.cliente-descargar-pdf').forEach(btn => btn.addEventListener('click', async () => {
      try { await api.documentos.descargar(`/api/documentos/tienda/${encodeURIComponent(slug)}/${btn.dataset.id}`, btn.dataset.nombre, cliente.sesionDocumentos); }
      catch (err) { alert(err.message); }
    }));
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
