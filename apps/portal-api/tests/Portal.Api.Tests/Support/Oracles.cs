using System.Net.Http.Headers;

namespace Portal.Api.Tests.Support;

/// <summary>
/// Oráculos compartilhados (constantes e auxiliares), para que o ajuste pós-merge do PR #4 seja em um só lugar.
/// </summary>
public static class Oracles
{
    public const string AllowedOrigin = "http://127.0.0.1:4200";
    public const string OtherOrigin = "http://127.0.0.1:4201";
    public const string TestPort = "8080";
    public const string JsonContentType = "application/json; charset=utf-8";
    public const string ProblemContentType = "application/problem+json";
    public const string KnownRequestId = "3f2b8c4e-1a2b-4c3d-8e9f-0a1b2c3d4e5f";

    /// <summary>Instante simulado do CT-0003 (7 casas) e seu valor esperado (3 casas, truncado).</summary>
    public static readonly DateTimeOffset FixedInstant = new DateTimeOffset(2026, 11, 26, 15, 34, 56, TimeSpan.Zero).AddTicks(7891234);
    public const string FixedInstantFormatted = "2026-11-26T15:34:56.789Z";

    /// <summary>Allow: const 'GET, HEAD' do contrato (openapi 1.0.0 components.headers.Allow), comparado exatamente.</summary>
    public const string AllowHeader = "GET, HEAD";

    /// <summary>Access-Control-Allow-Methods: comparado como conjunto {GET, HEAD} (DEC-ARQ-0012 §1).</summary>
    public static readonly string[] AllowedMethodsSet = ["GET", "HEAD"];

    /// <summary>Lê um cabeçalho de resposta ou de conteúdo; null se ausente.</summary>
    public static string? Header(HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Headers.TryGetValues(name, out var values))
        {
            return string.Join(", ", values);
        }

        return response.Content.Headers.TryGetValues(name, out var contentValues) ? string.Join(", ", contentValues) : null;
    }

    /// <summary>
    /// Valor bruto de um cabeçalho de conteúdo como recebido (sem o parse do HttpClient): exige uma única linha.
    /// Usado no Allow para que "GET,HEAD" ou duas linhas separadas não passem como "GET, HEAD".
    /// </summary>
    public static string? RawContentHeader(HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Content.Headers.NonValidated.TryGetValues(name, out var values))
        {
            return null;
        }

        Assert.Single(values);
        return values.ToString();
    }

    /// <summary>Compara listas de métodos como conjunto (só para Access-Control-Allow-Methods, DEC-ARQ-0012 §1).</summary>
    public static string[] MethodSet(string? headerValue) =>
        (headerValue ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m.ToUpperInvariant())
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static void AssertStandardHeaders(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal("no-store", Header(response, "Cache-Control"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.True(ContractValidator.IsCanonicalUuid(Header(response, "X-Request-Id")), "X-Request-Id canônico");
        Assert.Null(Header(response, "Server"));
    }

    public static string ContentType(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        MediaTypeHeaderValue? value = response.Content.Headers.ContentType;
        return value?.ToString() ?? string.Empty;
    }
}
