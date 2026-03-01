using System;
using System.Management.Automation;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBQueue", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBQueueCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "QueueName")]
    public string Queue { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        if (!Force && !ShouldContinue($"Remove queue '{Queue}'?", "Confirm queue deletion"))
        {
            return;
        }

        if (!ShouldProcess(Queue, "Delete Service Bus queue"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            admin.DeleteQueueAsync(Queue).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBQueueFailed", ErrorCategory.NotSpecified, Queue));
        }
    }
}
