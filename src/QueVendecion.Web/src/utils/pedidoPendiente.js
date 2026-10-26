const CLAVE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const RECHAZOS_SIN_PEDIDO = new Set([
  'stock_invalido', 'stock_insuficiente', 'cantidad_invalida', 'producto_no_disponible',
  'configuracion_invalida', 'entrega_no_disponible', 'modalidad_no_disponible',
  'fuera_de_atencion', 'fecha_invalida', 'horario_no_disponible',
  'producto_fecha_incompatible', 'producto_modalidad_incompatible',
  'producto_entrega_incompatible', 'direccion_entrega_invalida',
  'clave_invalida', 'pago_invalido', 'nota_invalida', 'precio_actualizado',
  'precio_invalido', 'importe_invalido', 'papel_invalido',
  'papel_cantidad_invalida', 'papel_configuracion_cambio',
  'doble_cara_no_disponible', 'inventario_inactivo',
  'pdf_configuracion_invalida', 'pdf_configuracion_cambio',
  'pdf_requerido', 'pdf_paginas_invalidas', 'pdf_no_disponible', 'pdf_repetido'
]);

const copia = value => JSON.parse(JSON.stringify(value));

function congelar(value) {
  if (value && typeof value === 'object') {
    Object.values(value).forEach(congelar);
    Object.freeze(value);
  }
  return value;
}

export function crearRevisionPedido(carrito, seleccion, hoy) {
  const partidas = Object.values(carrito).map(c => copia(c));
  const items = partidas.map(c => ({
    productoId: Number(c.id), cantidad: c.cantidad,
    carasPorJuego: c.documentoPdfId ? null : c.carasPorJuego ?? null,
    juegos: c.juegos ?? null, dobleCara: c.dobleCara ?? null,
    documentoPdfId: c.documentoPdfId ?? null, paginas: c.paginas ?? null,
    revisionImpresion: c.revisionImpresion ?? null,
    precioEsperado: c.documentoPdfId ? c.precio : null
  }));
  return congelar({
    partidas, items,
    totalEsperado: Number(partidas.reduce((sum, c) => sum + c.precio * c.cantidad, 0).toFixed(2)),
    modalidad: seleccion.modalidad, entrega: seleccion.entrega,
    fechaServicio: seleccion.modalidad === 'Programado' ? seleccion.fecha : hoy,
    ventanaSolicitada: seleccion.modalidad === 'Programado' ? seleccion.ventana : null
  });
}

export function crearIntentoPendiente(payload, partidas) {
  return congelar({ version: 2, payload: copia(payload), partidas: copia(partidas), creadoEnUtc: new Date().toISOString() });
}

function payloadValido(p, telefono) {
  return p && typeof p === 'object' && CLAVE.test(p.claveIdempotencia) &&
    p.telefonoCliente === telefono && Number.isFinite(p.totalEsperado) && p.totalEsperado >= 0 &&
    ['AlMomento', 'Programado'].includes(p.modalidad) &&
    ['Recoleccion', 'Domicilio'].includes(p.entrega) &&
    /^\d{4}-\d{2}-\d{2}$/.test(p.fechaServicio) &&
    (p.ventanaSolicitada == null || typeof p.ventanaSolicitada === 'string') &&
    Array.isArray(p.items) && p.items.length > 0 && p.items.length <= 100 &&
    p.items.every(i => Number.isInteger(i.productoId) && i.productoId > 0 &&
      Number.isInteger(i.cantidad) && i.cantidad > 0 &&
      (i.documentoPdfId == null || CLAVE.test(i.documentoPdfId)));
}

export function leerIntentoPendiente(raw, telefono) {
  if (raw == null) return { tipo: 'ninguno' };
  if (raw.length > 100_000) return { tipo: 'corrupto' };
  try {
    const parsed = JSON.parse(raw);
    const p = parsed?.version === 2 ? parsed.payload : parsed;
    if (!payloadValido(p, telefono)) return { tipo: 'corrupto' };
    const partidas = parsed?.version === 2 && Array.isArray(parsed.partidas)
      ? parsed.partidas : p.items.map(i => ({ id: i.productoId, nombre: `Producto #${i.productoId}`,
        cantidad: i.cantidad, nombrePdf: i.documentoPdfId ? 'PDF adjunto' : null,
        paginas: i.paginas, juegos: i.juegos, dobleCara: i.dobleCara }));
    return { tipo: 'valido', intento: congelar({ version: parsed?.version === 2 ? 2 : 1,
      payload: copia(p), partidas: copia(partidas), creadoEnUtc: parsed?.creadoEnUtc ?? null }) };
  } catch { return { tipo: 'corrupto' }; }
}

export function clasificarErrorPedido(error) {
  if (error?.code === 'idempotencia_conflicto') return 'conflicto';
  if (error?.code === 'pdf_sesion_invalida') return 'sesion';
  if (error?.code === 'registro_ocupado' || error?.code === 'inventario_concurrente') return 'incierto';
  if (error?.code === 'pdf_no_disponible' && error.status === 400) return 'pdf_vencido';
  if (error?.code === 'horario_no_disponible' && error.status === 400) return 'horario';
  if (error?.status === 400 && RECHAZOS_SIN_PEDIDO.has(error.code)) return 'rechazo';
  return 'incierto';
}
