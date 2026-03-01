using System.Management.Automation;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBConnectionString")]
[OutputType(typeof(string))]
public sealed class GetSBConnectionStringCommand : PSCmdlet
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
    [ValidateSet("Primary", "Secondary")]
    public string KeyType { get; set; } = "Primary";

    protected override void ProcessRecord()
    {
        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var entity = AuthorizationRuleHelper.LoadEntity(admin, Queue, Topic);
            var sasRule = AuthorizationRuleHelper.GetSharedAccessRule(entity, Rule);

            var key = KeyType == "Primary" ? sasRule.PrimaryKey : sasRule.SecondaryKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException($"{KeyType} key is empty for rule '{Rule}'.");
            }

            var cs = AuthorizationRuleHelper.BuildConnectionString(
                ServiceBusConnectionString,
                entity.EntityPath,
                sasRule.KeyName,
                key);

            WriteObject(cs);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "GetSBConnectionStringFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
