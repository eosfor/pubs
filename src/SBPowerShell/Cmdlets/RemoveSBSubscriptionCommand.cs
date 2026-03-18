using System;
using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBSubscription", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBSubscriptionCommand : SBEntityTargetCmdletBase
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("SubscriptionName")]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
        var targetPath = $"{target.Topic}/{target.Subscription}";

        if (!Force && !ShouldContinue($"Remove subscription '{targetPath}'?", "Confirm subscription deletion"))
        {
            return;
        }

        if (!ShouldProcess($"Subscription '{targetPath}' (from {target.Source})", "Delete Service Bus subscription"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            admin.DeleteSubscriptionAsync(target.Topic, target.Subscription).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBSubscriptionFailed", ErrorCategory.NotSpecified, targetPath));
        }
    }
}
