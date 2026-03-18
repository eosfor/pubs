using System;
using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBTopic", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBTopicCommand : SBEntityTargetCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveTopicTarget(Topic, resolvedConnectionString: connectionString);

        if (!Force && !ShouldContinue($"Remove topic '{target.Topic}'?", "Confirm topic deletion"))
        {
            return;
        }

        if (!ShouldProcess($"Topic '{target.Topic}' (from {target.Source})", "Delete Service Bus topic"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            admin.DeleteTopicAsync(target.Topic).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBTopicFailed", ErrorCategory.NotSpecified, target.Topic));
        }
    }
}
