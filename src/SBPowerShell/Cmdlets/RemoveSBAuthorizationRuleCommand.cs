using System.Management.Automation;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBAuthorizationRule", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveSBAuthorizationRuleCommand : PSCmdlet
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
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var entity = AuthorizationRuleHelper.LoadEntity(admin, Queue, Topic);

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
            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBAuthorizationRuleFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
