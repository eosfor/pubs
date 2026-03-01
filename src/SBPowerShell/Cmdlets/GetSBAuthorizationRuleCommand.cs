using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBAuthorizationRule")]
[OutputType(typeof(AuthorizationRule))]
public sealed class GetSBAuthorizationRuleCommand : PSCmdlet
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

    [Parameter]
    [Alias("Name")]
    public string? Rule { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var entity = AuthorizationRuleHelper.LoadEntity(admin, Queue, Topic);

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
            ThrowTerminatingError(new ErrorRecord(ex, "GetSBAuthorizationRuleFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
