using Microsoft.Net.Http.Headers;

namespace Portal.Api.Http;

/// <summary>
/// Erro inesperado ⇒ 500 com problem+json mínimo, em qualquer ambiente (inclusive Development),
/// antes que a página de exceção do desenvolvedor possa responder. Detalhe só no log Error.
/// </summary>
public sealed partial class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await next(context).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Fronteira do pipeline: toda exceção vira 500 padronizado.
        catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogUnexpected(logger, ex, RequestId.Get(context));
            var allowOrigin = context.Response.Headers[HeaderNames.AccessControlAllowOrigin];
            context.Response.Clear();
            if (allowOrigin.Count > 0)
            {
                context.Response.Headers[HeaderNames.AccessControlAllowOrigin] = allowOrigin;
            }

            await ProblemResponses.WriteAsync(context, StatusCodes.Status500InternalServerError).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Error, Message = "Falha inesperada ao processar a requisição (X-Request-Id {RequestId})")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string requestId);
}
