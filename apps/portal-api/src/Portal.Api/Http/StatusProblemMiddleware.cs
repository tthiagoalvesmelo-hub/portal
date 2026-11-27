namespace Portal.Api.Http;

/// <summary>
/// Completa com problem+json as respostas 404 (rota inexistente) e 405 (método não mapeado, inclusive
/// OPTIONS sem preflight) produzidas pelo roteamento sem corpo. O cabeçalho Allow vem do roteamento.
/// </summary>
public sealed class StatusProblemMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await next(context).ConfigureAwait(false);

        var status = context.Response.StatusCode;
        if (status is StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed
            && !context.Response.HasStarted
            && context.Response.ContentLength is null
            && string.IsNullOrEmpty(context.Response.ContentType))
        {
            await ProblemResponses.WriteAsync(context, status).ConfigureAwait(false);
        }
    }
}
