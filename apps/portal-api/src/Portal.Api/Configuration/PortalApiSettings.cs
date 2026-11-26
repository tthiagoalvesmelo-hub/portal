using System.Globalization;
using System.Net;

namespace Portal.Api.Configuration;

/// <summary>
/// Configuração da portal-api lida dos nomes oficiais do ADR-0001 v2.0 D7.
/// Falha de validação impede a inicialização (fail closed) sem ecoar o valor recebido.
/// </summary>
public sealed class PortalApiSettings
{
    public const string HostKey = "PORTAL_API_HOST";
    public const string PortKey = "PORTAL_API_PORT";
    public const string AllowedOriginKey = "PORTAL_API_ALLOWED_ORIGIN";
    public const string LogLevelKey = "LOG_LEVEL";

    private const string DefaultHost = "127.0.0.1";

    private PortalApiSettings(IPAddress host, int port, string allowedOrigin, LogLevelSetting logLevel)
    {
        Host = host;
        Port = port;
        AllowedOrigin = allowedOrigin;
        LogLevel = logLevel;
    }

    public IPAddress Host { get; }

    public int Port { get; }

    public string AllowedOrigin { get; }

    public LogLevelSetting LogLevel { get; }

    public static PortalApiSettings Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var hostValue = configuration[HostKey];
        var host = string.IsNullOrWhiteSpace(hostValue) ? DefaultHost : hostValue.Trim();
        if (!IPAddress.TryParse(host, out var address))
        {
            throw Invalid(HostKey);
        }

        var portValue = configuration[PortKey];
        if (!int.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            throw Invalid(PortKey);
        }

        var origin = configuration[AllowedOriginKey];
        if (!IsExactOrigin(origin))
        {
            throw Invalid(AllowedOriginKey);
        }

        return new PortalApiSettings(address, port, origin!, LogLevelSetting.Parse(configuration[LogLevelKey]));
    }

    /// <summary>
    /// Origem exata (DEC-ARQ-0002 L-06): http/https absoluta, sem curinga, credenciais, caminho, query ou fragmento.
    /// </summary>
    public static bool IsExactOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return string.Equals(uri.GetLeftPart(UriPartial.Authority), value, StringComparison.Ordinal);
    }

    private static InvalidOperationException Invalid(string key) =>
        new($"Configuração inválida: {key} ausente ou fora do formato esperado. A portal-api não inicia (fail closed).");
}
