using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBSubscription", SupportsShouldProcess = true)]
[OutputType(typeof(SubscriptionProperties))]
public sealed class NewSBSubscriptionCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("SubscriptionName")]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    public bool? RequiresSession { get; set; }

    [Parameter]
    public bool? EnableBatchedOperations { get; set; }

    [Parameter]
    public bool? DeadLetteringOnMessageExpiration { get; set; }

    [Parameter]
    public bool? EnableDeadLetteringOnFilterEvaluationExceptions { get; set; }

    [Parameter]
    public int? MaxDeliveryCount { get; set; }

    [Parameter]
    public TimeSpan? LockDuration { get; set; }

    [Parameter]
    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    [Parameter]
    public TimeSpan? AutoDeleteOnIdle { get; set; }

    [Parameter]
    public string? ForwardTo { get; set; }

    [Parameter]
    public string? ForwardDeadLetteredMessagesTo { get; set; }

    [Parameter]
    public string? UserMetadata { get; set; }

    [Parameter]
    public string? SqlFilter { get; set; }

    protected override void ProcessRecord()
    {
        var target = $"{Topic}/{Subscription}";
        if (!ShouldProcess(target, "Create Service Bus subscription"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var options = new CreateSubscriptionOptions(Topic, Subscription);

            ApplyOptions(options);

            SubscriptionProperties created;
            if (string.IsNullOrWhiteSpace(SqlFilter))
            {
                created = admin.CreateSubscriptionAsync(options).GetAwaiter().GetResult().Value;
            }
            else
            {
                var defaultRule = new CreateRuleOptions("$Default", new SqlRuleFilter(SqlFilter));
                created = admin.CreateSubscriptionAsync(options, defaultRule).GetAwaiter().GetResult().Value;
            }

            WriteObject(created);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "NewSBSubscriptionFailed", ErrorCategory.NotSpecified, target));
        }
    }

    private void ApplyOptions(CreateSubscriptionOptions options)
    {
        if (RequiresSession.HasValue) options.RequiresSession = RequiresSession.Value;
        if (EnableBatchedOperations.HasValue) options.EnableBatchedOperations = EnableBatchedOperations.Value;
        if (DeadLetteringOnMessageExpiration.HasValue) options.DeadLetteringOnMessageExpiration = DeadLetteringOnMessageExpiration.Value;
        if (EnableDeadLetteringOnFilterEvaluationExceptions.HasValue) options.EnableDeadLetteringOnFilterEvaluationExceptions = EnableDeadLetteringOnFilterEvaluationExceptions.Value;

        if (MaxDeliveryCount.HasValue) options.MaxDeliveryCount = MaxDeliveryCount.Value;

        if (LockDuration.HasValue) options.LockDuration = LockDuration.Value;
        if (DefaultMessageTimeToLive.HasValue) options.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) options.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;

        if (!string.IsNullOrWhiteSpace(ForwardTo)) options.ForwardTo = ForwardTo;
        if (!string.IsNullOrWhiteSpace(ForwardDeadLetteredMessagesTo)) options.ForwardDeadLetteredMessagesTo = ForwardDeadLetteredMessagesTo;
        if (UserMetadata is not null) options.UserMetadata = UserMetadata;
    }
}
