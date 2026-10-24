import { api } from '../utils/api.js';
import { getAuth, clearAuth } from '../utils/auth.js';
import { navigate } from '../utils/router.js';

const dias = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
const diasApi = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const textoVentanas = (ventanas = []) => ventanas.map(v => `${dias[typeof v.dia === 'number' ? v.dia : diasApi.indexOf(v.dia)]} ${v.inicio}-${v.fin}`).join('\n');
function leerVentanas(texto) {
  return texto.split('\n').map(linea => linea.trim()).filter(Boolean).map(linea => {
    const match = linea.match(/^(.+?)\s+(\d{2}:\d{2})-(\d{2}:\d{2})$/);
    const dia = match && dias.findIndex(d => d.toLowerCase() === match[1].toLowerCase());
    if (!match || dia < 0) throw new Error('Usa una línea por ventana: Lunes 09:00-17:00');
    return { dia, inicio: match[2], fin: match[3] };
  });
}

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

export function renderTienda(container, params) {
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
          <h1 class="text-xl font-bold mt-0.5">Panel de Control</h1>
        </div>
        <button id="btn-logout" class="btn-icon bg-white/10 text-white/80 hover:bg-white/20">
          <i data-lucide="log-out" class="w-5 h-5"></i>
        </button>
      </div>
      <main class="page-padding page-padding-bottom" style="margin-top:-8px">
        <div id="tienda-content">
          <div class="space-y-3">
            <div class="skeleton h-28 w-full"></div>
            <div class="skeleton h-20 w-full"></div>
            <div class="skeleton h-20 w-full"></div>
          </div>
        </div>
      </main>
      ${renderAdminNav('tienda')}
    </div>
  `;

  if (window.lucide) lucide.createIcons();

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
      <div class="space-y-4 animate-fade-in">
        <div class="card-elevated">
          <div class="flex justify-between items-start mb-4">
            <div>
              <h2 class="text-lg font-bold text-primary">${tienda.nombre}</h2>
              <div class="flex items-center gap-1.5 mt-1">
                <span class="badge badge-accent">/${tienda.slug}</span>
              </div>
            </div>
            <button id="btn-editar" class="btn-ghost text-sm">
              <i data-lucide="pencil" class="w-4 h-4"></i> Editar
            </button>
          </div>
          ${tienda.direccion ? `
            <div class="flex items-center gap-2 text-sm text-text-secondary mb-1.5">
              <i data-lucide="map-pin" class="w-4 h-4 text-accent"></i>
              ${tienda.direccion}
            </div>
          ` : ''}
          ${tienda.telefono ? `
            <div class="flex items-center gap-2 text-sm text-text-secondary">
              <i data-lucide="phone" class="w-4 h-4 text-accent"></i>
              ${tienda.telefono}
            </div>
          ` : ''}
        </div>

        <div class="card">
          <div class="flex items-center gap-2 mb-3">
            <i data-lucide="link" class="w-4 h-4 text-accent"></i>
            <h3 class="font-semibold text-primary text-[0.9375rem]">Link de tu tienda</h3>
          </div>
          <div class="flex items-center gap-2">
            <code class="bg-surface text-primary text-[0.8125rem] px-3 py-2.5 rounded-2xl flex-1 overflow-x-auto font-medium">/tienda/${tienda.slug}</code>
            <button id="btn-copiar" class="btn-accent text-sm px-4 py-2.5 whitespace-nowrap">
              <i data-lucide="copy" class="w-4 h-4"></i> Copiar
            </button>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <a href="/inventario" data-link class="card flex flex-col items-center gap-2 py-5 text-center"><span class="font-semibold text-primary text-sm">Inventario y ventas</span></a>
          ${getAuth()?.rol === 'Admin' ? '<a href="/mostrador" data-link class="card flex flex-col items-center gap-2 py-5 text-center"><span class="font-semibold text-primary text-sm">Venta de mostrador</span></a>' : ''}
          <a href="/menu" data-link class="card flex flex-col items-center gap-2 py-5 text-center">
            <div class="w-12 h-12 rounded-2xl bg-accent/15 flex items-center justify-center">
              <i data-lucide="utensils" class="w-6 h-6 text-primary"></i>
            </div>
            <span class="font-semibold text-primary text-sm">Menú</span>
          </a>
          <a href="/pedidos" data-link class="card flex flex-col items-center gap-2 py-5 text-center">
            <div class="w-12 h-12 rounded-2xl bg-accent/15 flex items-center justify-center">
              <i data-lucide="clipboard-list" class="w-6 h-6 text-primary"></i>
            </div>
            <span class="font-semibold text-primary text-sm">Pedidos</span>
          </a>
        </div>

        <div class="card-elevated">
          <h3 class="font-semibold text-primary mb-3">Cómo recibe y entrega pedidos</h3>
          <form id="form-operacion" class="space-y-3 text-sm">
            <label class="block"><input type="checkbox" id="op-momento" ${tienda.operacion.pedidosAlMomento ? 'checked' : ''}> Pedidos al momento</label>
            <label class="block"><input type="checkbox" id="op-programados" ${tienda.operacion.pedidosProgramados ? 'checked' : ''}> Pedidos programados</label>
            <label class="block"><input type="checkbox" id="op-recoger" ${tienda.operacion.recoleccion ? 'checked' : ''}> Recolección</label>
            <label class="block"><input type="checkbox" id="op-domicilio" ${tienda.operacion.domicilio ? 'checked' : ''}> Entrega a domicilio</label>
            <label class="block">Zona horaria<input id="op-zona" class="input-field" value="${tienda.operacion.zonaHoraria}" required></label>
            <label class="block">Aviso público<textarea id="op-mensaje" class="input-field" rows="2" placeholder="Condiciones de entrega">${tienda.operacion.mensajePublico || ''}</textarea></label>
            <label class="block">Días y horarios para recibir pedidos<textarea id="op-atencion" class="input-field" rows="3" placeholder="Lunes 09:00-17:00">${textoVentanas(tienda.operacion.horariosAtencion)}</textarea></label>
            <label class="block">Ventanas de recolección o entrega<textarea id="op-servicio" class="input-field" rows="3" placeholder="Lunes 20:00-22:00">${textoVentanas(tienda.operacion.ventanasServicio)}</textarea></label>
            <p class="text-text-secondary">Una ventana por línea. Usa el nombre del día y HH:mm-HH:mm. Recepción vacía = abierta todo el tiempo. La entrega se configura por separado.</p>
            <label class="block">Anticipación mínima (minutos)<input id="op-min" type="number" min="0" class="input-field" value="${tienda.operacion.anticipacionMinimaMinutos}"></label>
            <label class="block">Anticipación máxima (días)<input id="op-max" type="number" min="1" max="365" class="input-field" value="${tienda.operacion.anticipacionMaximaDias}"></label>
            <label class="block">Días antes de la fecha de entrega para cerrar pedidos (0 = sin corte)<input id="op-corte-dias" type="number" min="0" class="input-field" value="${tienda.operacion.diasPreviosCorte}"></label>
            <label class="block">Hora límite local<input id="op-corte-hora" type="time" class="input-field" value="${tienda.operacion.horaCorte || ''}"></label>
            <p id="op-error" class="text-error"></p>
            <button type="submit" class="btn-accent w-full">Guardar operación</button>
          </form>
        </div>

        <button id="btn-cambiar-pass" class="btn-ghost w-full justify-center text-text-secondary text-sm">
          <i data-lucide="key-round" class="w-4 h-4"></i>
          Cambiar contraseña
        </button>

        <div id="editar-tienda" class="hidden">
          <div class="card-elevated">
            <h3 class="font-semibold text-primary mb-4">
              <i data-lucide="pencil" class="w-4 h-4 inline-block mr-1 text-accent"></i>
              Editar Tienda
            </h3>
            <form id="form-editar" class="space-y-3">
              <input type="text" id="edit-nombre" value="${tienda.nombre}" class="input-field" placeholder="Nombre" />
              <input type="text" id="edit-slug" value="${tienda.slug}" class="input-field" placeholder="Slug" />
              <input type="text" id="edit-direccion" value="${tienda.direccion || ''}" class="input-field" placeholder="Dirección" />
              <input type="tel" id="edit-telefono" value="${tienda.telefono || ''}" class="input-field" placeholder="Teléfono" />
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar-edit" class="btn-ghost flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">Guardar</button>
              </div>
            </form>
          </div>
        </div>

        <div id="cambiar-pass-section" class="hidden">
          <div class="card-elevated">
            <h3 class="font-semibold text-primary mb-4">
              <i data-lucide="key-round" class="w-4 h-4 inline-block mr-1 text-accent"></i>
              Cambiar Contraseña
            </h3>
            <form id="form-pass" class="space-y-3">
              <input type="password" id="pass-actual" placeholder="Contraseña actual" class="input-field" required />
              <input type="password" id="pass-nueva" placeholder="Nueva contraseña" class="input-field" required />
              <input type="password" id="pass-confirmar" placeholder="Confirmar nueva contraseña" class="input-field" required />
              <div id="pass-msg" class="text-sm hidden text-center"></div>
              <div class="flex gap-2">
                <button type="button" id="btn-cancelar-pass" class="btn-ghost flex-1">Cancelar</button>
                <button type="submit" class="btn-accent flex-1">Actualizar</button>
              </div>
            </form>
          </div>
        </div>
      </div>
    `;

    if (window.lucide) lucide.createIcons();

    container.querySelector('#btn-editar').addEventListener('click', () => {
      container.querySelector('#editar-tienda').classList.toggle('hidden');
    });
    container.querySelector('#form-operacion').addEventListener('submit', async e => {
      e.preventDefault();
      try {
        await api.tiendas.updateOperacion({
          pedidosAlMomento: container.querySelector('#op-momento').checked,
          pedidosProgramados: container.querySelector('#op-programados').checked,
          recoleccion: container.querySelector('#op-recoger').checked,
          domicilio: container.querySelector('#op-domicilio').checked,
          zonaHoraria: container.querySelector('#op-zona').value.trim(),
          mensajePublico: container.querySelector('#op-mensaje').value.trim() || null,
          horariosAtencion: leerVentanas(container.querySelector('#op-atencion').value),
          ventanasServicio: leerVentanas(container.querySelector('#op-servicio').value),
          anticipacionMinimaMinutos: Number(container.querySelector('#op-min').value),
          anticipacionMaximaDias: Number(container.querySelector('#op-max').value),
          diasPreviosCorte: Number(container.querySelector('#op-corte-dias').value),
          horaCorte: container.querySelector('#op-corte-hora').value || null
        });
        loadTienda(container);
      } catch (err) { container.querySelector('#op-error').textContent = err.message; }
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
      const btn = container.querySelector('#btn-copiar');
      btn.innerHTML = '<i data-lucide="check" class="w-4 h-4"></i> ¡Copiado!';
      if (window.lucide) lucide.createIcons();
      setTimeout(() => {
        btn.innerHTML = '<i data-lucide="copy" class="w-4 h-4"></i> Copiar';
        if (window.lucide) lucide.createIcons();
      }, 2000);
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
        msg.className = 'text-sm text-error';
        return;
      }

      try {
        await api.auth.cambiarPassword({
          passwordActual: container.querySelector('#pass-actual').value,
          passwordNueva: nueva
        });
        msg.textContent = '¡Contraseña actualizada!';
        msg.className = 'text-sm text-success';
        container.querySelector('#form-pass').reset();
      } catch (err) {
        msg.textContent = 'La contraseña actual es incorrecta';
        msg.className = 'text-sm text-error';
      }
    });
  } catch (error) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon"><i data-lucide="alert-circle"></i></div>
        <h3>Error al cargar</h3>
        <p>No se pudo conectar con tu tienda</p>
      </div>
    `;
    if (window.lucide) lucide.createIcons();
  }
}
