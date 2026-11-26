using Microsoft.Extensions.Configuration;

using Portal.Api.Configuration;
using Portal.Api.Health;

namespace Portal.Api.Tests.Unit;

/// <summary>ADR-0001 v2.0 D7 (nomes e fail closed), LOG_LEVEL e versão do serviço (ADR-0002).</summary>
public sealed class ConfigurationTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void ConfiguracaoValidaUsaHostPadraoLoopback()
    {
        var settings = PortalApiSettings.Load(Config(("PORTAL_API_PORT", "8080"), ("PORTAL_API_ALLOWED_ORIGIN", "http://127.0.0.1:4200")));

        Assert.Equal("127.0.0.1", settings.Host.ToString());
        Assert.Equal(8080, settings.Port);
        Assert.Equal("http://127.0.0.1:4200", settings.AllowedOrigin);
        Assert.Equal(LogLevel.Information, settings.LogLevel.Level);
        Assert.False(settings.LogLevel.FellBack);
    }

    [Theory]
    [InlineData("PORTAL_API_PORT", null)]
    [InlineData("PORTAL_API_PORT", "0")]
    [InlineData("PORTAL_API_PORT", "70000")]
    [InlineData("PORTAL_API_PORT", "80a")]
    [InlineData("PORTAL_API_HOST", "localhost")]
    [InlineData("PORTAL_API_ALLOWED_ORIGIN", "http://127.0.0.1:4200/")]
    [InlineData("PORTAL_API_ALLOWED_ORIGIN", "ftp://127.0.0.1:4200")]
    [InlineData("PORTAL_API_ALLOWED_ORIGIN", "http://user@127.0.0.1:4200")]
    [InlineData("PORTAL_API_ALLOWED_ORIGIN", "http://127.0.0.1:4200?a=1")]
    [InlineData("PORTAL_API_ALLOWED_ORIGIN", "http://127.0.0.1:4200#x")]
    [InlineData("PORTAL_API_ALLOWED_ORIGIN", "http://*.local")]
    public void ValorInvalidoImpedeAInicializacaoSemEcoarOValor(string key, string? value)
    {
        var values = new Dictionary<string, string?>
        {
            ["PORTAL_API_PORT"] = "8080",
            ["PORTAL_API_ALLOWED_ORIGIN"] = "http://127.0.0.1:4200",
            [key] = value,
        };

        var error = Assert.Throws<InvalidOperationException>(() => PortalApiSettings.Load(Config(values.Select(kv => (kv.Key, kv.Value)).ToArray())));

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(value))
        {
            Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("Debug", LogLevel.Debug)]
    [InlineData("Information", LogLevel.Information)]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("Error", LogLevel.Error)]
    [InlineData("Critical", LogLevel.Critical)]
    [InlineData("None", LogLevel.None)]
    public void LogLevelAceitoEhMapeado(string value, LogLevel expected)
    {
        var setting = LogLevelSetting.Parse(value);

        Assert.Equal(expected, setting.Level);
        Assert.False(setting.FellBack);
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("debug")]
    [InlineData("5")]
    public void LogLevelInvalidoCaiEmInformationComAviso(string value)
    {
        var setting = LogLevelSetting.Parse(value);

        Assert.Equal(LogLevel.Information, setting.Level);
        Assert.True(setting.FellBack);
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0-rc.1")]
    [InlineData("10.20.30")]
    public void VersaoSemVerValidaEhAceita(string value) => Assert.Equal(value, ServiceVersion.Parse(value).Value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0.1.0+abc1234")]
    [InlineData("01.1.0")]
    [InlineData("0.1")]
    [InlineData("0.1.0\n")]
    public void VersaoInvalidaImpedeAInicializacao(string? value) =>
        Assert.Throws<InvalidOperationException>(() => ServiceVersion.Parse(value));

    [Fact]
    public void VersaoDoAssemblyDeProducaoEhLidaDoCsproj() =>
        Assert.Equal("0.1.0", ServiceVersion.FromAssembly(typeof(ServiceVersion).Assembly).Value);
}
