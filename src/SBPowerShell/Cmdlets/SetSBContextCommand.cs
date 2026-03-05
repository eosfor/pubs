using System.Management.Automation;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBContext")]
[OutputType(typeof(SBContext))]
public sealed class SetSBContextCommand : PSCmdlet
{
    private const string ParameterSetNamespace = "Namespace";
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetFromObject = "FromObject";

    private readonly IContextStore _contextStore = new RunspaceContextStore();

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNamespace)]
    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFromObject, ValueFromPipeline = true)]
    [ValidateNotNull]
    public SBContext InputObject { get; set; } = null!;

    [Parameter]
    public SwitchParameter Strict { get; set; }

    [Parameter]
    public SwitchParameter NoClobber { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    public SwitchParameter IgnoreCertificateChainErrors { get; set; }

    protected override void EndProcessing()
    {
        var existing = GetCurrentContext();

        if (NoClobber && existing is not null)
        {
            ThrowResolverError(
                "SBContextAlreadyExists",
                "SB context already exists. Use Clear-SBContext first or omit -NoClobber.",
                ErrorCategory.ResourceExists,
                existing);
        }

        var context = BuildContext(existing);
        EnsureValidContext(context);

        ValidateEntityPathConflict(context);
        SetCurrentContext(context);

        if (PassThru)
        {
            WriteObject(context);
        }
    }

    private SBContext BuildContext(SBContext? existing)
    {
        if (ParameterSetName == ParameterSetFromObject)
        {
            return NormalizeContext(InputObject, existing);
        }

        var serviceBusConnectionString = ResolveConnection(existing);
        var queue = ParameterSetName == ParameterSetQueue
            ? SBContextValidation.Normalize(Queue)
            : null;
        var topic = ParameterSetName == ParameterSetSubscription
            ? SBContextValidation.Normalize(Topic)
            : null;
        var subscription = ParameterSetName == ParameterSetSubscription
            ? SBContextValidation.Normalize(Subscription)
            : null;

        var mode = ResolveMode(queue, topic, subscription);
        var createdAt = existing?.CreatedAtUtc ?? DateTime.UtcNow;
        var ignoreChainErrors = ResolveIgnoreCertificateChainErrors(existing?.IgnoreCertificateChainErrors ?? false);

        return new SBContext
        {
            ServiceBusConnectionString = serviceBusConnectionString,
            Queue = queue,
            Topic = topic,
            Subscription = subscription,
            IgnoreCertificateChainErrors = ignoreChainErrors,
            EntityMode = mode,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = DateTime.UtcNow,
            Source = "User"
        };
    }

    private SBContext NormalizeContext(SBContext context, SBContext? existing)
    {
        var queue = SBContextValidation.Normalize(context.Queue);
        var topic = SBContextValidation.Normalize(context.Topic);
        var subscription = SBContextValidation.Normalize(context.Subscription);
        var connectionString = SBContextValidation.Normalize(context.ServiceBusConnectionString)
            ?? SBContextValidation.Normalize(existing?.ServiceBusConnectionString)
            ?? string.Empty;
        var ignoreChainErrors = ResolveIgnoreCertificateChainErrors(context.IgnoreCertificateChainErrors);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ThrowResolverError(
                "MissingConnectionString",
                "ServiceBusConnectionString is required. Provide it explicitly or via Set-SBContext.",
                ErrorCategory.InvalidArgument,
                this);
        }

        return new SBContext
        {
            ServiceBusConnectionString = connectionString,
            Queue = queue,
            Topic = topic,
            Subscription = subscription,
            IgnoreCertificateChainErrors = ignoreChainErrors,
            EntityMode = ResolveMode(queue, topic, subscription),
            CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Source = string.IsNullOrWhiteSpace(context.Source) ? "User" : context.Source
        };
    }

    private string ResolveConnection(SBContext? existing)
    {
        var explicitConnection = SBContextValidation.Normalize(ServiceBusConnectionString);
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            return explicitConnection;
        }

        var existingConnection = SBContextValidation.Normalize(existing?.ServiceBusConnectionString);
        if (!string.IsNullOrWhiteSpace(existingConnection))
        {
            return existingConnection!;
        }

        ThrowResolverError(
            "MissingConnectionString",
            "ServiceBusConnectionString is required. Provide it explicitly or via Set-SBContext.",
            ErrorCategory.InvalidArgument,
            this);

        return string.Empty;
    }

    private void ValidateEntityPathConflict(SBContext context)
    {
        var entityPathFromConnectionString = SBContextValidation.TryGetEntityPathFromConnectionString(context.ServiceBusConnectionString);
        if (string.IsNullOrWhiteSpace(entityPathFromConnectionString))
        {
            return;
        }

        var targetEntityPath = context.EntityMode switch
        {
            SBContextEntityMode.Queue => context.Queue,
            SBContextEntityMode.Subscription => $"{context.Topic}/Subscriptions/{context.Subscription}",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(targetEntityPath))
        {
            return;
        }

        if (string.Equals(entityPathFromConnectionString, targetEntityPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Strict)
        {
            ThrowResolverError(
                "StrictContextConflict",
                "Connection string EntityPath conflicts with resolved target in -Strict mode.",
                ErrorCategory.InvalidArgument,
                targetEntityPath);
        }

        WriteWarning($"Connection string EntityPath ('{entityPathFromConnectionString}') differs from resolved target ('{targetEntityPath}'). Explicit target is used.");
    }

    private static SBContextEntityMode ResolveMode(string? queue, string? topic, string? subscription)
    {
        if (!string.IsNullOrWhiteSpace(queue))
        {
            return SBContextEntityMode.Queue;
        }

        if (!string.IsNullOrWhiteSpace(topic) || !string.IsNullOrWhiteSpace(subscription))
        {
            return SBContextEntityMode.Subscription;
        }

        return SBContextEntityMode.Namespace;
    }

    private bool ResolveIgnoreCertificateChainErrors(bool fallbackValue)
    {
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IgnoreCertificateChainErrors)))
        {
            return IgnoreCertificateChainErrors.IsPresent;
        }

        return fallbackValue;
    }

    private SBContext? GetCurrentContext()
    {
        return _contextStore.Get(SessionState);
    }

    private void SetCurrentContext(SBContext context)
    {
        _contextStore.Set(SessionState, context);
    }

    private void EnsureValidContext(SBContext context)
    {
        if (SBContextValidation.TryValidate(context, out var error))
        {
            return;
        }

        ThrowResolverError(
            "InvalidContext",
            $"SB context is invalid: {error}",
            ErrorCategory.InvalidData,
            context);
    }

    private void ThrowResolverError(string errorId, string message, ErrorCategory category, object? target)
    {
        var exception = new InvalidOperationException(message);
        var error = new ErrorRecord(exception, errorId, category, target)
        {
            ErrorDetails = new ErrorDetails(message)
        };
        ThrowTerminatingError(error);
    }
}
