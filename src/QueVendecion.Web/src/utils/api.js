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
    throw new Error(`Error: ${response.status}`);
  }

  if (response.status === 204) return null;
  return response.json();
}

export const api = {
  tiendas: {
    getMiTienda: () => request('/tiendas/mi-tienda'),
    update: (id, data) => request(`/tiendas/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  },
  productos: {
    getByMiTienda: () => request('/tiendas/mi-tienda/productos'),
    create: (data) => request('/tiendas/mi-tienda/productos', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => request(`/tiendas/mi-tienda/productos/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => request(`/tiendas/mi-tienda/productos/${id}`, { method: 'DELETE' })
  },
  pedidos: {
    getMisPedidos: (estado) => {
      const params = estado ? `?estado=${estado}` : '';
      return request(`/pedidos/mi-tienda${params}`);
    },
    getById: (id) => request(`/pedidos/${id}`),
    updateEstado: (id, estado) => request(`/pedidos/${id}/estado`, { method: 'PATCH', body: JSON.stringify({ estado }) }),
    updatePago: (id, estadoPago) => request(`/pedidos/${id}/pago`, { method: 'PATCH', body: JSON.stringify({ estadoPago }) }),
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
    getMenu: (slug) => request(`/public/tienda/${slug}/menu`),
    registrarCliente: (slug, data) => request(`/public/tienda/${slug}/cliente`, { method: 'POST', body: JSON.stringify(data) }),
    getPedidosCliente: (slug, telefono) => request(`/public/tienda/${slug}/cliente/${encodeURIComponent(telefono)}/pedidos`),
    crearPedido: (slug, data) => request(`/public/tienda/${slug}/pedido`, { method: 'POST', body: JSON.stringify(data) })
  }
};
