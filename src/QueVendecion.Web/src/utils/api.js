const API_BASE = '/api';

async function request(url, options = {}) {
  const response = await fetch(`${API_BASE}${url}`, {
    headers: {
      'Content-Type': 'application/json',
      ...options.headers
    },
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
    getAll: () => request('/tiendas'),
    getById: (id) => request(`/tiendas/${id}`),
    create: (data) => request('/tiendas', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => request(`/tiendas/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => request(`/tiendas/${id}`, { method: 'DELETE' })
  },
  productos: {
    getByTienda: (tiendaId) => request(`/tiendas/${tiendaId}/productos`),
    getById: (id) => request(`/tiendas/0/productos/${id}`),
    create: (tiendaId, data) => request(`/tiendas/${tiendaId}/productos`, { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => request(`/tiendas/0/productos/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => request(`/tiendas/0/productos/${id}`, { method: 'DELETE' })
  },
  pedidos: {
    getByTienda: (tiendaId, estado) => {
      const params = estado ? `?estado=${estado}` : '';
      return request(`/pedidos/tienda/${tiendaId}${params}`);
    },
    getById: (id) => request(`/pedidos/${id}`),
    create: (data) => request('/pedidos', { method: 'POST', body: JSON.stringify(data) }),
    updateEstado: (id, estado) => request(`/pedidos/${id}/estado`, { method: 'PATCH', body: JSON.stringify({ estado }) }),
    updatePago: (id, estadoPago) => request(`/pedidos/${id}/pago`, { method: 'PATCH', body: JSON.stringify({ estadoPago }) }),
    delete: (id) => request(`/pedidos/${id}`, { method: 'DELETE' })
  },
  clientes: {
    getByTelefono: (telefono) => request(`/clientes/telefono/${telefono}`),
    createOrGet: (data) => request('/clientes', { method: 'POST', body: JSON.stringify(data) })
  },
  auth: {
    login: (data) => request('/auth/login', { method: 'POST', body: JSON.stringify(data) }),
    registrar: (data) => request('/auth/registrar', { method: 'POST', body: JSON.stringify(data) })
  }
};
