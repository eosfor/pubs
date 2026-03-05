using System.Collections.Concurrent;
using System.Management.Automation;
using System.Net.Sockets;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

public abstract class SBContextAwareCmdletBase : PSCmdlet
{
    private readonly IContextStore _contextStore = new RunspaceContextStore();
    private static readonly string[] ResolverErrorIds =
    {
        "SBContextNotFound",
        "MissingConnectionString",
        "MissingEntity",
        "AmbiguousEntity",
        "InvalidContext",
        "SessionContextEntityMismatch",
        "StrictContextConflict"
    };

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter]
    public SBContext? Context { get; set; }

    [Parameter]
    public SwitchParameter NoContext { get; set; }

    [Parameter]
    public SwitchParameter IgnoreCertificateChainErrors { get; set; }

    [Parameter]
    public SBTransport? Transport { get; set; }

    protected void SetCurrentContext(SBContext context)
    {
        _contextStore.Set(SessionState, context);
    }

    protected bool ClearCurrentContext()
    {
        return _contextStore.Clear(SessionState);
    }

    protected SBContext? GetCurrentContext()
    {
        return _contextStore.Get(SessionState);
    }

    protected string ResolveConnectionString(SessionContext? sessionContext = null)
    {
        var explicitConnection = SBContextValidation.Normalize(ServiceBusConnectionString);
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            WriteVerbose("Resolved ServiceBusConnectionString from Explicit parameter.");
            WarnWhenContextOverridden("ServiceBusConnectionString", explicitConnection);
            return explicitConnection;
        }

        if (!string.IsNullOrWhiteSpace(sessionContext?.ConnectionString))
        {
            WriteVerbose("Resolved ServiceBusConnectionString from SessionContext.");
            return sessionContext.ConnectionString;
        }

        var explicitContext = Context;
        if (explicitContext is not null)
        {
            EnsureValidContext(explicitContext);
            if (!string.IsNullOrWhiteSpace(explicitContext.ServiceBusConnectionString))
            {
                WriteVerbose("Resolved ServiceBusConnectionString from -Context.");
                return explicitContext.ServiceBusConnectionString!;
            }
        }

        if (!NoContext)
        {
            var current = GetCurrentContext();
            if (current is not null)
            {
                EnsureValidContext(current);
                if (!string.IsNullOrWhiteSpace(current.ServiceBusConnectionString))
                {
                    WriteVerbose("Resolved ServiceBusConnectionString from SB context.");
                    return current.ServiceBusConnectionString!;
                }
            }
        }

        ThrowResolverError(
            "MissingConnectionString",
            "ServiceBusConnectionString is required. Provide it explicitly or via Set-SBContext.",
            ErrorCategory.InvalidArgument,
            this);

        return string.Empty;
    }

    protected ServiceBusClient CreateServiceBusClient(string connectionString)
    {
        var handle = CreateServiceBusClientWithTransport(connectionString);
        return handle.Client;
    }

    protected ServiceBusClient CreateServiceBusClient(string connectionString, ServiceBusClientOptions options)
    {
        var handle = CreateServiceBusClientWithTransport(connectionString, options);
        return handle.Client;
    }

    protected (ServiceBusClient Client, ServiceBusTransportType TransportType) CreateServiceBusClientWithTransport(string connectionString)
    {
        var tlsOptions = ResolveTlsValidationOptions();
        var requestedTransport = ResolveRequestedTransport();
        return DataClientProvider.CreateWithResolvedTransport(
            connectionString,
            requestedTransport,
            tlsOptions.IgnoreCertificateChainErrors,
            tlsOptions.WarningWriter);
    }

    protected (ServiceBusClient Client, ServiceBusTransportType TransportType) CreateServiceBusClientWithTransport(
        string connectionString,
        ServiceBusClientOptions options)
    {
        var tlsOptions = ResolveTlsValidationOptions();
        var requestedTransport = ResolveRequestedTransport();
        return DataClientProvider.CreateWithResolvedTransport(
            connectionString,
            options,
            requestedTransport,
            tlsOptions.IgnoreCertificateChainErrors,
            tlsOptions.WarningWriter);
    }

    protected ServiceBusAdministrationClient CreateAdminClient(string connectionString)
    {
        var tlsOptions = ResolveTlsValidationOptions();
        var requestedTransport = ResolveRequestedTransport();
        if (requestedTransport is not null)
        {
            WriteVerbose("Transport setting applies to ServiceBus data-plane clients. ServiceBusAdministrationClient always uses HTTPS.");
        }

        return AdminClientProvider.Create(connectionString, tlsOptions.IgnoreCertificateChainErrors, tlsOptions.WarningWriter);
    }

    protected void EnsureValidContext(SBContext context)
    {
        if (!SBContextValidation.TryValidate(context, out var error))
        {
            ThrowResolverError(
                "InvalidContext",
                $"SB context is invalid: {error}",
                ErrorCategory.InvalidData,
                context);
        }
    }

    protected void ThrowResolverError(string errorId, string message, ErrorCategory category, object? target, Exception? innerException = null)
    {
        var exception = innerException ?? new InvalidOperationException(message);
        var error = new ErrorRecord(exception, errorId, category, target)
        {
            ErrorDetails = new ErrorDetails(message)
        };

        ThrowTerminatingError(error);
    }

    protected static bool IsResolverException(Exception ex)
    {
        if (ex is RuntimeException runtime &&
            IsResolverErrorId(runtime.ErrorRecord?.FullyQualifiedErrorId))
        {
            return true;
        }

        return ex.InnerException is not null && IsResolverException(ex.InnerException);
    }

    private static bool IsResolverErrorId(string? fullyQualifiedErrorId)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedErrorId))
        {
            return false;
        }

        foreach (var resolverErrorId in ResolverErrorIds)
        {
            if (fullyQualifiedErrorId.StartsWith(resolverErrorId, StringComparison.OrdinalIgnoreCase) ||
                fullyQualifiedErrorId.Contains(resolverErrorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void WarnWhenContextOverridden(string name, string explicitValue)
    {
        if (Context is null)
        {
            return;
        }

        var contextValue = SBContextValidation.Normalize(Context.ServiceBusConnectionString);
        if (!string.IsNullOrWhiteSpace(contextValue) &&
            !string.Equals(contextValue, explicitValue, StringComparison.Ordinal))
        {
            WriteWarning($"Explicit parameter '{name}' overrides value from SB context.");
        }
    }

    private (bool IgnoreCertificateChainErrors, Action<string> WarningWriter) ResolveTlsValidationOptions()
    {
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IgnoreCertificateChainErrors)))
        {
            var explicitValue = IgnoreCertificateChainErrors.IsPresent;
            WriteVerbose($"Resolved IgnoreCertificateChainErrors from Explicit parameter: {explicitValue}.");
            WarnWhenTlsPolicyOverridden(explicitValue);
            return (explicitValue, WriteWarning);
        }

        if (Context is not null)
        {
            EnsureValidContext(Context);
            WriteVerbose($"Resolved IgnoreCertificateChainErrors from -Context: {Context.IgnoreCertificateChainErrors}.");
            return (Context.IgnoreCertificateChainErrors, WriteWarning);
        }

        if (!NoContext)
        {
            var current = GetCurrentContext();
            if (current is not null)
            {
                EnsureValidContext(current);
                WriteVerbose($"Resolved IgnoreCertificateChainErrors from SB context: {current.IgnoreCertificateChainErrors}.");
                return (current.IgnoreCertificateChainErrors, WriteWarning);
            }
        }

        return (false, WriteWarning);
    }

    private void WarnWhenTlsPolicyOverridden(bool explicitValue)
    {
        if (Context is null)
        {
            return;
        }

        if (Context.IgnoreCertificateChainErrors != explicitValue)
        {
            WriteWarning("Explicit parameter 'IgnoreCertificateChainErrors' overrides value from SB context.");
        }
    }

    private ServiceBusTransportType? ResolveRequestedTransport()
    {
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Transport)) && Transport is not null)
        {
            var explicitTransport = ToServiceBusTransportType(Transport.Value);
            WriteVerbose($"Resolved Transport from Explicit parameter: {explicitTransport}.");
            WarnWhenTransportOverridden(Transport.Value);
            return explicitTransport;
        }

        if (Context is not null)
        {
            EnsureValidContext(Context);
            if (Context.Transport is not null)
            {
                var contextTransport = ToServiceBusTransportType(Context.Transport.Value);
                WriteVerbose($"Resolved Transport from -Context: {contextTransport}.");
                return contextTransport;
            }
        }

        if (!NoContext)
        {
            var current = GetCurrentContext();
            if (current is not null)
            {
                EnsureValidContext(current);
                if (current.Transport is not null)
                {
                    var contextTransport = ToServiceBusTransportType(current.Transport.Value);
                    WriteVerbose($"Resolved Transport from SB context: {contextTransport}.");
                    return contextTransport;
                }
            }
        }

        WriteVerbose("Resolved Transport from default policy: Auto (AmqpTcp with fallback to AmqpWebSockets).");
        return null;
    }

    private void WarnWhenTransportOverridden(SBTransport explicitTransport)
    {
        if (Context?.Transport is null)
        {
            return;
        }

        if (Context.Transport.Value != explicitTransport)
        {
            WriteWarning("Explicit parameter 'Transport' overrides value from SB context.");
        }
    }

    private static ServiceBusTransportType ToServiceBusTransportType(SBTransport transport)
    {
        return transport switch
        {
            SBTransport.AmqpWebSockets => ServiceBusTransportType.AmqpWebSockets,
            _ => ServiceBusTransportType.AmqpTcp
        };
    }
}

internal static class AdminClientProvider
{
    public static ServiceBusAdministrationClient Create(
        string connectionString,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        return ServiceBusAdminClientFactory.Create(connectionString, ignoreCertificateChainErrors, warningWriter);
    }
}

internal static class DataClientProvider
{
    private const int DefaultAmqpTcpPort = 5671;
    private const int TcpProbeTimeoutMs = 1500;
    private static readonly ConcurrentDictionary<string, ServiceBusTransportType> EndpointTransportCache = new(StringComparer.OrdinalIgnoreCase);

    public static ServiceBusClient Create(
        string connectionString,
        ServiceBusTransportType? requestedTransport,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        var options = new ServiceBusClientOptions();
        var handle = CreateWithResolvedTransport(
            connectionString,
            options,
            requestedTransport,
            ignoreCertificateChainErrors,
            warningWriter);
        return handle.Client;
    }

    public static ServiceBusClient Create(
        string connectionString,
        ServiceBusClientOptions options,
        ServiceBusTransportType? requestedTransport,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        var handle = CreateWithResolvedTransport(
            connectionString,
            options,
            requestedTransport,
            ignoreCertificateChainErrors,
            warningWriter);
        return handle.Client;
    }

    public static (ServiceBusClient Client, ServiceBusTransportType TransportType) CreateWithResolvedTransport(
        string connectionString,
        ServiceBusTransportType? requestedTransport,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        var options = new ServiceBusClientOptions();
        return CreateWithResolvedTransport(
            connectionString,
            options,
            requestedTransport,
            ignoreCertificateChainErrors,
            warningWriter);
    }

    public static (ServiceBusClient Client, ServiceBusTransportType TransportType) CreateWithResolvedTransport(
        string connectionString,
        ServiceBusClientOptions options,
        ServiceBusTransportType? requestedTransport,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("ServiceBusConnectionString is required.", nameof(connectionString));
        }

        var resolvedTransport = requestedTransport ?? ResolveAutoTransport(connectionString, warningWriter);
        options.TransportType = resolvedTransport;
        TlsCertificateValidation.Apply(options, ignoreCertificateChainErrors, warningWriter);
        return (new ServiceBusClient(connectionString, options), resolvedTransport);
    }

    private static ServiceBusTransportType ResolveAutoTransport(string connectionString, Action<string>? warningWriter)
    {
        if (!TryGetEndpoint(connectionString, out var endpoint))
        {
            return ServiceBusTransportType.AmqpTcp;
        }

        if (IsDevelopmentEmulator(connectionString))
        {
            return ServiceBusTransportType.AmqpTcp;
        }

        var cacheKey = $"{endpoint.Host}:{ResolveAmqpTcpPort(endpoint)}";
        if (EndpointTransportCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (TryProbeAmqpTcp(endpoint, out var failureReason))
        {
            EndpointTransportCache[cacheKey] = ServiceBusTransportType.AmqpTcp;
            return ServiceBusTransportType.AmqpTcp;
        }

        warningWriter?.Invoke(
            $"AMQP TCP transport probe failed for '{cacheKey}'. Falling back to AmqpWebSockets. Reason: {failureReason}");
        EndpointTransportCache[cacheKey] = ServiceBusTransportType.AmqpWebSockets;
        return ServiceBusTransportType.AmqpWebSockets;
    }

    private static bool IsDevelopmentEmulator(string connectionString)
    {
        return connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetEndpoint(string connectionString, out Uri endpoint)
    {
        endpoint = default!;

        try
        {
            var props = ServiceBusConnectionStringProperties.Parse(connectionString);
            if (props.Endpoint is null)
            {
                return false;
            }

            endpoint = props.Endpoint;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryProbeAmqpTcp(Uri endpoint, out string failureReason)
    {
        var host = endpoint.Host;
        var port = ResolveAmqpTcpPort(endpoint);

        if (string.IsNullOrWhiteSpace(host))
        {
            failureReason = "Endpoint host is empty.";
            return false;
        }

        try
        {
            using var probe = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(TcpProbeTimeoutMs));
            probe.ConnectAsync(host, port, cts.Token).AsTask().GetAwaiter().GetResult();
            failureReason = string.Empty;
            return true;
        }
        catch (OperationCanceledException)
        {
            failureReason = $"TCP probe timed out after {TcpProbeTimeoutMs} ms.";
            return false;
        }
        catch (SocketException ex)
        {
            failureReason = $"{ex.SocketErrorCode}: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            failureReason = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static int ResolveAmqpTcpPort(Uri endpoint)
    {
        if (!endpoint.IsDefaultPort && endpoint.Port > 0)
        {
            return endpoint.Port;
        }

        return DefaultAmqpTcpPort;
    }
}
