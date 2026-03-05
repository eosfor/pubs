using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Internal;

internal static class TlsCertificateValidation
{
    private static readonly ConcurrentDictionary<string, byte> WarnedFailures = new(StringComparer.Ordinal);

    public static void Apply(ServiceBusClientOptions options, bool ignoreChainErrors, Action<string>? warningWriter)
    {
        options.CertificateValidationCallback = (_, certificate, chain, sslPolicyErrors) =>
            Validate(certificate, chain, sslPolicyErrors, ignoreChainErrors, warningWriter);
    }

    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> CreateHttpCallback(
        bool ignoreChainErrors,
        Action<string>? warningWriter)
    {
        return (_, certificate, chain, sslPolicyErrors) =>
            Validate(certificate, chain, sslPolicyErrors, ignoreChainErrors, warningWriter);
    }

    private static bool Validate(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        bool ignoreChainErrors,
        Action<string>? warningWriter)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        var hasChainErrors = (sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0;
        var hasNonChainErrors = (sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0;

        var summary = BuildFailureSummary(certificate, chain, sslPolicyErrors);
        var canIgnore = ignoreChainErrors && hasChainErrors && !hasNonChainErrors;
        var outcome = canIgnore ? "connection will continue because IgnoreCertificateChainErrors is enabled." : "connection will be rejected.";
        WriteWarningOnce(warningWriter, $"{summary} {outcome}");

        return canIgnore;
    }

    private static void WriteWarningOnce(Action<string>? warningWriter, string warning)
    {
        if (warningWriter is null)
        {
            return;
        }

        if (!WarnedFailures.TryAdd(warning, 0))
        {
            return;
        }

        warningWriter(warning);
    }

    private static string BuildFailureSummary(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        var subject = certificate?.Subject ?? "<unknown>";
        var issuer = certificate?.Issuer ?? "<unknown>";
        var thumbprint = certificate?.GetCertHashString() ?? "<unknown>";
        var chainDetails = BuildChainDetails(chain);

        return $"TLS certificate validation failed ({sslPolicyErrors}). Subject='{subject}', Issuer='{issuer}', Thumbprint='{thumbprint}'. Chain details: {chainDetails}";
    }

    private static string BuildChainDetails(X509Chain? chain)
    {
        if (chain is null)
        {
            return "Chain object is null.";
        }

        var details = chain.ChainStatus
            .Where(status => status.Status != X509ChainStatusFlags.NoError)
            .Select(status =>
            {
                var info = string.IsNullOrWhiteSpace(status.StatusInformation)
                    ? "no additional info"
                    : status.StatusInformation.Trim();
                return $"{status.Status}: {info}";
            })
            .ToArray();

        if (details.Length == 0)
        {
            return "No chain status flags were provided.";
        }

        return string.Join("; ", details);
    }
}
