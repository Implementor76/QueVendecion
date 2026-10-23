namespace QueVendecion.Application.Exceptions;

public class ReglaNegocioException(string codigo, string mensaje) : InvalidOperationException(mensaje)
{
    public string Codigo { get; } = codigo;
}
