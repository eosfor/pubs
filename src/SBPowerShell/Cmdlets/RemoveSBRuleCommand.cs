using System;
using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBRule", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBRuleCommand : SBEntityTargetCmdletBase
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "RuleName")]
    public string Rule { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var resolvedTarget = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
        var target = $"{resolvedTarget.Topic}/{resolvedTarget.Subscription}/{Rule}";

        if (!Force && !ShouldContinue($"Remove rule '{target}'?", "Confirm rule deletion"))
        {
            return;
        }

        if (!ShouldProcess($"Rule '{target}' (from {resolvedTarget.Source})", "Delete Service Bus rule"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            admin.DeleteRuleAsync(resolvedTarget.Topic, resolvedTarget.Subscription, Rule).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBRuleFailed", ErrorCategory.NotSpecified, target));
        }
    }
}
