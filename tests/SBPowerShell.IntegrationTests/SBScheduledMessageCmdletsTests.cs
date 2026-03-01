using System;
using System.Linq;
using System.Threading;
using SBPowerShell.Models;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBScheduledMessageCmdletsTests : SBCommandTestBase
{
    public SBScheduledMessageCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Scheduled_message_cmdlets_schedule_receive_and_cancel()
    {
        var admin = CreateAdminClient();
        var queue = UniqueName("mgmt-sched-q");

        try
        {
            admin.CreateQueueAsync(queue).GetAwaiter().GetResult();

            var immediateMsg = _fixture.NewMessages(null, new[] { "scheduled-receive" });
            var scheduled = Invoke<ScheduledMessageResult>(ps =>
            {
                ps.AddCommand("Send-SBScheduledMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Message", immediateMsg)
                    .AddParameter("ScheduleAtUtc", DateTimeOffset.UtcNow.AddSeconds(2));
            });
            Assert.Single(scheduled);
            Assert.True(scheduled[0].SequenceNumber > 0);

            Thread.Sleep(TimeSpan.FromSeconds(3));
            var received = _fixture.ReceiveFromQueue(queue, waitSeconds: 8);
            Assert.Single(received);
            Assert.Equal("scheduled-receive", received[0].Body.ToString());

            var cancelMsg = _fixture.NewMessages(null, new[] { "scheduled-cancel" });
            var toCancel = Invoke<ScheduledMessageResult>(ps =>
            {
                ps.AddCommand("Send-SBScheduledMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Message", cancelMsg)
                    .AddParameter("ScheduleAtUtc", DateTimeOffset.UtcNow.AddMinutes(3));
            }).Single();

            Invoke(ps =>
            {
                ps.AddCommand("Remove-SBScheduledMessage")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("SequenceNumber", new[] { toCancel.SequenceNumber });
            });

            var shouldBeEmpty = _fixture.ReceiveFromQueue(queue, waitSeconds: 2);
            Assert.Empty(shouldBeEmpty);
        }
        finally
        {
            SafeDeleteQueue(admin, queue);
        }
    }
}
