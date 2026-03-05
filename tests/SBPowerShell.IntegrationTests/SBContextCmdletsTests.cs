using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBContextCmdletsTests : SBCommandTestBase
{
    public SBContextCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Set_and_get_context_supports_safe_view_raw_and_connection_string()
    {
        using var ps = _fixture.CreateShell();

        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Get-SBContext");
        var safeView = ps.Invoke<PSObject>().Single();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.NotNull(safeView.Properties["HasConnectionString"]?.Value);
        Assert.Equal(true, safeView.Properties["HasConnectionString"]?.Value);
        Assert.Null(safeView.Properties["ServiceBusConnectionString"]);

        ps.Commands.Clear();
        ps.AddCommand("Get-SBContext").AddParameter("Raw", true);
        var raw = ps.Invoke<PSObject>().Single().BaseObject as SBContext;
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.NotNull(raw);
        Assert.Equal(_fixture.ConnectionString, raw!.ServiceBusConnectionString);

        ps.Commands.Clear();
        ps.AddCommand("Get-SBContext").AddParameter("AsConnectionString", true);
        var cs = ps.Invoke<string>().Single();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.Equal(_fixture.ConnectionString, cs);

        ClearContext(ps);
    }

    [Fact]
    public void Set_context_validates_conflicts_and_NoClobber()
    {
        using var ps = _fixture.CreateShell();

        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "test-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "session-queue")
            .AddParameter("NoClobber", true);
        var noClobberEx = Assert.Throws<CmdletInvocationException>(() => ps.Invoke());
        var noClobberErrorId = noClobberEx.ErrorRecord?.FullyQualifiedErrorId
                               ?? noClobberEx.InnerException?.Message
                               ?? noClobberEx.Message;
        Assert.Contains("SBContextAlreadyExists", noClobberErrorId, StringComparison.OrdinalIgnoreCase);

        ps.Commands.Clear();
        var badContext = new SBContext
        {
            ServiceBusConnectionString = _fixture.ConnectionString,
            Queue = "q1",
            Topic = "t1",
            EntityMode = SBContextEntityMode.Queue
        };

        ps.AddCommand("Set-SBContext")
            .AddParameter("InputObject", badContext);
        var invalidContextEx = Assert.Throws<CmdletInvocationException>(() => ps.Invoke());
        var invalidContextErrorId = invalidContextEx.ErrorRecord?.FullyQualifiedErrorId
                                    ?? invalidContextEx.InnerException?.Message
                                    ?? invalidContextEx.Message;
        Assert.Contains("InvalidContext", invalidContextErrorId, StringComparison.OrdinalIgnoreCase);

        ClearContext(ps);
    }

    [Fact]
    public void Clear_context_is_idempotent_and_supports_WhatIf()
    {
        using var ps = _fixture.CreateShell();

        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "test-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Clear-SBContext")
            .AddParameter("WhatIf", true)
            .AddParameter("PassThru", true);
        var whatIf = ps.Invoke<bool>().Single();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.False(whatIf);

        ps.Commands.Clear();
        ps.AddCommand("Get-SBContext").AddParameter("Raw", true);
        var shouldExist = ps.Invoke<PSObject>();
        ServiceBusFixture.EnsureNoErrors(ps);
        Assert.Single(shouldExist);

        ps.Commands.Clear();
        ps.AddCommand("Clear-SBContext")
            .AddParameter("PassThru", true)
            .AddParameter("Confirm", false);
        var removed = ps.Invoke<bool>().Single();
        ServiceBusFixture.EnsureNoErrors(ps);
        Assert.True(removed);

        ps.Commands.Clear();
        ps.AddCommand("Clear-SBContext")
            .AddParameter("Force", true)
            .AddParameter("PassThru", true);
        var removedAgain = ps.Invoke<bool>().Single();
        ServiceBusFixture.EnsureNoErrors(ps);
        Assert.False(removedAgain);
    }

    [Fact]
    public void Resolver_supports_context_only_send_and_receive_for_queue()
    {
        _fixture.ClearQueue("test-queue");

        var messages = _fixture.NewMessages(null, new[] { "ctx-queue" }, new Dictionary<string, object> { ["mode"] = "ctx" });

        using var ps = _fixture.CreateShell();
        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "test-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Message", messages);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("MaxMessages", 1);
        var received = ps.Invoke<ServiceBusReceivedMessage>().ToArray();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.Single(received);
        Assert.Equal("ctx-queue", received[0].Body.ToString());
        Assert.Equal("ctx", received[0].ApplicationProperties["mode"]?.ToString());

        ClearContext(ps);
    }

    [Fact]
    public void Resolver_supports_context_only_subscription_paths()
    {
        _fixture.ClearSubscription("test-topic", "test-sub");

        var messages = _fixture.NewMessages(null, new[] { "ctx-sub" });

        using var ps = _fixture.CreateShell();
        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Topic", "test-topic")
            .AddParameter("Subscription", "test-sub");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Message", messages);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Get-SBSubscription");
        var subscriptions = ps.Invoke<PSObject>();
        ServiceBusFixture.EnsureNoErrors(ps);
        Assert.Contains(subscriptions, s =>
            string.Equals(s.Properties["SubscriptionName"]?.Value?.ToString(), "test-sub", StringComparison.OrdinalIgnoreCase));

        ps.Commands.Clear();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("MaxMessages", 1);
        var received = ps.Invoke<ServiceBusReceivedMessage>().ToArray();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.Single(received);
        Assert.Equal("ctx-sub", received[0].Body.ToString());

        ClearContext(ps);
    }

    [Fact]
    public void Resolver_supports_context_only_Get_SBTopic()
    {
        using var ps = _fixture.CreateShell();

        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Get-SBTopic");
        var topics = ps.Invoke<PSObject>();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.NotEmpty(topics);
        Assert.Contains(topics, t =>
            string.Equals(t.Properties["Name"]?.Value?.ToString(), "test-topic", StringComparison.OrdinalIgnoreCase));

        ClearContext(ps);
    }

    [Fact]
    public void Explicit_parameters_override_context_target()
    {
        _fixture.ClearQueue("test-queue");
        _fixture.ClearQueue("session-queue");

        var messages = _fixture.NewMessages(null, new[] { "explicit-wins" });

        using var ps = _fixture.CreateShell();
        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "session-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Queue", "test-queue")
            .AddParameter("Message", messages);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("Queue", "test-queue")
            .AddParameter("MaxMessages", 1);
        var received = ps.Invoke<ServiceBusReceivedMessage>().ToArray();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.Single(received);
        Assert.Equal("explicit-wins", received[0].Body.ToString());

        ClearContext(ps);
    }

    [Fact]
    public void New_session_context_uses_SBContext_defaults()
    {
        _fixture.ClearQueue("session-queue");
        var sessionId = $"ctx-session-{Guid.NewGuid():N}";

        var messages = _fixture.NewMessages(sessionId, new[] { "seed-session" });

        using var ps = _fixture.CreateShell();
        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "session-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Message", messages);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("New-SBSessionContext")
            .AddParameter("SessionId", sessionId);
        var context = ps.Invoke<PSObject>().Single().BaseObject as SessionContext;
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.NotNull(context);
        Assert.Equal(sessionId, context!.SessionId);
        Assert.Equal("session-queue", context.QueueName);

        ps.Commands.Clear();
        ps.AddCommand("Close-SBSessionContext")
            .AddParameter("Context", new[] { context });
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ClearContext(ps);
    }

    [Fact]
    public void SessionContext_conflict_returns_standardized_error()
    {
        _fixture.ClearQueue("session-queue");

        var sessionId = $"conflict-{Guid.NewGuid():N}";
        var messages = _fixture.NewMessages(sessionId, new[] { "conflict-body" });

        using var ps = _fixture.CreateShell();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Queue", "session-queue")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Message", messages)
            .AddParameter("PerSessionThreadAuto", true);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("New-SBSessionContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "session-queue")
            .AddParameter("SessionId", sessionId);
        var context = ps.Invoke<PSObject>().Single().BaseObject;
        ServiceBusFixture.EnsureNoErrors(ps);

        try
        {
            ps.Commands.Clear();
            ps.AddCommand("Receive-SBMessage")
                .AddParameter("SessionContext", context)
                .AddParameter("MaxMessages", 1)
                .AddParameter("NoComplete", true);
            var pending = ps.Invoke<ServiceBusReceivedMessage>().ToArray();
            ServiceBusFixture.EnsureNoErrors(ps);
            Assert.Single(pending);

            ps.Commands.Clear();
            ps.AddCommand("Set-SBMessage")
                .AddParameter("SessionContext", context)
                .AddParameter("Topic", "session-topic")
                .AddParameter("Subscription", "session-sub")
                .AddParameter("Complete", true)
                .AddParameter("Message", pending);
            var ex = Assert.Throws<CmdletInvocationException>(() => ps.Invoke());
            var fullyQualifiedErrorId = ex.ErrorRecord?.FullyQualifiedErrorId
                                        ?? ex.InnerException?.Message
                                        ?? ex.Message;
            Assert.Contains("SessionContextEntityMismatch", fullyQualifiedErrorId, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ps.Commands.Clear();
            ps.AddCommand("Close-SBSessionContext")
                .AddParameter("Context", new[] { context });
            ps.Invoke();
            ServiceBusFixture.EnsureNoErrors(ps);
        }
    }

    [Fact]
    public void NoContext_disables_default_context_fallback()
    {
        using var ps = _fixture.CreateShell();

        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "test-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("MaxMessages", 1)
            .AddParameter("NoContext", true);
        var ex = Assert.Throws<CmdletInvocationException>(() => ps.Invoke());
        var fullyQualifiedErrorId = ex.ErrorRecord?.FullyQualifiedErrorId
                                    ?? ex.InnerException?.Message
                                    ?? ex.Message;
        Assert.Contains("MissingConnectionString", fullyQualifiedErrorId, StringComparison.OrdinalIgnoreCase);

        ClearContext(ps);
    }

    [Fact]
    public void Clear_queue_respects_WhatIf_with_context_target()
    {
        _fixture.ClearQueue("test-queue");
        var messages = _fixture.NewMessages(null, new[] { "whatif-message" });

        using var ps = _fixture.CreateShell();

        ps.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "test-queue");
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Message", messages);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Clear-SBQueue")
            .AddParameter("WhatIf", true);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);

        ps.Commands.Clear();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("MaxMessages", 1);
        var remaining = ps.Invoke<ServiceBusReceivedMessage>().ToArray();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.Single(remaining);
        Assert.Equal("whatif-message", remaining[0].Body.ToString());

        ClearContext(ps);
    }

    [Fact]
    public void Context_is_runspace_local()
    {
        using var shellA = _fixture.CreateShell();
        using var shellB = _fixture.CreateShell();

        shellA.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "test-queue");
        shellA.Invoke();
        ServiceBusFixture.EnsureNoErrors(shellA);

        shellB.AddCommand("Set-SBContext")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
            .AddParameter("Queue", "session-queue");
        shellB.Invoke();
        ServiceBusFixture.EnsureNoErrors(shellB);

        shellA.Commands.Clear();
        shellA.AddCommand("Get-SBContext").AddParameter("Raw", true);
        var contextA = shellA.Invoke<PSObject>().Single().BaseObject as SBContext;
        ServiceBusFixture.EnsureNoErrors(shellA);

        shellB.Commands.Clear();
        shellB.AddCommand("Get-SBContext").AddParameter("Raw", true);
        var contextB = shellB.Invoke<PSObject>().Single().BaseObject as SBContext;
        ServiceBusFixture.EnsureNoErrors(shellB);

        Assert.NotNull(contextA);
        Assert.NotNull(contextB);
        Assert.Equal("test-queue", contextA!.Queue);
        Assert.Equal("session-queue", contextB!.Queue);

        ClearContext(shellA);
        ClearContext(shellB);
    }

    private static void ClearContext(PowerShell ps)
    {
        ps.Commands.Clear();
        ps.AddCommand("Clear-SBContext")
            .AddParameter("Force", true)
            .AddParameter("Confirm", false);
        ps.Invoke();
        ServiceBusFixture.EnsureNoErrors(ps);
    }
}
