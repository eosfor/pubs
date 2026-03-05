using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBRule", DefaultParameterSetName = ParameterSetSql, SupportsShouldProcess = true)]
[OutputType(typeof(RuleProperties))]
public sealed class SetSBRuleCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetSql = "Sql";
    private const string ParameterSetCorrelation = "Correlation";

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "RuleName")]
    public string Rule { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSql)]
    public string? SqlFilter { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? CorrelationId { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? MessageId { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? To { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? ReplyTo { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? Subject { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? SessionId { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? ReplyToSessionId { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public string? ContentType { get; set; }

    [Parameter(ParameterSetName = ParameterSetCorrelation)]
    public Hashtable? CorrelationProperty { get; set; }

    [Parameter]
    public string? SqlAction { get; set; }

    [Parameter]
    public SwitchParameter ClearAction { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var resolvedTarget = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
        var target = $"{resolvedTarget.Topic}/{resolvedTarget.Subscription}/{Rule}";
        if (!ShouldProcess($"Rule '{target}' (from {resolvedTarget.Source})", "Update Service Bus rule"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var rule = admin.GetRuleAsync(resolvedTarget.Topic, resolvedTarget.Subscription, Rule).GetAwaiter().GetResult().Value;

            Apply(rule);

            var updated = admin.UpdateRuleAsync(resolvedTarget.Topic, resolvedTarget.Subscription, rule).GetAwaiter().GetResult().Value;
            WriteObject(updated);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SetSBRuleFailed", ErrorCategory.NotSpecified, target));
        }
    }

    private void Apply(RuleProperties rule)
    {
        if (ParameterSetName == ParameterSetCorrelation)
        {
            rule.Filter = BuildCorrelationFilter();
        }
        else if (MyInvocation.BoundParameters.ContainsKey(nameof(SqlFilter)))
        {
            rule.Filter = string.IsNullOrWhiteSpace(SqlFilter)
                ? new TrueRuleFilter()
                : new SqlRuleFilter(SqlFilter);
        }

        if (ClearAction)
        {
            rule.Action = null;
        }
        else if (MyInvocation.BoundParameters.ContainsKey(nameof(SqlAction)))
        {
            rule.Action = string.IsNullOrWhiteSpace(SqlAction) ? null : new SqlRuleAction(SqlAction);
        }
    }

    private CorrelationRuleFilter BuildCorrelationFilter()
    {
        var correlation = new CorrelationRuleFilter();

        if (!string.IsNullOrWhiteSpace(CorrelationId)) correlation.CorrelationId = CorrelationId;
        if (!string.IsNullOrWhiteSpace(MessageId)) correlation.MessageId = MessageId;
        if (!string.IsNullOrWhiteSpace(To)) correlation.To = To;
        if (!string.IsNullOrWhiteSpace(ReplyTo)) correlation.ReplyTo = ReplyTo;
        if (!string.IsNullOrWhiteSpace(Subject)) correlation.Subject = Subject;
        if (!string.IsNullOrWhiteSpace(SessionId)) correlation.SessionId = SessionId;
        if (!string.IsNullOrWhiteSpace(ReplyToSessionId)) correlation.ReplyToSessionId = ReplyToSessionId;
        if (!string.IsNullOrWhiteSpace(ContentType)) correlation.ContentType = ContentType;

        if (CorrelationProperty is not null)
        {
            foreach (DictionaryEntry item in CorrelationProperty)
            {
                var key = item.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("CorrelationProperty contains an empty key.");
                }

                correlation.ApplicationProperties[key] = item.Value!;
            }
        }

        if (!HasCorrelationCriteria(correlation))
        {
            throw new ArgumentException("For correlation filter, specify at least one criterion.");
        }

        return correlation;
    }

    private static bool HasCorrelationCriteria(CorrelationRuleFilter filter)
    {
        return !string.IsNullOrWhiteSpace(filter.CorrelationId)
               || !string.IsNullOrWhiteSpace(filter.MessageId)
               || !string.IsNullOrWhiteSpace(filter.To)
               || !string.IsNullOrWhiteSpace(filter.ReplyTo)
               || !string.IsNullOrWhiteSpace(filter.Subject)
               || !string.IsNullOrWhiteSpace(filter.SessionId)
               || !string.IsNullOrWhiteSpace(filter.ReplyToSessionId)
               || !string.IsNullOrWhiteSpace(filter.ContentType)
               || filter.ApplicationProperties.Count > 0;
    }
}
