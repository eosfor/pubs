using System;
using Azure.Messaging.ServiceBus;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBDlqCmdletsTests : SBCommandTestBase
{
    public SBDlqCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Advanced_dlq_cmdlets_replay_and_clear_messages()
    {
        var admin = CreateAdminClient();
        var source = UniqueName("mgmt-dlq-src");
        var destination = UniqueName("mgmt-dlq-dst");

        try
        {
            admin.CreateQueueAsync(source).GetAwaiter().GetResult();
            admin.CreateQueueAsync(destination).GetAwaiter().GetResult();

            _fixture.SendToQueue(source, _fixture.NewMessages(null, new[] { "replay-complete" }));
            DeadLetterSingleQueueMessage(source);
            WaitForDlqMessage(source);

            Invoke(ps =>
            {
                ps.AddCommand("Replay-SBDLQMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", source)
                    .AddParameter("DestinationQueue", destination)
                    .AddParameter("MaxMessages", 1);
            });

            var replayed = _fixture.ReceiveFromQueue(destination, waitSeconds: 3);
            Assert.Single(replayed);
            Assert.Equal("replay-complete", replayed[0].Body.ToString());
            Assert.Empty(_fixture.ReceiveDlqFromQueue(source, waitSeconds: 1));

            _fixture.SendToQueue(source, _fixture.NewMessages(null, new[] { "replay-no-complete" }));
            DeadLetterSingleQueueMessage(source);
            WaitForDlqMessage(source);

            Invoke(ps =>
            {
                ps.AddCommand("Replay-SBDLQMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", source)
                    .AddParameter("DestinationQueue", destination)
                    .AddParameter("MaxMessages", 1)
                    .AddParameter("NoCompleteSource", true);
            });

            var stillInDlq = _fixture.ReceiveDlqFromQueue(source, maxMessages: 1, peek: true);
            Assert.Single(stillInDlq);

            Invoke(ps =>
            {
                ps.AddCommand("Clear-SBDLQ")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", source);
            });

            Assert.Empty(_fixture.ReceiveDlqFromQueue(source, waitSeconds: 1));
        }
        finally
        {
            SafeDeleteQueue(admin, source);
            SafeDeleteQueue(admin, destination);
        }
    }
}
