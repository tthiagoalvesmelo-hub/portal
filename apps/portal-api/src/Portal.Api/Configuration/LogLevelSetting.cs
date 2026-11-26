namespace Portal.Api.Configuration;

/// <summary>
/// Nível base de log da aplicação a partir de LOG_LEVEL (ADR-0001 v2.0 D7).
/// Valor ausente ou inválido cai em Information; o aviso é registrado após a inicialização.
/// </summary>
public sealed class LogLevelSetting
{
    private static readonly string[] Accepted = ["Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"];

    private LogLevelSetting(LogLevel level, bool fellBack)
    {
        Level = level;
        FellBack = fellBack;
    }

    public LogLevel Level { get; }

    /// <summary>Verdadeiro quando LOG_LEVEL estava definido com valor não aceito.</summary>
    public bool FellBack { get; }

    public static LogLevelSetting Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new LogLevelSetting(LogLevel.Information, fellBack: false);
        }

        var match = Array.Find(Accepted, a => string.Equals(a, value.Trim(), StringComparison.Ordinal));
        return match is null
            ? new LogLevelSetting(LogLevel.Information, fellBack: true)
            : new LogLevelSetting(Enum.Parse<LogLevel>(match), fellBack: false);
    }
}
