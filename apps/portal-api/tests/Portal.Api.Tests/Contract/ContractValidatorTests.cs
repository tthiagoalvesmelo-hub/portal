using System.Reflection;
using System.Text.Json;

using Portal.Api.Health;
using Portal.Api.Http;
using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Contract;

/// <summary>R2 (format date-time) e R5 (âncora \z), com controles negativos obrigatórios (DEC-ARQ-0014).</summary>
public sealed class ContractValidatorTests
{
    [Fact]
    [Trait("CT", "CT-0004")]
    public async Task CorposReaisDe200E503PassamNoValidadorComFormatAtivo()
    {
        await using var ok = PortalApiFactory.Create();
        await using var unavailable = PortalApiFactory.Create(s => PortalApiFactory.Replace<IHealthState>(s, new UnavailableHealthState()));
        using var okClient = ok.CreateClient();
        using var unavailableClient = unavailable.CreateClient();

        using var okBody = JsonDocument.Parse(await okClient.GetStringAsync(new Uri("/health", UriKind.Relative)));
        using var response = await unavailableClient.GetAsync(new Uri("/health", UriKind.Relative));
        using var unavailableBody = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Empty(ContractValidator.ValidateHealthOk(okBody.RootElement));
        Assert.Empty(ContractValidator.ValidateHealthUnavailable(unavailableBody.RootElement));
    }

    [Fact]
    [Trait("CT", "CT-0004")]
    public void DataInexistenteQueCasaOPatternEhReprovada()
    {
        Assert.Matches(ContractValidator.TimestampPattern, "2026-02-31T10:00:00.000Z");
        Assert.False(ContractValidator.IsValidTimestamp("2026-02-31T10:00:00.000Z"));
        Assert.True(ContractValidator.IsValidTimestamp("2026-11-26T15:34:56.789Z"));
    }

    [Theory]
    [InlineData("2026-02-31T10:00:00.000Z")]
    [InlineData("2026-11-26T15:34:56Z")]
    [InlineData("2026-11-26T15:34:56.7891234Z")]
    [InlineData("2026-11-26T15:34:56.789Z\n")]
    [InlineData("2026-11-26T15:34:56.789+00:00")]
    [InlineData("2026-11-26T12:34:56.789-03:00")]
    [Trait("CT", "CT-0004")]
    public void NegativosDeTimestampSaoReprovadosPeloValidadorEPeloFormat(string value)
    {
        Assert.False(ContractValidator.IsValidTimestamp(value));
        Assert.False(ContractValidator.IsValidDateTimeFormat(value));
    }

    [Fact]
    [Trait("CT", "CT-0004")]
    public void FormatAceitaOValorPositivoDoContrato() =>
        Assert.True(ContractValidator.IsValidDateTimeFormat("2026-11-26T15:30:00.123Z"));

    [Fact]
    [Trait("CT", "CT-0004")]
    public void CorpoComDataInexistenteEhReprovadoPeloEsquema()
    {
        using var body = JsonDocument.Parse("""{"status":"ok","version":"0.1.0","timestamp":"2026-02-31T10:00:00.000Z"}""");

        Assert.NotEmpty(ContractValidator.ValidateHealthOk(body.RootElement));
    }

    [Theory]
    [InlineData("0.1.0\n")]
    [InlineData("2026-11-26T15:34:56.789Z\n")]
    [InlineData("3f2b8c4e-1a2b-4c3d-8e9f-0a1b2c3d4e5f\n")]
    [Trait("CT", "CT-0005")]
    public void ValorComQuebraDeLinhaFinalEhReprovado(string value)
    {
        Assert.False(ContractValidator.IsValidVersion(value));
        Assert.False(ContractValidator.IsValidTimestamp(value));
        Assert.False(ContractValidator.IsCanonicalUuid(value));

        // Âncora isolada: o format date-time também rejeitaria o "\n" do timestamp e mascararia um "$".
        Assert.DoesNotMatch(ContractValidator.VersionPattern, value);
        Assert.DoesNotMatch(ContractValidator.TimestampPattern, value);
        Assert.DoesNotMatch(ContractValidator.UuidPattern, value);
    }

    [Fact]
    [Trait("CT", "CT-0005")]
    public void ValoresSemQuebraDeLinhaSaoAprovados()
    {
        Assert.True(ContractValidator.IsValidVersion("0.1.0"));
        Assert.True(ContractValidator.IsValidTimestamp("2026-11-26T15:34:56.789Z"));
        Assert.True(ContractValidator.IsCanonicalUuid(Oracles.KnownRequestId));
    }

    [Fact]
    [Trait("CT", "CT-0005")]
    public void NenhumaExpressaoDosTestesOuDoServicoTerminaEmCifrao()
    {
        var patterns = typeof(ContractValidator).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name.EndsWith("Pattern", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Append(ServiceVersion.Pattern)
            .Append(RequestId.Pattern)
            .ToList();

        Assert.Equal(6, patterns.Count);
        Assert.All(patterns, p =>
        {
            Assert.EndsWith(@"\z", p, StringComparison.Ordinal);
            Assert.DoesNotContain("$", p, StringComparison.Ordinal);
        });
    }

    [Fact]
    [Trait("CT", "CT-0001")]
    public void ValidadorReprovaCampoExtraCampoAusenteEConstErrado()
    {
        using var extra = JsonDocument.Parse("""{"status":"ok","version":"0.1.0","timestamp":"2026-11-26T15:30:00.123Z","x":1}""");
        using var missing = JsonDocument.Parse("""{"status":"ok","version":"0.1.0"}""");
        using var wrongConst = JsonDocument.Parse("""{"status":"up","version":"0.1.0","timestamp":"2026-11-26T15:30:00.123Z"}""");
        using var withBuild = JsonDocument.Parse("""{"status":"ok","version":"0.1.0+abc","timestamp":"2026-11-26T15:30:00.123Z"}""");
        using var notObject = JsonDocument.Parse("[]");
        using var problem = JsonDocument.Parse("""{"type":"about:blank","title":"Not Found","status":"404"}""");

        Assert.NotEmpty(ContractValidator.ValidateHealthOk(extra.RootElement));
        Assert.NotEmpty(ContractValidator.ValidateHealthOk(missing.RootElement));
        Assert.NotEmpty(ContractValidator.ValidateHealthOk(wrongConst.RootElement));
        Assert.NotEmpty(ContractValidator.ValidateHealthOk(withBuild.RootElement));
        Assert.NotEmpty(ContractValidator.ValidateHealthOk(notObject.RootElement));
        Assert.NotEmpty(ContractValidator.ValidateProblem(problem.RootElement, "Not Found", 404));
        Assert.NotEmpty(ContractValidator.ValidateProblem(notObject.RootElement, "Not Found", 404));
    }

    /// <summary>Sensibilidade do oráculo exato do Allow (S3 do CR): variações que um conjunto aceitaria são reprovadas.</summary>
    [Theory]
    [InlineData("GET, HEAD", true)]
    [InlineData("GET,HEAD", false)]
    [InlineData("HEAD, GET", false)]
    [InlineData("get, head", false)]
    [Trait("CT", "CT-0008")]
    public void OraculoDoAllowComparaOValorBrutoExato(string sent, bool expected)
    {
        using var response = new HttpResponseMessage { Content = new ByteArrayContent([]) };
        Assert.True(response.Content.Headers.TryAddWithoutValidation("Allow", sent));

        Assert.Equal(expected, Oracles.AllowHeader == Oracles.RawContentHeader(response, "Allow"));
    }

    [Fact]
    public void ReferenciaAoContratoApontaParaASHAAprovada()
    {
        Assert.Equal("tthiagoalvesmelo-hub/stack-conhecimento", ContractReference.Repository);
        Assert.Equal("02-tecnica/contratos-api/portal-api/openapi.yaml", ContractReference.Path);
        Assert.Equal("1.0.0", ContractReference.InfoVersion);
        Assert.Equal("e34e6b9", ContractReference.Sha);
        Assert.StartsWith("https://", ContractReference.Url, StringComparison.Ordinal);
        Assert.Contains(ContractReference.Sha, ContractReference.Url, StringComparison.Ordinal);
        Assert.EndsWith(ContractReference.Path, ContractReference.Url, StringComparison.Ordinal);
    }
}
