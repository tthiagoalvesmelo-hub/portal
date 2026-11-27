using Portal.Api.Health;

namespace Portal.Api.Tests.Support;

/// <summary>Dublê L-12: estado de saúde sempre indisponível (CT-0018).</summary>
public sealed class UnavailableHealthState : IHealthState
{
    public bool IsUnavailable => true;

    public void MarkShuttingDown()
    {
    }
}

/// <summary>Dublê L-12: origem de falha interna que sempre lança (CT-0017). Mensagem sem dado sensível.</summary>
public sealed class ThrowingFailureSource : IInternalFailureSource
{
    public void ThrowIfFailed() => throw new InvalidOperationException("falha injetada pelo teste");
}

/// <summary>Drenagem sem espera, para não atrasar o descarte das fábricas de teste.</summary>
public sealed class NoWaitDrain : IShutdownDrain
{
    public void Wait()
    {
    }
}

/// <summary>Relógio controlado pelo teste.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
