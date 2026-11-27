using System.Text.Json;

namespace Portal.Api.Http;

/// <summary>
/// Corpo de erro padronizado (RFC 9457; DEC-ARQ-0002 L-01): somente type, title e status, sem traceId nem detalhe.
/// </summary>
public static class ProblemResponses
{
    public const string ContentType = "application/problem+json";
    public const string AboutBlank = "about:blank";

    public static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status405MethodNotAllowed => "Method Not Allowed",
        _ => "Internal Server Error",
    };

    public static async Task WriteAsync(HttpContext context, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = ContentType;
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", AboutBlank);
            writer.WriteString("title", TitleFor(statusCode));
            writer.WriteNumber("status", statusCode);
            writer.WriteEndObject();
        }

        await context.Response.Body.WriteAsync(stream.ToArray(), context.RequestAborted).ConfigureAwait(false);
    }
}
