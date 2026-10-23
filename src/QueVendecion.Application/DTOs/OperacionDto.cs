namespace QueVendecion.Application.DTOs;

public class VentanaOperacionDto
{
    public DayOfWeek Dia { get; set; }
    public string Inicio { get; set; } = "00:00";
    public string Fin { get; set; } = "23:59";
}

public class OperacionTiendaDto
{
    public bool PedidosAlMomento { get; set; } = true;
    public bool PedidosProgramados { get; set; }
    public bool Recoleccion { get; set; } = true;
    public bool Domicilio { get; set; }
    public string ZonaHoraria { get; set; } = "America/Hermosillo";
    public string? MensajePublico { get; set; }
    public int AnticipacionMinimaMinutos { get; set; }
    public int AnticipacionMaximaDias { get; set; } = 30;
    // Días completos previos al servicio para aplicar el corte; 0 lo desactiva.
    public int DiasPreviosCorte { get; set; }
    public string? HoraCorte { get; set; }
    public List<VentanaOperacionDto> HorariosAtencion { get; set; } = new();
    public List<VentanaOperacionDto> VentanasServicio { get; set; } = new();
}
