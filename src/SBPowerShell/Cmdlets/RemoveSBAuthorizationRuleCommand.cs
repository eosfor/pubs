using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBAuthorizationRule", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBAuthorizationRuleCommand : SBEntityTargetCmdletBase
{
    [Parameter(ParameterSetName = "Queue")]
    [ValidateNotNullOrEmpty]
    public string? Queue { get; set; }

    [Parameter(ParameterSetName = "Topic")]
    [ValidateNotNullOrEmpty]
    public string? Topic { get; set; }

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "RuleName")]
    public string Rule { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var connectionString = ResolveConnectionString();
            var target = ResolveQueueOrTopicTarget(Queue, Topic, resolvedConnectionString: connectionString);
            var admin = CreateAdminClient(connectionString);
            var entity = AuthorizationRuleHelper.LoadEntity(
                admin,
                target.Kind == ResolvedEntityKind.Queue ? target.Queue : null,
                target.Kind == ResolvedEntityKind.Topic ? target.Topic : null);

            if (!Force && !ShouldContinue($"Remove authorization rule '{Rule}' from '{entity.EntityPath}'?", "Confirm rule deletion"))
            {
                return;
            }

            if (!ShouldProcess(entity.EntityPath, $"Remove authorization rule '{Rule}'"))
            {
                return;
            }

            var found = entity.Rules.FirstOrDefault(r => string.Equals(r.KeyName, Rule, StringComparison.OrdinalIgnoreCase));
            if (found is null)
            {
                throw new InvalidOperationException($"Authorization rule '{Rule}' not found on '{entity.EntityPath}'.");
            }

            entity.Rules.Remove(found);
            entity.Update(admin);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBAuthorizationRuleFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
