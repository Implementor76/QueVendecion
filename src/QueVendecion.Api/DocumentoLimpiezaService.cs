using QueVendecion.Infrastructure.Services;

namespace QueVendecion.Api;

public sealed class DocumentoLimpiezaService : BackgroundService
{
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<DocumentoLimpiezaService> log;
    public DocumentoLimpiezaService(IServiceScopeFactory scopes, ILogger<DocumentoLimpiezaService> log)
    { this.scopes = scopes; this.log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DocumentoService>().LimpiarAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch { log.LogWarning("No se completó la limpieza de documentos; se reintentará."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
