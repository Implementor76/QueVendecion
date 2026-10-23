function partesEnTienda(instanteUtc, zonaHoraria) {
  const fecha = new Date(instanteUtc);
  if (Number.isNaN(fecha.getTime())) throw new RangeError('Instante UTC inválido');
  const partes = new Intl.DateTimeFormat('es-MX', {
    timeZone: zonaHoraria,
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
  }).formatToParts(fecha);
  return Object.fromEntries(partes.map(({ type, value }) => [type, value]));
}

export function horaEnTienda(instanteUtc, zonaHoraria) {
  const { hour, minute } = partesEnTienda(instanteUtc, zonaHoraria);
  return `${hour}:${minute}`;
}

export function fechaHoraEnTienda(instanteUtc, zonaHoraria) {
  const { day, month, year, hour, minute } = partesEnTienda(instanteUtc, zonaHoraria);
  return `${day}/${month}/${year} ${hour}:${minute}`;
}

export function fechaHoyEnTienda(zonaHoraria, ahora = new Date()) {
  const { day, month, year } = partesEnTienda(ahora, zonaHoraria);
  return `${year}-${month}-${day}`;
}
