namespace Portal.Api.Health;

/// <summary>
/// Janela de drenagem fixa em código (DEC-ARQ-0012 §3): o listener continua no ar e o /health responde 503.
/// Deve ser menor que <see cref="ShutdownTimeout"/>, que por sua vez fica ≤ espera do stop-local.sh − 2 s.
/// </summary>
public sealed class FixedShutdownDrain : IShutdownDrain
{
    /// <summary>D = 3 s (ajuste permitido sem nova DEC: 2 s a 4 s, com medição no spike R4).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    /// <summary>HostOptions.ShutdownTimeout = 5 s, definido em código (DEC-ARQ-0012 §3).</summary>
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    public void Wait() => Thread.Sleep(Window);
}
