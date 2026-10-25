using QueVendecion.Application.Exceptions;

namespace QueVendecion.Infrastructure.Services;

public readonly record struct SeleccionPdf(string Normalizada, int Cantidad);

public static class PaginasPdf
{
    public static SeleccionPdf Normalizar(string? entrada, int total)
    {
        if (total is < 1 or > 1000) throw new ReglaNegocioException("pdf_paginas_invalidas", "El PDF no tiene un número de páginas permitido.");
        if (string.IsNullOrWhiteSpace(entrada) || entrada.Equals("todas", StringComparison.OrdinalIgnoreCase))
            return new SeleccionPdf(string.Join(',', Enumerable.Range(1, total)), total);
        if (entrada.Length > 2000) throw new ReglaNegocioException("pdf_paginas_invalidas", "El rango de páginas es demasiado largo.");
        var paginas = new SortedSet<int>();
        foreach (var parte in entrada.Split(','))
        {
            var limites = parte.Trim().Split('-', StringSplitOptions.TrimEntries);
            if (limites.Length is < 1 or > 2 || !int.TryParse(limites[0], out var desde) ||
                !int.TryParse(limites[^1], out var hasta) || desde < 1 || hasta > total || desde > hasta)
                throw new ReglaNegocioException("pdf_paginas_invalidas", "Revisa los rangos; usa números de página existentes, por ejemplo 1-3,5.");
            for (var p = desde; p <= hasta; p++) paginas.Add(p);
        }
        if (paginas.Count == 0) throw new ReglaNegocioException("pdf_paginas_invalidas", "Selecciona al menos una página.");
        return new SeleccionPdf(string.Join(',', paginas), paginas.Count);
    }
}
