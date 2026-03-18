using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBConnectionString")]
[OutputType(typeof(string))]
public sealed class GetSBConnectionStringCommand : SBEntityTargetCmdletBase
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
    [ValidateSet("Primary", "Secondary")]
    public string KeyType { get; set; } = "Primary";

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
            var sasRule = AuthorizationRuleHelper.GetSharedAccessRule(entity, Rule);

            var key = KeyType == "Primary" ? sasRule.PrimaryKey : sasRule.SecondaryKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException($"{KeyType} key is empty for rule '{Rule}'.");
            }

            var cs = AuthorizationRuleHelper.BuildConnectionString(
                connectionString,
                entity.EntityPath,
                sasRule.KeyName,
                key);

            WriteObject(cs);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "GetSBConnectionStringFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
