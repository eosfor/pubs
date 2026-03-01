using System;
using System.Collections;
using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBRuleCmdletsTests : SBCommandTestBase
{
    public SBRuleCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Rule_management_cmdlets_support_sql_and_correlation_filters()
    {
        var admin = CreateAdminClient();
        var topic = UniqueName("mgmt-rule-topic");
        var subscription = UniqueName("mgmt-rule-sub");
        var rule = UniqueName("mgmt-rule");

        try
        {
            admin.CreateTopicAsync(topic).GetAwaiter().GetResult();
            admin.CreateSubscriptionAsync(topic, subscription).GetAwaiter().GetResult();

            Invoke(ps =>
            {
                ps.AddCommand("New-SBRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("Rule", rule)
                    .AddParameter("SqlFilter", "priority = 'high'")
                    .AddParameter("SqlAction", "SET route = 'ops'");
            });

            var listed = Invoke<RuleProperties>(ps =>
            {
                ps.AddCommand("Get-SBRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("Rule", rule);
            });
            Assert.Single(listed);

            var correlationProps = new Hashtable { ["tenant"] = "corp" };
            Invoke(ps =>
            {
                ps.AddCommand("Set-SBRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("Rule", rule)
                    .AddParameter("CorrelationId", "corr-1")
                    .AddParameter("SessionId", "sess-1")
                    .AddParameter("CorrelationProperty", correlationProps)
                    .AddParameter("SqlAction", "SET route = 'updated'");
            });

            var updated = admin.GetRuleAsync(topic, subscription, rule).GetAwaiter().GetResult().Value;
            var correlation = Assert.IsType<CorrelationRuleFilter>(updated.Filter);
            Assert.Equal("corr-1", correlation.CorrelationId);
            Assert.Equal("sess-1", correlation.SessionId);
            Assert.Equal("corp", correlation.ApplicationProperties["tenant"]?.ToString());
            Assert.Equal("SET route = 'updated'", Assert.IsType<SqlRuleAction>(updated.Action).SqlExpression);

            Invoke(ps =>
            {
                ps.AddCommand("Remove-SBRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("Rule", rule)
                    .AddParameter("Force", true)
                    .AddParameter("Confirm", false);
            });

            Assert.False(admin.RuleExistsAsync(topic, subscription, rule).GetAwaiter().GetResult());
        }
        finally
        {
            SafeDeleteRule(admin, topic, subscription, rule);
            SafeDeleteSubscription(admin, topic, subscription);
            SafeDeleteTopic(admin, topic);
        }
    }
}
