using System;
using System.Management.Automation;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBSubscription", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBSubscriptionCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("SubscriptionName")]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        var target = $"{Topic}/{Subscription}";

        if (!Force && !ShouldContinue($"Remove subscription '{target}'?", "Confirm subscription deletion"))
        {
            return;
        }

        if (!ShouldProcess(target, "Delete Service Bus subscription"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            admin.DeleteSubscriptionAsync(Topic, Subscription).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBSubscriptionFailed", ErrorCategory.NotSpecified, target));
        }
    }
}
