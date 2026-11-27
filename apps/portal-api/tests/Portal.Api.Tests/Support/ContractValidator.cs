using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Portal.Api.Tests.Support;

/// <summary>
/// Validador explícito dos esquemas do contrato (DEC-ARQ-0014, PS-09), sem pacote de JSON Schema.
/// Cobre const, required, additionalProperties: false, pattern ancorado com \z (R5) e format date-time (R2).
/// Retorna a lista de violações; lista vazia = válido.
/// </summary>
public static class ContractValidator
{
    /// <summary>ServiceVersion (openapi 1.0.0), com \A…\z no lugar de ^…$.</summary>
    public const string VersionPattern = @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-((0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?\z";

    /// <summary>Timestamp (openapi 1.0.0), com \A…\z.</summary>
    public const string TimestampPattern = @"\A[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]\.[0-9]{3}Z\z";

    /// <summary>XRequestIdResponse (openapi 1.0.0), com \A…\z.</summary>
    public const string UuidPattern = @"\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z";

    /// <summary>UUID versão 4 canônico (variante RFC 4122), para o valor gerado pelo servidor.</summary>
    public const string UuidV4Pattern = @"\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z";

    private static readonly string[] HealthFields = ["status", "version", "timestamp"];
    private static readonly string[] ProblemFields = ["type", "title", "status"];

    public static bool IsValidVersion(string? value) => value is not null && Regex.IsMatch(value, VersionPattern, RegexOptions.CultureInvariant);

    public static bool IsCanonicalUuid(string? value) => value is not null && Regex.IsMatch(value, UuidPattern, RegexOptions.CultureInvariant);

    public static bool IsUuidV4(string? value) => value is not null && Regex.IsMatch(value, UuidV4Pattern, RegexOptions.CultureInvariant);

    /// <summary>pattern E format date-time: uma data inexistente que casa o pattern é reprovada aqui.</summary>
    public static bool IsValidTimestamp(string? value) =>
        value is not null
        && Regex.IsMatch(value, TimestampPattern, RegexOptions.CultureInvariant)
        && IsValidDateTimeFormat(value);

    /// <summary>
    /// format: date-time (RFC 3339) restrito ao formato do contrato, com DateTimeOffset: valida calendário real,
    /// exige o designador Z (offset zero) e exatamente 3 casas; não aceita espaço nem \n.
    /// </summary>
    public static bool IsValidDateTimeFormat(string value) =>
        DateTimeOffset.TryParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out _);

    public static IReadOnlyList<string> ValidateHealthOk(JsonElement body) => ValidateHealth(body, "ok");

    public static IReadOnlyList<string> ValidateHealthUnavailable(JsonElement body) => ValidateHealth(body, "unavailable");

    public static IReadOnlyList<string> ValidateProblem(JsonElement body, string title, int status)
    {
        var errors = new List<string>();
        if (!CheckObject(body, ProblemFields, errors))
        {
            return errors;
        }

        CheckConstString(body, "type", "about:blank", errors);
        CheckConstString(body, "title", title, errors);
        var statusElement = body.GetProperty("status");
        if (statusElement.ValueKind != JsonValueKind.Number || !statusElement.TryGetInt32(out var actual) || actual != status)
        {
            errors.Add($"status: esperado inteiro {status}");
        }

        return errors;
    }

    private static List<string> ValidateHealth(JsonElement body, string expectedStatus)
    {
        var errors = new List<string>();
        if (!CheckObject(body, HealthFields, errors))
        {
            return errors;
        }

        CheckConstString(body, "status", expectedStatus, errors);
        var version = body.GetProperty("version");
        if (version.ValueKind != JsonValueKind.String || !IsValidVersion(version.GetString()))
        {
            errors.Add("version: fora do pattern ServiceVersion");
        }

        var timestamp = body.GetProperty("timestamp");
        if (timestamp.ValueKind != JsonValueKind.String || !IsValidTimestamp(timestamp.GetString()))
        {
            errors.Add("timestamp: fora do pattern/format date-time");
        }

        return errors;
    }

    /// <summary>type: object, required e additionalProperties: false.</summary>
    private static bool CheckObject(JsonElement body, string[] fields, List<string> errors)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            errors.Add("corpo: esperado objeto");
            return false;
        }

        foreach (var property in body.EnumerateObject())
        {
            if (Array.IndexOf(fields, property.Name) < 0)
            {
                errors.Add($"additionalProperties: campo não previsto '{property.Name}'");
            }
        }

        foreach (var field in fields)
        {
            if (!body.TryGetProperty(field, out _))
            {
                errors.Add($"required: campo ausente '{field}'");
            }
        }

        return errors.Count == 0;
    }

    private static void CheckConstString(JsonElement body, string field, string expected, List<string> errors)
    {
        var element = body.GetProperty(field);
        if (element.ValueKind != JsonValueKind.String || !string.Equals(element.GetString(), expected, StringComparison.Ordinal))
        {
            errors.Add($"{field}: esperado const '{expected}'");
        }
    }
}
