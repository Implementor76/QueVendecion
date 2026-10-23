using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using QueVendecion.Application.DTOs;
using QueVendecion.Application.Exceptions;
using QueVendecion.Domain.Entities;
using QueVendecion.Domain.Enums;

namespace QueVendecion.Infrastructure.Services;

public static class OperacionReglas
{
    public static OperacionTiendaDto Leer(Tienda tienda) => string.IsNullOrWhiteSpace(tienda.OperacionJson)
        ? new OperacionTiendaDto()
        : JsonSerializer.Deserialize<OperacionTiendaDto>(tienda.OperacionJson) ?? new OperacionTiendaDto();

    public static List<DateOnly> LeerFechas(Producto producto) => string.IsNullOrWhiteSpace(producto.FechasServicioJson)
        ? new List<DateOnly>()
        : JsonSerializer.Deserialize<List<DateOnly>>(producto.FechasServicioJson) ?? new List<DateOnly>();

    public static void ValidarConfiguracion(OperacionTiendaDto config)
    {
        if (config.HorariosAtencion == null || config.VentanasServicio == null || string.IsNullOrWhiteSpace(config.ZonaHoraria))
            Error("configuracion_invalida", "Captura zona horaria y listas de horarios válidas.");
        if (!config.PedidosAlMomento && !config.PedidosProgramados || !config.Recoleccion && !config.Domicilio)
            Error("configuracion_invalida", "Habilita al menos una modalidad de pedido y una forma de entrega.");
        if (config.PedidosProgramados && config.VentanasServicio.Count == 0)
            Error("configuracion_invalida", "Configura al menos una ventana de entrega para los pedidos programados.");
        if (config.AnticipacionMinimaMinutos < 0 || config.AnticipacionMaximaDias < 1 || config.AnticipacionMaximaDias > 365 || config.DiasPreviosCorte < 0 || config.DiasPreviosCorte > config.AnticipacionMaximaDias)
            Error("configuracion_invalida", "Revisa la anticipación y los días previos al corte.");
        try { TimeZoneInfo.FindSystemTimeZoneById(config.ZonaHoraria); }
        catch { Error("configuracion_invalida", "La zona horaria no existe en este servidor."); }
        if (config.DiasPreviosCorte > 0 && !HoraValida(config.HoraCorte))
            Error("configuracion_invalida", "Captura una hora de corte válida.");
        foreach (var ventana in config.HorariosAtencion.Concat(config.VentanasServicio))
            if (!Enum.IsDefined(ventana.Dia) || !HoraValida(ventana.Inicio) || !HoraValida(ventana.Fin) || string.CompareOrdinal(ventana.Inicio, ventana.Fin) >= 0)
                Error("configuracion_invalida", "Las ventanas deben tener día y horas HH:mm en orden.");
    }

    public static bool HoraValida(string? hora) => hora?.Length == 5 && TimeOnly.TryParseExact(hora, "HH:mm", out _);

    public static DateTime AhoraLocal(OperacionTiendaDto config, DateTime utcNow) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(config.ZonaHoraria));

    public static List<string> VentanasPara(OperacionTiendaDto config, DateOnly fecha) =>
        config.VentanasServicio.Where(v => v.Dia == fecha.DayOfWeek).Select(v => $"{v.Inicio}-{v.Fin}").ToList();

    public static bool PuedeRecibirAhora(OperacionTiendaDto config, DateTime utcNow)
    {
        var local = AhoraLocal(config, utcNow);
        var hora = TimeOnly.FromDateTime(local);
        return config.HorariosAtencion.Count == 0 || config.HorariosAtencion.Any(v =>
            v.Dia == local.DayOfWeek && hora >= TimeOnly.Parse(v.Inicio) && hora <= TimeOnly.Parse(v.Fin));
    }

    public static bool PuedeAlMomento(OperacionTiendaDto config, DateTime utcNow) =>
        config.PedidosAlMomento && PuedeRecibirAhora(config, utcNow);

    public static bool PuedeProgramarse(OperacionTiendaDto config, DateOnly fecha, string? ventana, DateTime utcNow)
    {
        if (!config.PedidosProgramados || !PuedeRecibirAhora(config, utcNow)) return false;
        var local = AhoraLocal(config, utcNow);
        var hoy = DateOnly.FromDateTime(local);
        if (fecha < hoy || fecha > hoy.AddDays(config.AnticipacionMaximaDias)) return false;
        if (config.DiasPreviosCorte > 0 && config.HoraCorte != null &&
            local > fecha.AddDays(-config.DiasPreviosCorte).ToDateTime(TimeOnly.Parse(config.HoraCorte))) return false;
        var ventanas = VentanasPara(config, fecha);
        if (ventana == null || ventanas.Count == 0 || !ventanas.Contains(ventana)) return false;
        var inicio = TimeOnly.Parse(ventana![..5]);
        var inicioServicio = fecha.ToDateTime(inicio);
        return inicioServicio >= local.AddMinutes(config.AnticipacionMinimaMinutos);
    }

    public static void ValidarPedido(OperacionTiendaDto config, CrearPedidoDto dto, IReadOnlyList<Producto> productos, DateTime utcNow)
    {
        ValidarConfiguracion(config);
        if (!PuedeRecibirAhora(config, utcNow))
            Error("fuera_de_atencion", "La tienda no recibe pedidos en este horario.");
        if (dto.Entrega == FormaEntrega.Recoleccion && !config.Recoleccion || dto.Entrega == FormaEntrega.Domicilio && !config.Domicilio || !Enum.IsDefined(dto.Entrega))
            Error("entrega_no_disponible", "La forma de entrega seleccionada no está disponible.");
        if (dto.Entrega == FormaEntrega.Domicilio && string.IsNullOrWhiteSpace(dto.DireccionEntrega) || dto.DireccionEntrega?.Length > 300)
            Error("direccion_entrega_invalida", "Escribe una dirección de entrega válida (máximo 300 caracteres).");
        var local = AhoraLocal(config, utcNow);
        DateOnly fecha;
        if (dto.Modalidad == ModalidadPedido.AlMomento)
        {
            if (!config.PedidosAlMomento || dto.VentanaSolicitada != null)
                Error("modalidad_no_disponible", "El pedido al momento no está disponible o incluye una ventana programada.");
            if (!PuedeAlMomento(config, utcNow))
                Error("fuera_de_atencion", "La tienda no recibe pedidos al momento en este horario.");
            fecha = DateOnly.FromDateTime(local);
            if (dto.FechaServicio != null && dto.FechaServicio != fecha)
                Error("fecha_invalida", "La fecha del pedido al momento debe ser hoy en la zona horaria de la tienda.");
        }
        else if (dto.Modalidad == ModalidadPedido.Programado && dto.FechaServicio is { } programada)
        {
            if (!PuedeProgramarse(config, programada, dto.VentanaSolicitada, utcNow))
                Error("horario_no_disponible", "La fecha o ventana ya no está disponible. Selecciona otra opción.");
            fecha = programada;
        }
        else { Error("fecha_invalida", "Selecciona una fecha y ventana válidas para el pedido programado."); return; }

        foreach (var producto in productos)
        {
            if (producto.EntregaPermitida == null ||
                dto.Entrega == FormaEntrega.Recoleccion && producto.EntregaPermitida == EntregaProducto.Domicilio ||
                dto.Entrega == FormaEntrega.Domicilio && producto.EntregaPermitida == EntregaProducto.Recoleccion)
                Error("producto_entrega_incompatible", $"{producto.Nombre} requiere definir o cambiar su forma de entrega. Quita el producto o elige otra forma.");
            var fechas = LeerFechas(producto);
            if (fechas.Count > 0 && dto.Modalidad == ModalidadPedido.AlMomento)
                Error("producto_modalidad_incompatible", $"{producto.Nombre} requiere un pedido programado. Cambia la modalidad o quítalo del carrito.");
            if (fechas.Count > 0 && !fechas.Contains(fecha))
                Error("producto_fecha_incompatible", $"{producto.Nombre} no está disponible para {fecha:yyyy-MM-dd}. Cambia la fecha o quítalo del carrito.");
        }
    }

    [DoesNotReturn]
    private static void Error(string code, string message) => throw new ReglaNegocioException(code, message);
}
