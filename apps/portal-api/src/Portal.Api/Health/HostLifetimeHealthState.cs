namespace Portal.Api.Health;

/// <summary>Implementação de produção: fica indisponível somente após o sinal de parada do host.</summary>
public sealed class HostLifetimeHealthState : IHealthState
{
    private volatile bool _shuttingDown;

    public bool IsUnavailable => _shuttingDown;

    public void MarkShuttingDown() => _shuttingDown = true;
}
