using System;
using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBTransferDlqCmdletTests : SBCommandTestBase
{
    public SBTransferDlqCmdletTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Receive_transfer_dlq_cmdlet_returns_quickly_when_empty_for_queue_and_subscription()
    {
        var admin = CreateAdminClient();
        var queue = UniqueName("mgmt-tdlq-q");
        var topic = UniqueName("mgmt-tdlq-topic");
        var subscription = UniqueName("mgmt-tdlq-sub");

        try
        {
            admin.CreateQueueAsync(queue).GetAwaiter().GetResult();
            admin.CreateTopicAsync(topic).GetAwaiter().GetResult();
            admin.CreateSubscriptionAsync(topic, subscription).GetAwaiter().GetResult();

            var swQueue = Stopwatch.StartNew();
            var queueResult = Invoke<ServiceBusReceivedMessage>(ps =>
            {
                ps.AddCommand("Receive-SBTransferDLQMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("WaitSeconds", 1);
            });
            swQueue.Stop();

            Assert.Empty(queueResult);
            Assert.InRange(swQueue.Elapsed.TotalSeconds, 0, 4);

            var swSub = Stopwatch.StartNew();
            var subResult = Invoke<ServiceBusReceivedMessage>(ps =>
            {
                ps.AddCommand("Receive-SBTransferDLQMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Subscription", subscription)
                    .AddParameter("WaitSeconds", 1);
            });
            swSub.Stop();

            Assert.Empty(subResult);
            Assert.InRange(swSub.Elapsed.TotalSeconds, 0, 8);
        }
        finally
        {
            SafeDeleteSubscription(admin, topic, subscription);
            SafeDeleteTopic(admin, topic);
            SafeDeleteQueue(admin, queue);
        }
    }
}
