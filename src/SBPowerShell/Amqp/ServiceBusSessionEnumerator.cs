using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.Azure.Amqp.Framing;

namespace SBPowerShell.Amqp;

internal static class ServiceBusSessionEnumerator
{
    // Non-public Service Bus AMQP management operation used by legacy Track1 SDK (`GetMessageSessions`).
    private const string EnumerateSessionsOperation = "com.microsoft:get-message-sessions";
    private const string ManagementStatusCode = "statusCode";
    private const string ManagementStatusDescription = "statusDescription";
    private const string LegacyManagementStatusCode = "status-code";
    private const string LegacyManagementStatusDescription = "status-description";
    private const string LastUpdatedTimeKey = "last-updated-time";
    private const string SkipKey = "skip";
    private const string TopKey = "top";
    private const string LastSessionIdKey = "last-session-id";
    private const string SessionsIdsKey = "sessions-ids";
    private const int PageSize = 100;
    private static readonly DateTime AllSessionsSentinelUtc = new(DateTime.MaxValue.Ticks, DateTimeKind.Utc);

    public static async Task<IReadOnlyList<string>> GetSessionsAsync(
        string connectionString,
        string entityPath,
        bool activeOnly,
        DateTime? lastUpdatedSince,
        TimeSpan operationTimeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(entityPath))
        {
            throw new ArgumentException("Entity path is required.", nameof(entityPath));
        }

        var cs = ServiceBusConnectionStringProperties.Parse(connectionString);
        if (cs.Endpoint is null)
        {
            throw new InvalidOperationException("Service Bus endpoint was not found in the connection string.");
        }

        var host = cs.FullyQualifiedNamespace ?? cs.Endpoint.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Service Bus host could not be resolved from the connection string.");
        }

        var normalizedEntityPath = NormalizeEntityPathForAmqp(entityPath.Trim('/'));
        var resourceUri = new Uri(cs.Endpoint, normalizedEntityPath);
        var amqpUri = BuildAmqpConnectionUri(connectionString, cs.Endpoint, host);

        AmqpConnection? connection = null;
        AmqpSession? session = null;
        RequestResponseAmqpLink? managementLink = null;
        AmqpCbsLink? cbsLink = null;

        try
        {
            var connectionFactory = new AmqpConnectionFactory();
            connection = await connectionFactory.OpenConnectionAsync(amqpUri, cancellationToken).ConfigureAwait(false);

            // Authorize the AMQP connection via CBS before opening the entity management link.
            cbsLink = new AmqpCbsLink(connection);
            var tokenProvider = new ServiceBusCbsTokenProvider(cs);
            var audience = resourceUri.AbsoluteUri;
            await cbsLink.SendTokenAsync(tokenProvider, cs.Endpoint, audience, audience, Array.Empty<string>(), cancellationToken)
                .ConfigureAwait(false);

            // The management request/response link is scoped to "<entity>/$management".
            session = connection.CreateSession(new AmqpSessionSettings { Properties = new Fields() });
            await session.OpenAsync(cancellationToken).ConfigureAwait(false);

            var attachProperties = new Fields();
            attachProperties.Add(CbsConstants.TimeoutName, (uint)operationTimeout.TotalMilliseconds);
            managementLink = new RequestResponseAmqpLink("mgmt", session, $"{normalizedEntityPath}/$management", attachProperties);
            await managementLink.OpenAsync(cancellationToken).ConfigureAwait(false);

            var effectiveLastUpdated = ResolveLastUpdated(activeOnly, lastUpdatedSince);
            return await EnumerateAllPagesAsync(managementLink, effectiveLastUpdated, operationTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { managementLink?.Close(); } catch { }
            try { session?.SafeClose(); } catch { }
            try { cbsLink?.Close(); } catch { }
            try { connection?.SafeClose(); } catch { }
        }
    }

    private static DateTime? ResolveLastUpdated(bool activeOnly, DateTime? lastUpdatedSince)
    {
        if (activeOnly)
        {
            // Legacy Track1 SDK parameterless GetMessageSessions() also sends DateTime.MaxValue.
            // XML docs describe "active messages only", but the implementation maps to the same sentinel.
            return AllSessionsSentinelUtc;
        }

        if (lastUpdatedSince.HasValue)
        {
            return lastUpdatedSince.Value.Kind == DateTimeKind.Utc
                ? lastUpdatedSince.Value
                : lastUpdatedSince.Value.ToUniversalTime();
        }

        // Azure Service Bus management operation uses DateTime.MaxValue as a sentinel to return all sessions.
        return AllSessionsSentinelUtc;
    }

    private static Uri BuildAmqpConnectionUri(string connectionString, Uri endpoint, string host)
    {
        if (IsDevelopmentEmulator(connectionString))
        {
            // Emulator uses plain AMQP on 5672 (or EMULATOR_AMQP_PORT), not TLS on 5671.
            var port = endpoint.IsDefaultPort || endpoint.Port <= 0
                ? ResolveEmulatorAmqpPort()
                : endpoint.Port;

            return new UriBuilder(Uri.UriSchemeNetTcp, host)
            {
                Scheme = "amqp",
                Port = port
            }.Uri;
        }

        var tlsPort = endpoint.IsDefaultPort || endpoint.Port <= 0 ? 5671 : endpoint.Port;
        return new UriBuilder(Uri.UriSchemeNetTcp, host)
        {
            Scheme = "amqps",
            Port = tlsPort
        }.Uri;
    }

    private static bool IsDevelopmentEmulator(string connectionString)
    {
        return connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEntityPathForAmqp(string entityPath)
    {
        // Azure validates SAS/canonical resource URI strictly; subscription segment must be lowercase.
        return entityPath.Replace("/Subscriptions/", "/subscriptions/", StringComparison.Ordinal)
            .Replace("/subscriptions/", "/subscriptions/", StringComparison.Ordinal);
    }

    private static int ResolveEmulatorAmqpPort()
    {
        var envPort = Environment.GetEnvironmentVariable("EMULATOR_AMQP_PORT");
        return int.TryParse(envPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port > 0
            ? port
            : 5672;
    }

    private static async Task<IReadOnlyList<string>> EnumerateAllPagesAsync(
        RequestResponseAmqpLink managementLink,
        DateTime? lastUpdatedTime,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skip = 0;

        while (true)
        {
            // Service returns sessions in pages; we advance using response skip/count similarly to Track1.
            var response = await RequestPageAsync(managementLink, lastUpdatedTime, skip, PageSize, timeout, cancellationToken)
                .ConfigureAwait(false);

            if (response.SessionIds.Count == 0)
            {
                break;
            }

            foreach (var sessionId in response.SessionIds)
            {
                if (!string.IsNullOrWhiteSpace(sessionId) && seen.Add(sessionId))
                {
                    results.Add(sessionId);
                }
            }

            var nextSkip = response.ResponseSkip + response.SessionIds.Count;
            if (nextSkip <= skip)
            {
                nextSkip = skip + response.SessionIds.Count;
            }

            if (nextSkip <= skip)
            {
                break;
            }

            skip = nextSkip;
        }

        return results;
    }

    private static async Task<PageResponse> RequestPageAsync(
        RequestResponseAmqpLink managementLink,
        DateTime? lastUpdatedTime,
        int skip,
        int top,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(lastUpdatedTime, skip, top);
        // RequestResponseAmqpLink.RequestAsync(timeout) internally correlates request/response via message-id.
        using var response = await managementLink.RequestAsync(request, timeout).ConfigureAwait(false);

        EnsureSuccessResponse(response);
        return ParsePageResponse(response);
    }

    private static AmqpMessage BuildRequest(DateTime? lastUpdatedTime, int skip, int top)
    {
        // Microsoft.Azure.Amqp does not accept a regular Dictionary here; the body must be an AMQP map type.
        var bodyMap = new AmqpMap(new Hashtable
        {
            [SkipKey] = skip,
            [TopKey] = top
        });

        if (lastUpdatedTime.HasValue)
        {
            bodyMap[new MapKey(LastUpdatedTimeKey)] = lastUpdatedTime.Value;
        }

        var request = AmqpMessage.Create(new AmqpValue { Value = bodyMap });
        _ = request.Properties;
        // Service Bus management operations are selected through the application-properties["operation"] field.
        request.ApplicationProperties.Map["operation"] = EnumerateSessionsOperation;
        return request;
    }

    private static void EnsureSuccessResponse(AmqpMessage response)
    {
        if (response.ApplicationProperties?.Map is not { } map)
        {
            throw new InvalidOperationException("Service Bus management response is missing application properties.");
        }

        var statusCode = TryGetMapInt(map, ManagementStatusCode)
            ?? TryGetMapInt(map, LegacyManagementStatusCode)
            ?? throw new InvalidOperationException("Service Bus management response is missing status code.");

        if (statusCode is >= 200 and < 300)
        {
            return;
        }

        var description = TryGetMapString(map, ManagementStatusDescription)
            ?? TryGetMapString(map, LegacyManagementStatusDescription)
            ?? "Unknown Service Bus management error.";

        throw new InvalidOperationException($"Service Bus management operation '{EnumerateSessionsOperation}' failed: {statusCode} {description}");
    }

    private static PageResponse ParsePageResponse(AmqpMessage response)
    {
        if (response.ValueBody?.Value is not { } value)
        {
            return new PageResponse(0, Array.Empty<string>());
        }

        // Response shape is an AMQP map with fields like "skip" and "sessions-ids".
        var bodyEntries = EnumerateMapEntries(value);
        var responseSkip = 0;
        List<string>? sessionIds = null;

        foreach (var (key, rawValue) in bodyEntries)
        {
            if (key.Equals(SkipKey, StringComparison.OrdinalIgnoreCase))
            {
                responseSkip = ConvertToInt32(rawValue, SkipKey);
                continue;
            }

            if (key.Equals(SessionsIdsKey, StringComparison.OrdinalIgnoreCase))
            {
                sessionIds = ConvertToStringList(rawValue);
            }
        }

        return new PageResponse(responseSkip, sessionIds ?? new List<string>());
    }

    private static IEnumerable<(string Key, object? Value)> EnumerateMapEntries(object mapLike)
    {
        if (mapLike is null)
        {
            yield break;
        }

        if (mapLike is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    yield return (key!, entry.Value);
                }
            }

            yield break;
        }

        if (mapLike is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                var itemType = item.GetType();
                var keyProp = itemType.GetProperty("Key");
                var valueProp = itemType.GetProperty("Value");
                if (keyProp is null || valueProp is null)
                {
                    continue;
                }

                var keyObj = keyProp.GetValue(item);
                var key = keyObj?.ToString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    yield return (key!, valueProp.GetValue(item));
                }
            }
        }
    }

    private static int? TryGetMapInt(PropertiesMap map, string key)
    {
        if (map.TryGetValue<int>(key, out var asInt))
        {
            return asInt;
        }

        if (map.TryGetValue<long>(key, out var asLong))
        {
            return checked((int)asLong);
        }

        if (map.TryGetValue<uint>(key, out var asUInt))
        {
            return checked((int)asUInt);
        }

        if (map.TryGetValue<object>(key, out var raw))
        {
            return ConvertToNullableInt32(raw);
        }

        return null;
    }

    private static string? TryGetMapString(PropertiesMap map, string key)
    {
        if (map.TryGetValue<string>(key, out var value))
        {
            return value;
        }

        if (map.TryGetValue<object>(key, out var raw))
        {
            return raw?.ToString();
        }

        return null;
    }

    private static int ConvertToInt32(object? value, string name)
    {
        return ConvertToNullableInt32(value)
            ?? throw new InvalidOperationException($"Management response field '{name}' is not an integer.");
    }

    private static int? ConvertToNullableInt32(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            uint ui => checked((int)ui),
            short s => s,
            ushort us => us,
            byte b => b,
            sbyte sb => sb,
            string str when int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static List<string> ConvertToStringList(object? value)
    {
        var result = new List<string>();
        if (value is null)
        {
            return result;
        }

        if (value is string s)
        {
            result.Add(s);
            return result;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                var str = item.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    result.Add(str);
                }
            }
        }

        return result;
    }

    private readonly record struct PageResponse(int ResponseSkip, IReadOnlyList<string> SessionIds);

    private sealed class ServiceBusCbsTokenProvider : ICbsTokenProvider
    {
        private readonly ServiceBusConnectionStringProperties _connectionString;

        public ServiceBusCbsTokenProvider(ServiceBusConnectionStringProperties connectionString)
        {
            _connectionString = connectionString;
        }

        public Task<CbsToken> GetTokenAsync(Uri namespaceAddress, string appliesTo, string[] requiredClaims)
        {
            // `appliesTo` is ignored intentionally for SAS key auth: Azure SDK issues a namespace-level CBS token.
            _ = requiredClaims;
            _ = appliesTo;

            if (!string.IsNullOrWhiteSpace(_connectionString.SharedAccessSignature))
            {
                return Task.FromResult(CreateFromSharedAccessSignature(_connectionString.SharedAccessSignature));
            }

            if (string.IsNullOrWhiteSpace(_connectionString.SharedAccessKeyName) || string.IsNullOrWhiteSpace(_connectionString.SharedAccessKey))
            {
                throw new InvalidOperationException("Connection string must contain either SharedAccessSignature or SharedAccessKeyName/SharedAccessKey.");
            }

            var expiresAtUtc = DateTime.UtcNow.AddHours(1);
            var token = BuildSharedAccessSignature(
                namespaceAddress.AbsoluteUri,
                _connectionString.SharedAccessKeyName!,
                _connectionString.SharedAccessKey!,
                expiresAtUtc);

            return Task.FromResult(new CbsToken(token, CbsConstants.ServiceBusSasTokenType, expiresAtUtc));
        }

        private static CbsToken CreateFromSharedAccessSignature(string sharedAccessSignature)
        {
            if (!TryExtractExpiry(sharedAccessSignature, out var expiresAtUtc))
            {
                throw new InvalidOperationException("Unable to parse 'se' (expiry) from SharedAccessSignature in the connection string.");
            }

            return new CbsToken(sharedAccessSignature, CbsConstants.ServiceBusSasTokenType, expiresAtUtc);
        }

        private static string BuildSharedAccessSignature(string resourceUri, string keyName, string key, DateTime expiresAtUtc)
        {
            // Mirrors Azure.Messaging.ServiceBus SharedAccessSignature implementation:
            // HMACSHA256 over "<url-encoded-resource>\\n<unix-expiry>" using the raw UTF-8 SAS key bytes.
            var expiry = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var encodedResource = Uri.EscapeDataString(resourceUri);
            var stringToSign = $"{encodedResource}\n{expiry}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            var signature = Uri.EscapeDataString(Convert.ToBase64String(signatureBytes));

            return $"SharedAccessSignature sr={encodedResource}&sig={signature}&se={expiry}&skn={Uri.EscapeDataString(keyName)}";
        }

        private static bool TryExtractExpiry(string sas, out DateTime expiresAtUtc)
        {
            expiresAtUtc = default;
            const string prefix = "SharedAccessSignature ";

            var payload = sas.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? sas[prefix.Length..]
                : sas;

            foreach (var part in payload.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length != 2 || !kvp[0].Equals("se", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var raw = Uri.UnescapeDataString(kvp[1]);
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
                {
                    return false;
                }

                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                return true;
            }

            return false;
        }
    }
}
