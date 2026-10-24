using QueVendecion.Application.Exceptions;

namespace QueVendecion.Infrastructure.Services;

public readonly record struct PapelCalculo(int CarasCobradas, int HojasConsumidas);

public static class PapelReglas
{
    public const int MaxCarasPorPedido = 100_000;

    public static PapelCalculo Calcular(int? carasPorJuego, int? juegos, bool? dobleCara, bool admiteDobleCara)
    {
        if (carasPorJuego is null or <= 0 || juegos is null or <= 0 || dobleCara is null ||
            carasPorJuego > MaxCarasPorPedido || juegos > MaxCarasPorPedido)
            throw new ReglaNegocioException("papel_cantidad_invalida", "Captura caras por juego y juegos enteros mayores a cero.");
        if (dobleCara.Value && !admiteDobleCara)
            throw new ReglaNegocioException("doble_cara_no_disponible", "Este servicio no admite ambos lados.");
        var caras = (long)carasPorJuego.Value * juegos.Value;
        var hojas = (dobleCara.Value ? ((long)carasPorJuego.Value + 1) / 2 : carasPorJuego.Value) * juegos.Value;
        if (caras > MaxCarasPorPedido || hojas > MaxCarasPorPedido)
            throw new ReglaNegocioException("papel_cantidad_invalida", "La cantidad supera el máximo de 100000 caras por pedido.");
        return new PapelCalculo((int)caras, (int)hojas);
    }
}
