namespace Portal.Api.Http;

/// <summary>
/// Cabeçalhos de toda resposta (ADR-0001 v2.0 D8 item 6; DEC-ARQ-0002 L-03/L-04):
/// Cache-Control: no-store, X-Content-Type-Options: nosniff e X-Request-Id, inclusive em 404, 405, 500 e preflight.
/// Aplicados no início do envio para sobreviver a respostas reescritas.
/// </summary>
public sealed class StandardHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var requestId = RequestId.Resolve(context);
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.CacheControl = "no-store";
            headers.XContentTypeOptions = "nosniff";
            headers[RequestId.HeaderName] = requestId;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
