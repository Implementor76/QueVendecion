import test from 'node:test';
import assert from 'node:assert/strict';
import { fechaHoraEnTienda, horaEnTienda, fechaHoyEnTienda } from '../src/utils/fechaTienda.js';

test('muestra el instante UTC en la zona de la tienda aunque cambie el día', () => {
  const zona = 'America/Hermosillo';
  assert.equal(fechaHoraEnTienda('2026-10-22T05:11:00Z', zona), '21/10/2026 22:11');
  assert.equal(horaEnTienda('2026-10-22T05:11:00Z', zona), '22:11');
  assert.equal(fechaHoraEnTienda('2026-10-22T07:05:00Z', zona), '22/10/2026 00:05');
});

test('la zona de la tienda determina la salida, no la del navegador', () => {
  const instante = '2026-10-22T05:11:00Z';
  assert.equal(fechaHoraEnTienda(instante, 'America/Hermosillo'), '21/10/2026 22:11');
  assert.equal(fechaHoraEnTienda(instante, 'Etc/UTC'), '22/10/2026 05:11');
});

test('el rango de ventas inicia en el día local de la tienda', () => {
  assert.equal(fechaHoyEnTienda('America/Hermosillo', new Date('2026-10-23T05:11:00Z')), '2026-10-22');
});
