namespace Portal.Api.Tests.Support;

/// <summary>
/// Referência única ao contrato (DEC-ARQ-0015): o openapi.yaml NÃO é copiado, não há submódulo nem acesso à rede.
/// Endereço só para rastreabilidade humana (os testes não leem a rede): <see cref="Url"/>.
/// Os oráculos deste projeto foram derivados do documento abaixo, na SHA indicada.
/// Mudança de contrato ⇒ atualizar esta referência e os oráculos no mesmo PR.
/// </summary>
public static class ContractReference
{
    public const string Repository = "tthiagoalvesmelo-hub/stack-conhecimento";
    public const string Path = "02-tecnica/contratos-api/portal-api/openapi.yaml";
    public const string InfoVersion = "1.0.0";
    public const string Sha = "e34e6b9";
    public const string Url = "https://github.com/tthiagoalvesmelo-hub/stack-conhecimento/blob/e34e6b9/02-tecnica/contratos-api/portal-api/openapi.yaml";
}
