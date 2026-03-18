using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBAuthorizationRule")]
[OutputType(typeof(AuthorizationRule))]
public sealed class GetSBAuthorizationRuleCommand : SBEntityTargetCmdletBase
{
    [Parameter(ParameterSetName = "Queue")]
    [ValidateNotNullOrEmpty]
    public string? Queue { get; set; }

    [Parameter(ParameterSetName = "Topic")]
    [ValidateNotNullOrEmpty]
    public string? Topic { get; set; }

    [Parameter]
    [Alias("Name")]
    public string? Rule { get; set; }

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

            var output = string.IsNullOrWhiteSpace(Rule)
                ? entity.Rules.ToArray()
                : entity.Rules.Where(r => string.Equals(r.KeyName, Rule, StringComparison.OrdinalIgnoreCase)).ToArray();

            foreach (var item in output)
            {
                WriteObject(item);
            }
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "GetSBAuthorizationRuleFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
