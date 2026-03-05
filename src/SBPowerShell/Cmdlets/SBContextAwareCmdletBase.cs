using System.Management.Automation;
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
        var tlsOptions = ResolveTlsValidationOptions();
        return DataClientProvider.Create(connectionString, tlsOptions.IgnoreCertificateChainErrors, tlsOptions.WarningWriter);
    }

    protected ServiceBusClient CreateServiceBusClient(string connectionString, ServiceBusClientOptions options)
    {
        var tlsOptions = ResolveTlsValidationOptions();
        return DataClientProvider.Create(connectionString, options, tlsOptions.IgnoreCertificateChainErrors, tlsOptions.WarningWriter);
    }

    protected ServiceBusAdministrationClient CreateAdminClient(string connectionString)
    {
        var tlsOptions = ResolveTlsValidationOptions();
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
    public static ServiceBusClient Create(
        string connectionString,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        var options = new ServiceBusClientOptions();
        TlsCertificateValidation.Apply(options, ignoreCertificateChainErrors, warningWriter);
        return new ServiceBusClient(connectionString, options);
    }

    public static ServiceBusClient Create(
        string connectionString,
        ServiceBusClientOptions options,
        bool ignoreCertificateChainErrors,
        Action<string>? warningWriter)
    {
        TlsCertificateValidation.Apply(options, ignoreCertificateChainErrors, warningWriter);
        return new ServiceBusClient(connectionString, options);
    }
}
