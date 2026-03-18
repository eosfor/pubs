using System;
using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBQueue", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBQueueCommand : SBEntityTargetCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "QueueName")]
    public string Queue { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveQueueTarget(Queue);

        if (!Force && !ShouldContinue($"Remove queue '{target.Queue}'?", "Confirm queue deletion"))
        {
            return;
        }

        if (!ShouldProcess($"Queue '{target.Queue}' (from {target.Source})", "Delete Service Bus queue"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            admin.DeleteQueueAsync(target.Queue).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBQueueFailed", ErrorCategory.NotSpecified, target.Queue));
        }
    }
}
