using System;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.IntegrationTests;

public abstract class SBCommandTestBase
{
    protected readonly ServiceBusFixture _fixture;

    protected SBCommandTestBase(ServiceBusFixture fixture)
    {
        _fixture = fixture;
    }

    protected void DeadLetterSingleQueueMessage(string queue)
    {
        Invoke(ps =>
        {
            ps.AddCommand("Receive-SBMessage")
                .AddParameter("Queue", queue)
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("MaxMessages", 1)
                .AddParameter("NoComplete", true);

            ps.AddCommand("Set-SBMessage")
                .AddParameter("Queue", queue)
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("DeadLetter", true)
                .AddParameter("DeadLetterReason", "integration-test");
        });
    }

    protected void WaitForDlqMessage(string queue, int timeoutSeconds = 10)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var peeked = _fixture.ReceiveDlqFromQueue(queue, maxMessages: 1, peek: true);
            if (peeked.Length > 0)
            {
                return;
            }

            Thread.Sleep(250);
        }

        throw new Xunit.Sdk.XunitException($"Timed out waiting for DLQ message for queue '{queue}'.");
    }

    protected bool WaitForQueueAuthorizationRule(ServiceBusAdministrationClient admin, string queue, string rule, int timeoutSeconds = 10)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var current = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value.AuthorizationRules
                .OfType<SharedAccessAuthorizationRule>()
                .Any(r => string.Equals(r.KeyName, rule, StringComparison.OrdinalIgnoreCase));
            if (current)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    protected PSObject[] WaitForAuthorizationRuleViaCmdlet(string queue, string rule, int timeoutSeconds = 10)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var fetched = Invoke<PSObject>(ps =>
            {
                ps.AddCommand("Get-SBAuthorizationRule")
                    .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                    .AddParameter("Queue", queue)
                    .AddParameter("Rule", rule);
            });
            if (fetched.Length > 0)
            {
                return fetched;
            }

            Thread.Sleep(250);
        }

        return [];
    }

    protected void Invoke(Action<PowerShell> configure)
    {
        using var ps = _fixture.CreateShell();
        configure(ps);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);
    }

    protected T[] Invoke<T>(Action<PowerShell> configure)
    {
        using var ps = _fixture.CreateShell();
        configure(ps);
        var result = ps.Invoke<T>().ToArray();
        ServiceBusFixture.EnsureNoErrors(ps);
        return result;
    }

    protected ServiceBusAdministrationClient CreateAdminClient()
    {
        var adminConnection = AdjustForAdmin(_fixture.ConnectionString, _fixture.HttpPort);
        return new ServiceBusAdministrationClient(adminConnection);
    }

    protected static string AdjustForAdmin(string connectionString, int httpPort)
    {
        if (!connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return Regex.Replace(
            connectionString,
            @"Endpoint=sb://([^;:]+);",
            $"Endpoint=sb://$1:{httpPort};",
            RegexOptions.IgnoreCase);
    }

    protected static string UniqueName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    protected static void SafeDeleteQueue(ServiceBusAdministrationClient admin, string queue)
    {
        try
        {
            if (admin.QueueExistsAsync(queue).GetAwaiter().GetResult())
            {
                admin.DeleteQueueAsync(queue).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    protected static void SafeDeleteTopic(ServiceBusAdministrationClient admin, string topic)
    {
        try
        {
            if (admin.TopicExistsAsync(topic).GetAwaiter().GetResult())
            {
                admin.DeleteTopicAsync(topic).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    protected static void SafeDeleteSubscription(ServiceBusAdministrationClient admin, string topic, string subscription)
    {
        try
        {
            if (admin.TopicExistsAsync(topic).GetAwaiter().GetResult() &&
                admin.SubscriptionExistsAsync(topic, subscription).GetAwaiter().GetResult())
            {
                admin.DeleteSubscriptionAsync(topic, subscription).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    protected static void SafeDeleteRule(ServiceBusAdministrationClient admin, string topic, string subscription, string rule)
    {
        try
        {
            if (admin.TopicExistsAsync(topic).GetAwaiter().GetResult() &&
                admin.SubscriptionExistsAsync(topic, subscription).GetAwaiter().GetResult() &&
                admin.RuleExistsAsync(topic, subscription, rule).GetAwaiter().GetResult())
            {
                admin.DeleteRuleAsync(topic, subscription, rule).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
