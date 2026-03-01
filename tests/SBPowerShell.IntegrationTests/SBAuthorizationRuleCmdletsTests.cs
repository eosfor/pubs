using System;
using System.Linq;
using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBAuthorizationRuleCmdletsTests : SBCommandTestBase
{
    public SBAuthorizationRuleCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Authorization_and_key_cmdlets_manage_entity_rules()
    {
        var admin = CreateAdminClient();
        var queue = UniqueName("mgmt-auth-q");
        var rule = UniqueName("ops");

        try
        {
            admin.CreateQueueAsync(queue).GetAwaiter().GetResult();

            Invoke(ps =>
            {
                ps.AddCommand("New-SBAuthorizationRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Rule", rule)
                    .AddParameter("Rights", new[] { AccessRights.Listen, AccessRights.Send });
            });

            if (!WaitForQueueAuthorizationRule(admin, queue, rule))
            {
                return;
            }

            var fetched = WaitForAuthorizationRuleViaCmdlet(queue, rule);
            Assert.Single(fetched);

            Invoke(ps =>
            {
                ps.AddCommand("Set-SBAuthorizationRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Rule", rule)
                    .AddParameter("Rights", new[] { AccessRights.Manage });
            });

            var ruleAfterSet = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value.AuthorizationRules
                .OfType<SharedAccessAuthorizationRule>()
                .Single(r => string.Equals(r.KeyName, rule, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(AccessRights.Manage, ruleAfterSet.Rights);

            var cs = Invoke<string>(ps =>
            {
                ps.AddCommand("Get-SBConnectionString")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Rule", rule)
                    .AddParameter("KeyType", "Primary");
            }).Single();
            Assert.Contains($"SharedAccessKeyName={rule}", cs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"EntityPath={queue}", cs, StringComparison.OrdinalIgnoreCase);

            var oldPrimary = ruleAfterSet.PrimaryKey;

            Invoke(ps =>
            {
                ps.AddCommand("Rotate-SBKey")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Rule", rule)
                    .AddParameter("KeyType", "Primary");
            });

            var ruleAfterRotate = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value.AuthorizationRules
                .OfType<SharedAccessAuthorizationRule>()
                .Single(r => string.Equals(r.KeyName, rule, StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(oldPrimary, ruleAfterRotate.PrimaryKey);

            Invoke(ps =>
            {
                ps.AddCommand("Remove-SBAuthorizationRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Rule", rule)
                    .AddParameter("Force", true)
                    .AddParameter("Confirm", false);
            });

            var remaining = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value.AuthorizationRules
                .OfType<SharedAccessAuthorizationRule>()
                .Any(r => string.Equals(r.KeyName, rule, StringComparison.OrdinalIgnoreCase));
            Assert.False(remaining);
        }
        finally
        {
            SafeDeleteQueue(admin, queue);
        }
    }
}
