using System;
using System.Text.RegularExpressions;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell;

internal static class ServiceBusAdminClientFactory
{
    public static ServiceBusAdministrationClient Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("ServiceBusConnectionString is required.", nameof(connectionString));
        }

        var adjusted = AdjustForEmulator(connectionString);
        return new ServiceBusAdministrationClient(adjusted);
    }

    private static string AdjustForEmulator(string connectionString)
    {
        if (!connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        try
        {
            var match = Regex.Match(connectionString, @"Endpoint=sb://([^;]+);", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return connectionString;
            }

            var host = match.Groups[1].Value;
            if (host.Contains(":", StringComparison.Ordinal))
            {
                return connectionString;
            }

            var port = 5300;
            var envPort = Environment.GetEnvironmentVariable("EMULATOR_HTTP_PORT");
            if (!string.IsNullOrWhiteSpace(envPort) && int.TryParse(envPort, out var parsed) && parsed > 0)
            {
                port = parsed;
            }

            var withPort = $"Endpoint=sb://{host}:{port};";
            var regex = new Regex(@"Endpoint=sb://[^;]+;", RegexOptions.IgnoreCase);
            return regex.Replace(connectionString, withPort, 1);
        }
        catch
        {
            // If parsing fails, keep the original connection string to avoid masking the real error.
            return connectionString;
        }
    }
}
