using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBAuthorizationRule", SupportsShouldProcess = true)]
[OutputType(typeof(SharedAccessAuthorizationRule))]
public sealed class NewSBAuthorizationRuleCommand : SBEntityTargetCmdletBase
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

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public AccessRights[] Rights { get; set; } = [];

    [Parameter]
    public string? PrimaryKey { get; set; }

    [Parameter]
    public string? SecondaryKey { get; set; }

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

            if (!ShouldProcess(entity.EntityPath, $"Create authorization rule '{Rule}'"))
            {
                return;
            }

            var exists = entity.Rules.Any(r => string.Equals(r.KeyName, Rule, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                throw new InvalidOperationException($"Authorization rule '{Rule}' already exists on '{entity.EntityPath}'.");
            }

            SharedAccessAuthorizationRule rule;
            if (!string.IsNullOrWhiteSpace(PrimaryKey) && !string.IsNullOrWhiteSpace(SecondaryKey))
            {
                rule = new SharedAccessAuthorizationRule(Rule, PrimaryKey, SecondaryKey, Rights);
            }
            else if (!string.IsNullOrWhiteSpace(PrimaryKey))
            {
                rule = new SharedAccessAuthorizationRule(Rule, PrimaryKey, Rights);
            }
            else
            {
                rule = new SharedAccessAuthorizationRule(Rule, Rights);
            }

            entity.Rules.Add(rule);
            entity.Update(admin);

            WriteObject(rule);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "NewSBAuthorizationRuleFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
