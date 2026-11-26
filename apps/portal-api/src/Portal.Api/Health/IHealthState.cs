namespace Portal.Api.Health;

/// <summary>
/// Estado de saúde do processo. Em produção vem só do ciclo de vida do host (encerramento gracioso).
/// Substituível apenas na composição de teste (DEC-ARQ-0011).
/// </summary>
public interface IHealthState
{
    bool IsUnavailable { get; }

    void MarkShuttingDown();
}
