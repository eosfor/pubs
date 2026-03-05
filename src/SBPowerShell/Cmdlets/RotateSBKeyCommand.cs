using System.Management.Automation;

namespace SBPowerShell.Cmdlets;

[Cmdlet("Rotate", "SBKey", SupportsShouldProcess = true)]
[OutputType(typeof(object))]
public sealed class RotateSBKeyCommand : SBEntityTargetCmdletBase
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
    [ValidateSet("Primary", "Secondary", "Both")]
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

            if (!ShouldProcess(entity.EntityPath, $"Rotate {KeyType} key(s) for rule '{Rule}'"))
            {
                return;
            }

            if (KeyType is "Primary" or "Both")
            {
                sasRule.PrimaryKey = AuthorizationRuleHelper.GenerateSharedAccessKey();
            }

            if (KeyType is "Secondary" or "Both")
            {
                sasRule.SecondaryKey = AuthorizationRuleHelper.GenerateSharedAccessKey();
            }

            entity.Update(admin);

            WriteObject(new
            {
                Entity = entity.EntityPath,
                Rule = sasRule.KeyName,
                Rotated = KeyType,
                sasRule.PrimaryKey,
                sasRule.SecondaryKey
            });
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RotateSBKeyFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
