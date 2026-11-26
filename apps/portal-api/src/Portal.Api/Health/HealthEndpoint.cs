using System.Globalization;
using System.Text.Json;

using Portal.Api.Http;

namespace Portal.Api.Health;

/// <summary>GET/HEAD /health (US-0001; openapi 1.0.0). Somente leitura, sem parâmetros.</summary>
public static partial class HealthEndpoint
{
    public const string Route = "/health";
    public const string StatusOk = "ok";
    public const string StatusUnavailable = "unavailable";
    public const string JsonContentType = "application/json; charset=utf-8";
    public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, HandleAsync);
        endpoints.MapMethods(Route, [HttpMethods.Head], HandleAsync);
        return endpoints;
    }

    public static string FormatTimestamp(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static async Task HandleAsync(
        HttpContext context,
        IHealthState state,
        IInternalFailureSource failureSource,
        TimeProvider timeProvider,
        ServiceVersion version,
        ILoggerFactory loggerFactory)
    {
        failureSource.ThrowIfFailed();

        var unavailable = state.IsUnavailable;
        var statusCode = unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;
        var body = Serialize(unavailable ? StatusUnavailable : StatusOk, version.Value, FormatTimestamp(timeProvider.GetUtcNow()));

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = JsonContentType;

        var logger = loggerFactory.CreateLogger(typeof(HealthEndpoint).FullName!);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var method = context.Request.Method;
            var requestId = RequestId.Get(context);
            LogResponded(logger, method, statusCode, requestId);
        }

        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.Body.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static byte[] Serialize(string status, string version, string timestamp)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("status", status);
            writer.WriteString("version", version);
            writer.WriteString("timestamp", timestamp);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "{Method} /health respondido com {StatusCode} (X-Request-Id {RequestId})")]
    private static partial void LogResponded(ILogger logger, string method, int statusCode, string requestId);
}
