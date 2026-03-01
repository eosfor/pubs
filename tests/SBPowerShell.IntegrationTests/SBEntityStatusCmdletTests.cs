using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBEntityStatusCmdletTests : SBCommandTestBase
{
    public SBEntityStatusCmdletTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Entity_status_cmdlet_updates_queue_topic_and_subscription()
    {
        var admin = CreateAdminClient();
        var queue = UniqueName("mgmt-status-q");
        var topic = UniqueName("mgmt-status-topic");
        var subscription = UniqueName("mgmt-status-sub");

        try
        {
            admin.CreateQueueAsync(queue).GetAwaiter().GetResult();
            admin.CreateTopicAsync(topic).GetAwaiter().GetResult();
            admin.CreateSubscriptionAsync(topic, subscription).GetAwaiter().GetResult();

            Invoke(ps =>
            {
                ps.AddCommand("Set-SBEntityStatus")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Status", "Disabled");
            });
            Assert.Equal(EntityStatus.Disabled, admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value.Status);

            Invoke(ps =>
            {
                ps.AddCommand("Set-SBEntityStatus")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Status", "SendDisabled");
            });
            Assert.Equal(EntityStatus.SendDisabled, admin.GetTopicAsync(topic).GetAwaiter().GetResult().Value.Status);

            Invoke(ps =>
            {
                ps.AddCommand("Set-SBEntityStatus")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("Status", "ReceiveDisabled");
            });
            Assert.Equal(EntityStatus.ReceiveDisabled, admin.GetSubscriptionAsync(topic, subscription).GetAwaiter().GetResult().Value.Status);
        }
        finally
        {
            SafeDeleteSubscription(admin, topic, subscription);
            SafeDeleteTopic(admin, topic);
            SafeDeleteQueue(admin, queue);
        }
    }
}
