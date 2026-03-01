using System;
using System.Linq;
using System.Management.Automation;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBQueueCmdletsTests : SBCommandTestBase
{
    public SBQueueCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Queue_lifecycle_cmdlets_work_end_to_end()
    {
        var admin = CreateAdminClient();
        var queue = UniqueName("mgmt-q");

        try
        {
            Invoke(ps =>
            {
                ps.AddCommand("New-SBQueue")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("MaxDeliveryCount", 7)
                    .AddParameter("EnableBatchedOperations", true);
            });

            var created = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value;
            Assert.Equal(7, created.MaxDeliveryCount);
            Assert.True(created.EnableBatchedOperations);

            var listed = Invoke<PSObject>(ps =>
            {
                ps.AddCommand("Get-SBQueue")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue);
            });
            Assert.Single(listed);

            Invoke(ps =>
            {
                ps.AddCommand("Set-SBQueue")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("MaxDeliveryCount", 9);
            });

            var updated = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value;
            Assert.Equal(9, updated.MaxDeliveryCount);

            Invoke(ps =>
            {
                ps.AddCommand("Remove-SBQueue")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Force", true)
                    .AddParameter("Confirm", false);
            });

            Assert.False(admin.QueueExistsAsync(queue).GetAwaiter().GetResult());
        }
        finally
        {
            SafeDeleteQueue(admin, queue);
        }
    }
}
