using System.Reflection;
using System.Text.RegularExpressions;

namespace Portal.Api.Health;

/// <summary>
/// Versão do serviço lida uma única vez do AssemblyInformationalVersion, gravado a partir do
/// &lt;Version&gt; do Portal.Api.csproj (ADR-0002 v1.1). Formato inválido impede a inicialização.
/// </summary>
public sealed partial class ServiceVersion
{
    /// <summary>SemVer 2.0.0 sem metadados de build (openapi 1.0.0 ServiceVersion), ancorado com \z (R5).</summary>
    public const string Pattern = @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-((0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?\z";

    private ServiceVersion(string value) => Value = value;

    public string Value { get; }

    public static ServiceVersion FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return Parse(informational);
    }

    public static ServiceVersion Parse(string? value)
    {
        if (value is null || !VersionRegex().IsMatch(value))
        {
            throw new InvalidOperationException("Versão do serviço inválida (ADR-0002): a portal-api não inicia.");
        }

        return new ServiceVersion(value);
    }

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
