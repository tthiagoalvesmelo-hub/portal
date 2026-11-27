using System.Text.RegularExpressions;

namespace Portal.Api.Http;

/// <summary>
/// X-Request-Id (DEC-ARQ-0002 L-03): UUID canônico em hexadecimal minúsculo é aceito e devolvido;
/// ausente ou inválido gera UUID v4 novo. Nunca responde 400.
/// </summary>
public static partial class RequestId
{
    public const string HeaderName = "X-Request-Id";

    /// <summary>UUID canônico, ancorado com \z (R5).</summary>
    public const string Pattern = @"\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z";

    private static readonly object ItemKey = new();

    public static string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var received = context.Request.Headers[HeaderName];
        var value = received.Count == 1 && received[0] is { } candidate && UuidRegex().IsMatch(candidate)
            ? candidate
            : Guid.NewGuid().ToString("D");
        context.Items[ItemKey] = value;
        return value;
    }

    public static string Get(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(ItemKey, out var value) && value is string id ? id : Resolve(context);
    }

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex UuidRegex();
}
