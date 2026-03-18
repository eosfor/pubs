using System.Management.Automation;
using System.Security.Cryptography;
using System.Text;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBContext", DefaultParameterSetName = ParameterSetDefault)]
[OutputType(typeof(PSObject), typeof(SBContext), typeof(string))]
public sealed class GetSBContextCommand : PSCmdlet
{
    private const string ParameterSetDefault = "Default";

    private readonly IContextStore _contextStore = new RunspaceContextStore();

    [Parameter(ParameterSetName = "Raw")]
    public SwitchParameter Raw { get; set; }

    [Parameter(ParameterSetName = "ConnectionString")]
    public SwitchParameter AsConnectionString { get; set; }

    protected override void EndProcessing()
    {
        var context = _contextStore.Get(SessionState);
        if (context is null)
        {
            WriteVerbose("No SB context is currently set.");
            WriteObject(null);
            return;
        }

        if (!SBContextValidation.TryValidate(context, out var error))
        {
            var message = $"SB context is invalid: {error}";
            var ex = new InvalidOperationException(message);
            var record = new ErrorRecord(ex, "InvalidContext", ErrorCategory.InvalidData, context)
            {
                ErrorDetails = new ErrorDetails(message)
            };
            ThrowTerminatingError(record);
        }

        if (AsConnectionString)
        {
            WriteObject(context.ServiceBusConnectionString);
            return;
        }

        if (Raw)
        {
            WriteObject(context);
            return;
        }

        WriteObject(CreateSafeView(context));
    }

    private static PSObject CreateSafeView(SBContext context)
    {
        var view = new PSObject();

        view.Properties.Add(new PSNoteProperty("NamespaceEndpoint", ResolveNamespaceEndpoint(context.ServiceBusConnectionString)));
        view.Properties.Add(new PSNoteProperty("EntityMode", context.EntityMode));
        view.Properties.Add(new PSNoteProperty("Queue", context.Queue));
        view.Properties.Add(new PSNoteProperty("Topic", context.Topic));
        view.Properties.Add(new PSNoteProperty("Subscription", context.Subscription));
        view.Properties.Add(new PSNoteProperty("Transport", context.Transport));
        view.Properties.Add(new PSNoteProperty("IgnoreCertificateChainErrors", context.IgnoreCertificateChainErrors));
        view.Properties.Add(new PSNoteProperty("HasConnectionString", !string.IsNullOrWhiteSpace(context.ServiceBusConnectionString)));
        view.Properties.Add(new PSNoteProperty("ConnectionStringFingerprint", CreateFingerprint(context.ServiceBusConnectionString)));
        view.Properties.Add(new PSNoteProperty("CreatedAtUtc", context.CreatedAtUtc));
        view.Properties.Add(new PSNoteProperty("UpdatedAtUtc", context.UpdatedAtUtc));
        view.Properties.Add(new PSNoteProperty("Source", context.Source));

        return view;
    }

    private static string? ResolveNamespaceEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var props = ServiceBusConnectionStringProperties.Parse(connectionString);
            return props.Endpoint?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? CreateFingerprint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(connectionString);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..8]);
    }
}
