using System;
using System.Linq;
using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBTopologyCmdletsTests : SBCommandTestBase
{
    public SBTopologyCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Topology_export_and_import_cmdlets_roundtrip_entities()
    {
        var admin = CreateAdminClient();
        var queue = UniqueName("mgmt-topo-q");
        var topic = UniqueName("mgmt-topo-topic");
        var subscription = UniqueName("mgmt-topo-sub");
        var rule = UniqueName("mgmt-topo-rule");
        var filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sb-topology-{Guid.NewGuid():N}.json");

        try
        {
            admin.CreateQueueAsync(new CreateQueueOptions(queue) { UserMetadata = "topology-test" }).GetAwaiter().GetResult();
            admin.CreateTopicAsync(topic).GetAwaiter().GetResult();
            admin.CreateSubscriptionAsync(topic, subscription).GetAwaiter().GetResult();
            admin.CreateRuleAsync(topic, subscription, new CreateRuleOptions(rule, new SqlRuleFilter("flag = 1"))).GetAwaiter().GetResult();

            var exportedPath = Invoke<string>(ps =>
            {
                ps.AddCommand("Export-SBTopology")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Path", filePath);
            }).Single();

            Assert.True(System.IO.File.Exists(exportedPath));
            var json = System.IO.File.ReadAllText(exportedPath);
            Assert.Contains(queue, json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(topic, json, StringComparison.OrdinalIgnoreCase);

            admin.DeleteQueueAsync(queue).GetAwaiter().GetResult();
            admin.DeleteTopicAsync(topic).GetAwaiter().GetResult();

            Invoke(ps =>
            {
                ps.AddCommand("Import-SBTopology")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Path", filePath)
                    .AddParameter("Mode", "CreateOnly");
            });

            Assert.True(admin.QueueExistsAsync(queue).GetAwaiter().GetResult());
            Assert.True(admin.TopicExistsAsync(topic).GetAwaiter().GetResult());
            Assert.True(admin.SubscriptionExistsAsync(topic, subscription).GetAwaiter().GetResult());
            Assert.True(admin.RuleExistsAsync(topic, subscription, rule).GetAwaiter().GetResult());
        }
        finally
        {
            SafeDeleteRule(admin, topic, subscription, rule);
            SafeDeleteSubscription(admin, topic, subscription);
            SafeDeleteTopic(admin, topic);
            SafeDeleteQueue(admin, queue);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
