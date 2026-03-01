using System;
using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBSubscriptionCmdletsTests : SBCommandTestBase
{
    public SBSubscriptionCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Subscription_lifecycle_cmdlets_work_end_to_end()
    {
        var admin = CreateAdminClient();
        var topic = UniqueName("mgmt-sub-topic");
        var subscription = UniqueName("mgmt-sub");

        try
        {
            admin.CreateTopicAsync(topic).GetAwaiter().GetResult();

            Invoke(ps =>
            {
                ps.AddCommand("New-SBSubscription")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("MaxDeliveryCount", 3)
                    .AddParameter("SqlFilter", "region = 'EU'");
            });

            var created = admin.GetSubscriptionAsync(topic, subscription).GetAwaiter().GetResult().Value;
            Assert.Equal(3, created.MaxDeliveryCount);

            var defaultRule = admin.GetRuleAsync(topic, subscription, "$Default").GetAwaiter().GetResult().Value;
            var sql = Assert.IsType<SqlRuleFilter>(defaultRule.Filter);
            Assert.Contains("region", sql.SqlExpression, StringComparison.OrdinalIgnoreCase);

            Invoke(ps =>
            {
                ps.AddCommand("Set-SBSubscription")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("MaxDeliveryCount", 6);
            });

            var updated = admin.GetSubscriptionAsync(topic, subscription).GetAwaiter().GetResult().Value;
            Assert.Equal(6, updated.MaxDeliveryCount);

            Invoke(ps =>
            {
                ps.AddCommand("Remove-SBSubscription")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("Force", true)
                    .AddParameter("Confirm", false);
            });

            Assert.False(admin.SubscriptionExistsAsync(topic, subscription).GetAwaiter().GetResult());
        }
        finally
        {
            SafeDeleteSubscription(admin, topic, subscription);
            SafeDeleteTopic(admin, topic);
        }
    }
}
