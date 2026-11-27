using System.Collections.Concurrent;

namespace Portal.Api.Tests.Support;

/// <summary>Registro capturado: categoria, nível, mensagem formatada, estado estruturado e exceção.</summary>
public sealed record CapturedLog(long Sequence, string Category, LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State, Exception? Exception);

/// <summary>
/// Sumidouro de ILogger escrito à mão (DEC-ARQ-0013). Não altera filtros: quem decide os níveis é a composição
/// de produção, com a categoria da aplicação elevada a Debug só pelo teste.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();
    private long _sequence;

    public long Mark() => Interlocked.Read(ref _sequence);

    /// <summary>Registros da janela da requisição, sem o ciclo de vida do host (fora do oráculo, DEC-ARQ-0013 item 3).</summary>
    public IReadOnlyList<CapturedLog> Window(long fromExclusive, long toInclusive) =>
        _entries.Where(e => e.Sequence > fromExclusive && e.Sequence <= toInclusive && e.Category != "Microsoft.Hosting.Lifetime")
            .OrderBy(e => e.Sequence)
            .ToList();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(string category, LogLevel level, string message, IReadOnlyList<KeyValuePair<string, object?>> state, Exception? exception)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        _entries.Enqueue(new CapturedLog(sequence, category, level, message, state, exception));
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var structured = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            owner.Add(category, logLevel, formatter(state, exception), structured, exception);
        }
    }
}
