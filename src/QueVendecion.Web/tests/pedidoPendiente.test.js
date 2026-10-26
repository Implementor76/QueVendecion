import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { crearRevisionPedido, crearIntentoPendiente, leerIntentoPendiente, clasificarErrorPedido } from '../src/utils/pedidoPendiente.js';

const cliente = '5550001111';
const clave = '12345678-1234-4234-9234-123456789abc';
const carrito = () => ({ a: { id: '5', nombre: 'Impresión PDF', precio: 3, cantidad: 6,
  documentoPdfId: '12345678-1234-4234-9234-123456789abd', nombrePdf: 'prueba.pdf',
  paginas: '1-3', juegos: 2, dobleCara: true, hojas: 4, revisionImpresion: 'rev' } });
const solicitud = revision => ({ claveIdempotencia: clave, telefonoCliente: cliente,
  totalEsperado: revision.totalEsperado, modalidad: revision.modalidad, entrega: revision.entrega,
  fechaServicio: revision.fechaServicio, ventanaSolicitada: revision.ventanaSolicitada,
  formaPago: 'Efectivo', observaciones: 'primera nota', items: revision.items });

test('un rechazo ayer deja crear una revisión con la fecha nueva, sin reutilizar el envío antiguo', () => {
  const c = carrito();
  const anterior = crearRevisionPedido(c, { modalidad: 'Programado', entrega: 'Recoleccion',
    fecha: '2026-10-24', ventana: '08:00-12:00' }, '2026-10-25');
  const guardado = crearIntentoPendiente(solicitud(anterior), anterior.partidas);
  const actual = crearRevisionPedido(c, { modalidad: 'Programado', entrega: 'Recoleccion',
    fecha: '2026-10-26', ventana: '13:00-17:00' }, '2026-10-25');
  assert.equal(guardado.payload.fechaServicio, '2026-10-24');
  assert.equal(actual.fechaServicio, '2026-10-26');
  assert.equal(actual.ventanaSolicitada, '13:00-17:00');
  assert.equal(actual.totalEsperado, 18);
});

test('reintento conserva clave, PDF, partidas, nota y contenido aunque cambie el carrito', () => {
  const c = carrito();
  const revision = crearRevisionPedido(c, { modalidad: 'Programado', entrega: 'Recoleccion',
    fecha: '2026-10-26', ventana: '08:00-12:00' }, '2026-10-25');
  const intento = crearIntentoPendiente(solicitud(revision), revision.partidas);
  const raw = JSON.stringify(intento);
  c.a.cantidad = 1; c.a.precio = 9;
  const recuperado = leerIntentoPendiente(raw, cliente);
  assert.equal(recuperado.tipo, 'valido');
  assert.deepEqual(recuperado.intento.payload, intento.payload);
  assert.equal(recuperado.intento.payload.claveIdempotencia, clave);
  assert.equal(recuperado.intento.partidas[0].cantidad, 6);
  assert.equal(recuperado.intento.payload.observaciones, 'primera nota');
});

test('formato anterior válido se recupera; almacenamiento corrupto no se envía', () => {
  const r = crearRevisionPedido(carrito(), { modalidad: 'Programado', entrega: 'Recoleccion',
    fecha: '2026-10-26', ventana: '08:00-12:00' }, '2026-10-25');
  const legacy = leerIntentoPendiente(JSON.stringify(solicitud(r)), cliente);
  assert.equal(legacy.tipo, 'valido');
  assert.equal(legacy.intento.version, 1);
  assert.match(legacy.intento.partidas[0].nombre, /Producto #5/);
  assert.equal(leerIntentoPendiente('{roto', cliente).tipo, 'corrupto');
  assert.equal(leerIntentoPendiente(JSON.stringify({ ...solicitud(r), telefonoCliente: 'otro' }), cliente).tipo, 'corrupto');
  assert.equal(leerIntentoPendiente(null, cliente).tipo, 'ninguno');
});

test('solo rechazos de negocio confirmados liberan un intento', () => {
  const tipo = (code, status) => clasificarErrorPedido({ code, status });
  assert.equal(tipo('horario_no_disponible', 400), 'horario');
  assert.equal(tipo('stock_insuficiente', 400), 'rechazo');
  assert.equal(tipo('precio_actualizado', 400), 'rechazo');
  assert.equal(tipo('pdf_no_disponible', 400), 'pdf_vencido');
  assert.equal(tipo('pdf_sesion_invalida', 400), 'sesion');
  assert.equal(tipo('idempotencia_conflicto', 400), 'conflicto');
  assert.equal(tipo('registro_ocupado', 400), 'incierto');
  assert.equal(tipo('inventario_concurrente', 409), 'incierto');
  assert.equal(tipo(undefined, 400), 'incierto');
  assert.equal(tipo(undefined, 500), 'incierto');
  assert.equal(clasificarErrorPedido(new TypeError('Failed to fetch')), 'incierto');
});

test('sin red, el service worker no convierte un envío incierto en HTTP 200', async () => {
  const handlers = {};
  const self = { location: { origin: 'https://ejemplo.test' },
    addEventListener: (name, callback) => { handlers[name] = callback; } };
  runInNewContext(readFileSync(new URL('../public/sw.js', import.meta.url), 'utf8'),
    { self, URL, Response, fetch: async () => { throw new TypeError('sin red'); } });
  let respuesta;
  handlers.fetch({ request: { url: 'https://ejemplo.test/api/public/tienda/prueba/pedido' },
    respondWith: p => { respuesta = p; } });
  assert.equal((await respuesta).status, 503);
});
