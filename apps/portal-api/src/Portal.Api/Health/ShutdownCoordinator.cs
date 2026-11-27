namespace Portal.Api.Health;

/// <summary>
/// Ligado ao ApplicationStopping: marca o estado "encerrando" e segura a parada pela janela de drenagem.
/// Esperar em IHostedService.StopAsync não serve, porque o servidor já parou (DEC-ARQ-0012 §3).
/// </summary>
public sealed partial class ShutdownCoordinator(IHealthState state, IShutdownDrain drain, ILogger<ShutdownCoordinator> logger)
{
    public void OnStopping()
    {
        state.MarkShuttingDown();
        LogDraining(logger);
        drain.Wait();
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Encerramento gracioso: /health responde 503 durante a janela de drenagem")]
    private static partial void LogDraining(ILogger logger);
}
