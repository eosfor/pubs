using System;
using System.Management.Automation;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBTopic", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBTopicCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        if (!Force && !ShouldContinue($"Remove topic '{Topic}'?", "Confirm topic deletion"))
        {
            return;
        }

        if (!ShouldProcess(Topic, "Delete Service Bus topic"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            admin.DeleteTopicAsync(Topic).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBTopicFailed", ErrorCategory.NotSpecified, Topic));
        }
    }
}
