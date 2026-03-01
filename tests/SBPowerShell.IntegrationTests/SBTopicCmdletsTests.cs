using System;
using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBTopicCmdletsTests : SBCommandTestBase
{
    public SBTopicCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Topic_lifecycle_cmdlets_work_end_to_end()
    {
        var admin = CreateAdminClient();
        var topic = UniqueName("mgmt-topic");

        try
        {
            Invoke(ps =>
            {
                ps.AddCommand("New-SBTopic")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("EnableBatchedOperations", true);
            });

            var created = admin.GetTopicAsync(topic).GetAwaiter().GetResult().Value;
            Assert.Equal(topic, created.Name);
            Assert.True(created.EnableBatchedOperations);

            var setResult = Invoke<TopicProperties>(ps =>
            {
                ps.AddCommand("Set-SBTopic")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("EnableBatchedOperations", false)
                    .AddParameter("UserMetadata", "topic-updated");
            });
            Assert.Single(setResult);
            Assert.Equal(topic, setResult[0].Name);

            var updated = admin.GetTopicAsync(topic).GetAwaiter().GetResult().Value;
            Assert.Equal(topic, updated.Name);

            Invoke(ps =>
            {
                ps.AddCommand("Remove-SBTopic")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Topic", topic)
                    .AddParameter("Force", true)
                    .AddParameter("Confirm", false);
            });

            Assert.False(admin.TopicExistsAsync(topic).GetAwaiter().GetResult());
        }
        finally
        {
            SafeDeleteTopic(admin, topic);
        }
    }
}
