using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBRule", DefaultParameterSetName = ParameterSetAll)]
[OutputType(typeof(RuleProperties))]
public sealed class GetSBRuleCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetAll = "All";
    private const string ParameterSetByName = "ByName";

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
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
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "GetSBRuleFailed", ErrorCategory.NotSpecified, $"{Topic}/{Subscription}/{Rule}"));
        }
    }

    private async Task<IReadOnlyList<RuleProperties>> GetRulesAsync()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
        var admin = CreateAdminClient(connectionString);

        if (ParameterSetName == ParameterSetByName && !string.IsNullOrWhiteSpace(Rule))
        {
            var single = (await admin.GetRuleAsync(target.Topic, target.Subscription, Rule!)).Value;
            return new[] { single };
        }

        var list = new List<RuleProperties>();
        await foreach (var rule in admin.GetRulesAsync(target.Topic, target.Subscription))
        {
            list.Add(rule);
        }

        return list;
    }
}
