namespace Portal.Api.Health;

/// <summary>Espera da janela de drenagem executada dentro do ApplicationStopping (DEC-ARQ-0012 §3).</summary>
public interface IShutdownDrain
{
    void Wait();
}
