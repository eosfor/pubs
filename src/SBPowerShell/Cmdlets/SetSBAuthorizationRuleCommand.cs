using System.Collections.Generic;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBAuthorizationRule", SupportsShouldProcess = true)]
[OutputType(typeof(SharedAccessAuthorizationRule))]
public sealed class SetSBAuthorizationRuleCommand : PSCmdlet
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
    public AccessRights[]? Rights { get; set; }

    [Parameter]
    public string? PrimaryKey { get; set; }

    [Parameter]
    public string? SecondaryKey { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var entity = AuthorizationRuleHelper.LoadEntity(admin, Queue, Topic);

            if (!ShouldProcess(entity.EntityPath, $"Update authorization rule '{Rule}'"))
            {
                return;
            }

            var rule = AuthorizationRuleHelper.GetSharedAccessRule(entity, Rule);

            if (Rights is { Length: > 0 })
            {
                rule.Rights = new List<AccessRights>(Rights);
            }

            if (MyInvocation.BoundParameters.ContainsKey(nameof(PrimaryKey)))
            {
                rule.PrimaryKey = PrimaryKey;
            }

            if (MyInvocation.BoundParameters.ContainsKey(nameof(SecondaryKey)))
            {
                rule.SecondaryKey = SecondaryKey;
            }

            entity.Update(admin);

            WriteObject(rule);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBAuthorizationRuleFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
