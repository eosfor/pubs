using System.Management.Automation;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet("Rotate", "SBKey", SupportsShouldProcess = true)]
[OutputType(typeof(object))]
public sealed class RotateSBKeyCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(ParameterSetName = "Queue", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Queue { get; set; }

    [Parameter(ParameterSetName = "Topic", Mandatory = true)]
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
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var entity = AuthorizationRuleHelper.LoadEntity(admin, Queue, Topic);
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
            ThrowTerminatingError(new ErrorRecord(ex, "RotateSBKeyFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
