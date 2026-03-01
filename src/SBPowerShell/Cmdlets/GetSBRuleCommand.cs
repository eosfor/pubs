using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBRule", DefaultParameterSetName = ParameterSetAll)]
[OutputType(typeof(RuleProperties))]
public sealed class GetSBRuleCommand : PSCmdlet
{
    private const string ParameterSetAll = "All";
    private const string ParameterSetByName = "ByName";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetByName, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "RuleName")]
    public string? Rule { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var output = GetRulesAsync().GetAwaiter().GetResult();
            foreach (var item in output)
            {
                WriteObject(item);
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "GetSBRuleFailed", ErrorCategory.NotSpecified, $"{Topic}/{Subscription}/{Rule}"));
        }
    }

    private async Task<IReadOnlyList<RuleProperties>> GetRulesAsync()
    {
        var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);

        if (ParameterSetName == ParameterSetByName && !string.IsNullOrWhiteSpace(Rule))
        {
            var single = (await admin.GetRuleAsync(Topic, Subscription, Rule!)).Value;
            return new[] { single };
        }

        var list = new List<RuleProperties>();
        await foreach (var rule in admin.GetRulesAsync(Topic, Subscription))
        {
            list.Add(rule);
        }

        return list;
    }
}
