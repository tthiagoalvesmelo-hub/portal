using System.Net;
using System.Text.RegularExpressions;

namespace Portal.Api.Tests.Support;

/// <summary>
/// Denylist do RN-0001 / CT-0016 / CT-0025: nada de dado pessoal, credencial, token, nome de máquina, IP,
/// caminho de arquivo, versão de runtime/dependência, hash de commit, valor de variável de ambiente ou stack trace.
/// </summary>
public static partial class Denylist
{
    public static IReadOnlyList<string> Findings(string text, bool allowStackTrace = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var findings = new List<string>();
        void Check(bool hit, string what)
        {
            if (hit)
            {
                findings.Add(what);
            }
        }

        Check(text.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase), "nome da máquina");
        Check(text.Contains(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase), "hostname");
        Check(Ipv4().IsMatch(text), "IP");
        Check(text.Contains(Environment.Version.ToString(), StringComparison.Ordinal), "versão do runtime");
        Check(text.Contains(".NET", StringComparison.OrdinalIgnoreCase) || text.Contains("Kestrel", StringComparison.OrdinalIgnoreCase), "runtime/servidor");
        Check(FilePath().IsMatch(text) || text.Contains(AppContext.BaseDirectory, StringComparison.Ordinal), "caminho de arquivo");
        Check(CommitHash().IsMatch(text), "hash de commit");
        Check(Credential().IsMatch(text), "credencial/token");
        Check(Email().IsMatch(text), "dado pessoal (e-mail)");
        Check(!allowStackTrace && (text.Contains("   at ", StringComparison.Ordinal) || text.Contains("Exception", StringComparison.Ordinal)), "stack trace/exceção");

        foreach (var name in new[] { "HOME", "USER", "PATH", "HOSTNAME" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value) && value.Length >= 4)
            {
                Check(text.Contains(value, StringComparison.Ordinal), $"valor de variável de ambiente {name}");
            }
        }

        return findings;
    }

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4();

    [GeneratedRegex(@"(?:/(?:home|workspace|usr|tmp|var|etc|opt|root)/)|(?:[A-Za-z]:\\)", RegexOptions.CultureInvariant)]
    private static partial Regex FilePath();

    [GeneratedRegex(@"\b[0-9a-f]{40}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CommitHash();

    [GeneratedRegex(@"(?i)(password|senha|secret|token|bearer|api[_-]?key|ghp_|github_pat_)", RegexOptions.CultureInvariant)]
    private static partial Regex Credential();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();
}
