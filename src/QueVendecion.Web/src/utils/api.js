import { getToken } from './auth.js';

const API_BASE = '/api';

async function request(url, options = {}) {
  const token = getToken();
  const headers = {
    'Content-Type': 'application/json',
    ...options.headers
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE}${url}`, {
    headers,
    ...options
  });

  if (!response.ok) {
    const body = await response.text();
    let payload;
    try { payload = JSON.parse(body); } catch { /* Non-JSON error response. */ }
    const safeCodes = ['stock_invalido', 'stock_insuficiente', 'cantidad_invalida',
      'producto_no_disponible', 'pedido_cancelado', 'inventario_concurrente',
      'configuracion_invalida', 'entrega_no_disponible', 'modalidad_no_disponible',
      'fuera_de_atencion', 'fecha_invalida', 'horario_no_disponible',
      'producto_fecha_incompatible', 'producto_modalidad_incompatible', 'producto_entrega_incompatible', 'entrega_producto_invalida', 'transicion_invalida', 'horario_invalido', 'fechas_invalidas'];
    safeCodes.push('direccion_entrega_invalida');
    const message = response.status < 500 && safeCodes.includes(payload?.code)
      ? payload.message : 'No se pudo completar la operación. Revisa los datos e intenta de nuevo.';
    const error = new Error(message);
    error.status = response.status;
    error.code = payload?.code;
    if (import.meta.env.DEV) console.error('API error', { url, status: response.status, body });
    throw error;
  }

  if (response.status === 204) return null;
  return response.json();
}

export const api = {
  tiendas: {
    getMiTienda: () => request('/tiendas/mi-tienda'),
    update: (id, data) => request('/tiendas/mi-tienda', { method: 'PUT', body: JSON.stringify(data) }),
    updateOperacion: (data) => request('/tiendas/mi-tienda/operacion', { method: 'PUT', body: JSON.stringify(data) }),
  },
  productos: {
    getByMiTienda: () => request('/tiendas/mi-tienda/productos'),
    create: (data) => request('/tiendas/mi-tienda/productos', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => request(`/tiendas/mi-tienda/productos/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    compra: (id, data) => request(`/tiendas/mi-tienda/productos/${id}/compras`, { method: 'POST', body: JSON.stringify(data) }),
    ajuste: (id, data) => request(`/tiendas/mi-tienda/productos/${id}/ajustes`, { method: 'POST', body: JSON.stringify(data) }),
    movimientos: (id) => request(`/tiendas/mi-tienda/productos/${id}/movimientos`),
    delete: (id) => request(`/tiendas/mi-tienda/productos/${id}`, { method: 'DELETE' })
  },
  pedidos: {
    resumen: (desde, hasta) => request(`/pedidos/mi-tienda/resumen?${new URLSearchParams({ desde, hasta })}`),
    getMisPedidos: (estado, fechaServicio) => {
      const params = new URLSearchParams();
      if (estado) params.set('estado', estado);
      if (fechaServicio) params.set('fechaServicio', fechaServicio);
      return request(`/pedidos/mi-tienda?${params}`);
    },
    getById: (id) => request(`/pedidos/${id}`),
    updateEstado: (id, estado) => request(`/pedidos/${id}/estado`, { method: 'PATCH', body: JSON.stringify({ estado }) }),
    updatePago: (id, estadoPago) => request(`/pedidos/${id}/pago`, { method: 'PATCH', body: JSON.stringify({ estadoPago }) }),
    confirmarHorario: (id, horarioConfirmado) => request(`/pedidos/${id}/horario`, { method: 'PATCH', body: JSON.stringify({ horarioConfirmado }) }),
  },
  auth: {
    login: (data) => request('/auth/login', { method: 'POST', body: JSON.stringify(data) }),
    registrar: (data) => request('/auth/registrar', { method: 'POST', body: JSON.stringify(data) }),
    cambiarPassword: (data) => request('/auth/cambiar-password', { method: 'POST', body: JSON.stringify(data) })
  },
  superAdmin: {
    crearTienda: (data) => request('/superadmin/tienda', { method: 'POST', body: JSON.stringify(data) }),
    getTiendas: () => request('/superadmin/tiendas')
  },
  public: {
    getTienda: (slug) => request(`/public/tienda/${slug}`),
    getMenu: (slug, fechaServicio, modalidad, entrega) => {
      const params = new URLSearchParams();
      if (fechaServicio) params.set('fechaServicio', fechaServicio);
      if (modalidad) params.set('modalidad', modalidad);
      if (entrega) params.set('entrega', entrega);
      return request(`/public/tienda/${slug}/menu?${params}`);
    },
    getOpciones: (slug) => request(`/public/tienda/${slug}/opciones`),
    registrarCliente: (slug, data) => request(`/public/tienda/${slug}/cliente`, { method: 'POST', body: JSON.stringify(data) }),
    getPedidosCliente: (slug, telefono) => request(`/public/tienda/${slug}/cliente/${encodeURIComponent(telefono)}/pedidos`),
    crearPedido: (slug, data) => request(`/public/tienda/${slug}/pedido`, { method: 'POST', body: JSON.stringify(data) })
  }
};
