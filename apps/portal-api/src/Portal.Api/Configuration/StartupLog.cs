namespace Portal.Api.Configuration;

/// <summary>Registros da partida da aplicação.</summary>
public static partial class StartupLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "LOG_LEVEL inválido; usando Information")]
    public static partial void InvalidLogLevel(ILogger logger);
}
