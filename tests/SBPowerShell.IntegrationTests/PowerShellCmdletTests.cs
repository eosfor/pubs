using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell.Models;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SBPowerShell.IntegrationTests;

public class ServiceBusFixture : IAsyncLifetime
{
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, string> _env;

    public ServiceBusFixture()
    {
        RepoRoot = LocateRepoRoot();
        _env = LoadEnv(Path.Combine(RepoRoot, ".env"));

        EmulatorHost = GetEnvValueOrDefault("EMULATOR_HOST", "localhost");
        AmqpPort = int.TryParse(GetEnvValueOrDefault("EMULATOR_AMQP_PORT", "5672"), out var amqp) ? amqp : 5672;
        HttpPort = int.TryParse(GetEnvValueOrDefault("EMULATOR_HTTP_PORT", "5300"), out var http) ? http : 5300;

        var sasKey = GetEnvValue("SAS_KEY_VALUE") ?? throw new InvalidOperationException("SAS_KEY_VALUE missing in .env");
        ConnectionString = $"Endpoint=sb://{EmulatorHost};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={sasKey};UseDevelopmentEmulator=true;";

        ModulePath = Path.Combine(AppContext.BaseDirectory, "SBPowerShell.psd1");
        if (!File.Exists(ModulePath))
        {
            // fallback to module output in project bin
            var debugModule = Path.Combine(RepoRoot, "src", "SBPowerShell", "bin", "Debug", "net8.0", "SBPowerShell.psd1");
            var releaseModule = Path.Combine(RepoRoot, "src", "SBPowerShell", "bin", "Release", "net8.0", "SBPowerShell.psd1");
            ModulePath = File.Exists(debugModule) ? debugModule : releaseModule;
        }
    }

    public string RepoRoot { get; } = null!;
    public string ConnectionString { get; } = null!;
    public string ModulePath { get; } = null!;
    public string EmulatorHost { get; } = null!;
    public int AmqpPort { get; }
    public int HttpPort { get; }

    public async Task InitializeAsync()
    {
        EnsureEmulatorUp();
        await WaitForEmulatorAsync(TimeSpan.FromSeconds(180));
        await ClearQueuesAndSubscriptions();
    }

    public Task DisposeAsync()
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    public PowerShell CreateShell()
    {
        var ps = PowerShell.Create();
        ps.AddCommand("Import-Module").AddArgument(ModulePath).AddParameter("Force", true).Invoke();
        EnsureNoErrors(ps);
        ps.Commands.Clear();
        return ps;
    }

    public PSMessage[] NewMessages(string? sessionId, IEnumerable<string> bodies, IDictionary<string, object>? customProps = null)
    {
        using var ps = CreateShell();
        ps.AddCommand("New-SBMessage");
        ps.AddParameter("Body", bodies.ToArray());
        if (!string.IsNullOrEmpty(sessionId))
        {
            ps.AddParameter("SessionId", sessionId);
        }

        if (customProps != null && customProps.Count > 0)
        {
            var ht = new Hashtable();
            foreach (var kv in customProps)
            {
                ht[kv.Key] = kv.Value;
            }
            ps.AddParameter("CustomProperties", new[] { ht });
        }

        var result = ps.Invoke<PSObject>();
        EnsureNoErrors(ps);
        return result.Select(o => (PSMessage)o.BaseObject).ToArray();
    }

    public void SendToQueue(string queue, PSMessage[] messages, bool perSessionThreadAuto = false, int perSessionThread = 0)
    {
        using var ps = CreateShell();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Queue", queue)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .AddParameter("Message", messages);

        if (perSessionThreadAuto)
        {
            ps.AddParameter("PerSessionThreadAuto", true);
        }

        if (perSessionThread > 0)
        {
            ps.AddParameter("PerSessionThread", perSessionThread);
        }

        ps.Invoke();
        EnsureNoErrors(ps);
    }

    public void SendToTopic(string topic, PSMessage[] messages, bool perSessionThreadAuto = false)
    {
        using var ps = CreateShell();
        ps.AddCommand("Send-SBMessage")
            .AddParameter("Topic", topic)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .AddParameter("Message", messages);

        if (perSessionThreadAuto)
        {
            ps.AddParameter("PerSessionThreadAuto", true);
        }

        ps.Invoke();
        EnsureNoErrors(ps);
    }

    public ServiceBusReceivedMessage[] ReceiveFromQueue(string queue, int maxMessages, int batchSize = 10, int waitSeconds = 5, bool peek = false)
    {
        using var ps = CreateShell();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("Queue", queue)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .AddParameter("MaxMessages", maxMessages)
            .AddParameter("BatchSize", batchSize)
            .AddParameter("WaitSeconds", waitSeconds);

        if (peek)
        {
            ps.AddParameter("Peek", true);
        }

        var result = ps.Invoke<ServiceBusReceivedMessage>();
        EnsureNoErrors(ps);
        return result.ToArray();
    }

    public ServiceBusReceivedMessage[] ReceiveDlqFromQueue(string queue, int maxMessages, int batchSize = 10, int waitSeconds = 5, bool peek = false)
    {
        using var ps = CreateShell();
        ps.AddCommand("Receive-SBDLQMessage")
            .AddParameter("Queue", queue)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .AddParameter("MaxMessages", maxMessages)
            .AddParameter("BatchSize", batchSize)
            .AddParameter("WaitSeconds", waitSeconds);

        if (peek)
        {
            ps.AddParameter("Peek", true);
        }

        var result = ps.Invoke<ServiceBusReceivedMessage>();
        EnsureNoErrors(ps);
        return result.ToArray();
    }

    public ServiceBusReceivedMessage[] ReceiveFromSubscription(string topic, string subscription, int maxMessages, int waitSeconds = 5, bool peek = false)
    {
        using var ps = CreateShell();
        ps.AddCommand("Receive-SBMessage")
            .AddParameter("Topic", topic)
            .AddParameter("Subscription", subscription)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .AddParameter("MaxMessages", maxMessages)
            .AddParameter("WaitSeconds", waitSeconds);

        if (peek)
        {
            ps.AddParameter("Peek", true);
        }

        var result = ps.Invoke<ServiceBusReceivedMessage>();
        EnsureNoErrors(ps);
        return result.ToArray();
    }

    public ServiceBusReceivedMessage[] ReceiveDlqFromSubscription(string topic, string subscription, int maxMessages, int waitSeconds = 5, bool peek = false)
    {
        using var ps = CreateShell();
        ps.AddCommand("Receive-SBDLQMessage")
            .AddParameter("Topic", topic)
            .AddParameter("Subscription", subscription)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .AddParameter("MaxMessages", maxMessages)
            .AddParameter("WaitSeconds", waitSeconds);

        if (peek)
        {
            ps.AddParameter("Peek", true);
        }

        var result = ps.Invoke<ServiceBusReceivedMessage>();
        EnsureNoErrors(ps);
        return result.ToArray();
    }

    public void ClearQueue(string queue)
    {
        using var ps = CreateShell();
        ps.AddCommand("Clear-SBQueue")
            .AddParameter("Queue", queue)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .Invoke();
        EnsureNoErrors(ps);
    }

    public void ClearDlqQueue(string queue)
    {
        while (true)
        {
            var drained = ReceiveDlqFromQueue(queue, maxMessages: 100, waitSeconds: 1);
            if (drained.Length == 0)
            {
                break;
            }
        }
    }

    public void ClearDlqSubscription(string topic, string subscription)
    {
        while (true)
        {
            var drained = ReceiveDlqFromSubscription(topic, subscription, maxMessages: 100, waitSeconds: 1);
            if (drained.Length == 0)
            {
                break;
            }
        }
    }

    public void ClearSubscription(string topic, string subscription)
    {
        using var ps = CreateShell();
        ps.AddCommand("Clear-SBSubscription")
            .AddParameter("Topic", topic)
            .AddParameter("Subscription", subscription)
            .AddParameter("ServiceBusConnectionString", ConnectionString)
            .Invoke();
        EnsureNoErrors(ps);
    }

    public PSObject[] GetTopics()
    {
        using var ps = CreateShell();
        ps.AddCommand("Get-SBTopic")
            .AddParameter("ServiceBusConnectionString", ConnectionString);
        var result = ps.Invoke<PSObject>().ToArray();
        EnsureNoErrors(ps);
        return result;
    }

    public PSObject[] GetSubscriptions(string topic, string? subscription = null)
    {
        using var ps = CreateShell();
        ps.AddCommand("Get-SBSubscription")
            .AddParameter("Topic", topic)
            .AddParameter("ServiceBusConnectionString", ConnectionString);

        if (!string.IsNullOrEmpty(subscription))
        {
            ps.AddParameter("Subscription", subscription);
        }

        var result = ps.Invoke<PSObject>().ToArray();
        EnsureNoErrors(ps);
        return result;
    }

    internal static void EnsureNoErrors(PowerShell ps)
    {
        if (!ps.HadErrors)
        {
            return;
        }

        var errors = ps.Streams.Error.Select(e => e.ToString()).ToArray();
        throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, errors));
    }

    private Task ClearQueuesAndSubscriptions()
    {
        ClearQueue("test-queue");
        ClearQueue("session-queue");
        ClearSubscription("test-topic", "test-sub");
        ClearSubscription("session-topic", "session-sub");
        ClearDlqQueue("test-queue");
        ClearDlqQueue("session-queue");
        ClearDlqSubscription("test-topic", "test-sub");
        ClearDlqSubscription("session-topic", "session-sub");
        return Task.CompletedTask;
    }

    private void EnsureEmulatorUp()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $@"compose -f ""{Path.Combine(RepoRoot, "docker-compose.sbus.yml")}"" up -d --force-recreate",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(30000);
        }
        catch
        {
            // best-effort; emulator might already be running
        }
    }

    private async Task WaitForEmulatorAsync(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        string? lastHttpError = null;
        while (sw.Elapsed < timeout)
        {
            var httpOk = await CheckHttpAsync();
            var tcpOk = await CheckTcpAsync();

            if (httpOk && tcpOk)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Service Bus emulator not ready within timeout ({timeout.TotalSeconds}s). Last HTTP error: {lastHttpError}");
    }

    private async Task<bool> CheckHttpAsync()
    {
        try
        {
            var resp = await _httpClient.GetAsync($"http://{EmulatorHost}:{HttpPort}/health");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckTcpAsync()
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(EmulatorHost, AmqpPort);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(2)));
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private string GetEnvValueOrDefault(string key, string defaultValue)
    {
        var value = GetEnvValue(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value!;
    }

    private string? GetEnvValue(string key)
    {
        if (_env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }

    private static Dictionary<string, string> LoadEnv(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return dict;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                dict[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return dict;
    }

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "pubs.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}

[CollectionDefinition("SBPowerShellIntegration")]
public sealed class ServiceBusCollection : ICollectionFixture<ServiceBusFixture>
{
}

[Collection("SBPowerShellIntegration")]
public class PowerShellCmdletTests
{
    private readonly ServiceBusFixture _fixture;

    public PowerShellCmdletTests(ServiceBusFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Sends_and_receives_non_session_queue_messages()
    {
        _fixture.ClearQueue("test-queue");

        var messages = _fixture.NewMessages(null, new[] { "hello", "world" }, new Dictionary<string, object> { ["prop"] = "v1" });
        _fixture.SendToQueue("test-queue", messages);

        var received = _fixture.ReceiveFromQueue("test-queue", maxMessages: 2);
        Assert.Equal(2, received.Length);
        Assert.All(received.Select(m => m.ApplicationProperties["prop"]), v => Assert.Equal("v1", v));

        var bodies = received.Select(m => m.Body.ToString()).ToArray();
        Assert.Contains("hello", bodies);
        Assert.Contains("world", bodies);
    }

    [Fact]
    public void Peek_does_not_remove_messages()
    {
        _fixture.ClearQueue("test-queue");

        var messages = _fixture.NewMessages(null, new[] { "peek-1", "peek-2" });
        _fixture.SendToQueue("test-queue", messages);

        var peeked = _fixture.ReceiveFromQueue("test-queue", maxMessages: 2, waitSeconds: 1, peek: true);
        Assert.Equal(2, peeked.Length);

        var received = _fixture.ReceiveFromQueue("test-queue", maxMessages: 2, waitSeconds: 1, peek: false);
        Assert.Equal(2, received.Length);

        var peekBodies = peeked.Select(m => m.Body.ToString()).OrderBy(x => x).ToArray();
        var recvBodies = received.Select(m => m.Body.ToString()).OrderBy(x => x).ToArray();
        Assert.Equal(peekBodies, recvBodies);
    }

    [Fact]
    public void Sends_and_receives_session_queue_messages_preserving_SessionId()
    {
        _fixture.ClearQueue("session-queue");

        var messages = _fixture.NewMessages("sess-1", new[] { "s1", "s2" });
        _fixture.SendToQueue("session-queue", messages, perSessionThreadAuto: true);

        var received = _fixture.ReceiveFromQueue("session-queue", maxMessages: 2);
        Assert.Equal(2, received.Length);
        Assert.All(received, m => Assert.Equal("sess-1", m.SessionId));
    }

    [Fact]
    public void Sends_to_topic_and_receives_from_subscription()
    {
        _fixture.ClearSubscription("test-topic", "test-sub");

        var messages = _fixture.NewMessages(null, new[] { "topic-msg" });
        _fixture.SendToTopic("test-topic", messages);

        var received = _fixture.ReceiveFromSubscription("test-topic", "test-sub", maxMessages: 1);
        Assert.Single(received);
        Assert.Equal("topic-msg", received[0].Body.ToString());
    }

    [Fact]
    public void Sends_and_receives_session_topic_messages_preserving_SessionId()
    {
        _fixture.ClearSubscription("session-topic", "session-sub");

        var messages = _fixture.NewMessages("sess-topic", new[] { "ts1", "ts2" });
        _fixture.SendToTopic("session-topic", messages, perSessionThreadAuto: true);

        var received = _fixture.ReceiveFromSubscription("session-topic", "session-sub", maxMessages: 2);
        Assert.Equal(2, received.Length);
        Assert.All(received, m => Assert.Equal("sess-topic", m.SessionId));
    }

    [Fact]
    public void Receives_multiple_messages_from_subscription()
    {
        _fixture.ClearSubscription("test-topic", "test-sub");

        var messages = _fixture.NewMessages(null, new[] { "sub-1", "sub-2" });
        _fixture.SendToTopic("test-topic", messages);

        var received = _fixture.ReceiveFromSubscription("test-topic", "test-sub", maxMessages: 2, waitSeconds: 1);
        Assert.Equal(2, received.Length);

        var bodies = received.Select(m => m.Body.ToString()).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "sub-1", "sub-2" }, bodies);
    }

    [Fact]
    public void Peek_from_subscription_does_not_remove_messages()
    {
        _fixture.ClearSubscription("test-topic", "test-sub");

        var messages = _fixture.NewMessages(null, new[] { "peek-topic-1", "peek-topic-2" });
        _fixture.SendToTopic("test-topic", messages);

        var peeked = _fixture.ReceiveFromSubscription("test-topic", "test-sub", maxMessages: 2, waitSeconds: 1, peek: true);
        Assert.Equal(2, peeked.Length);

        var received = _fixture.ReceiveFromSubscription("test-topic", "test-sub", maxMessages: 2, waitSeconds: 1, peek: false);
        Assert.Equal(2, received.Length);

        Assert.Equal(
            peeked.Select(m => m.Body.ToString()).OrderBy(x => x),
            received.Select(m => m.Body.ToString()).OrderBy(x => x));
    }

    [Fact]
    public void Pipes_received_messages_into_send()
    {
        _fixture.ClearQueue("test-queue");
        _fixture.ClearSubscription("test-topic", "test-sub");
        _fixture.ClearDlqQueue("test-queue");
        _fixture.ClearDlqSubscription("test-topic", "test-sub");

        var messages = _fixture.NewMessages(null, new[] { "pipe-one" });
        _fixture.SendToQueue("test-queue", messages);

        using (var ps = _fixture.CreateShell())
        {
            ps.AddCommand("Receive-SBMessage")
                .AddParameter("Queue", "test-queue")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("MaxMessages", 1);

            ps.AddCommand("Send-SBMessage")
                .AddParameter("Topic", "test-topic")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString);

            ps.Invoke();
            Assert.False(ps.HadErrors);
        }

        var received = _fixture.ReceiveFromSubscription("test-topic", "test-sub", maxMessages: 1, waitSeconds: 1);
        Assert.Single(received);
        Assert.Equal("pipe-one", received[0].Body.ToString());
    }

    [Fact]
    public void Defers_and_fetches_deferred_messages()
    {
        _fixture.ClearQueue("test-queue");
        _fixture.ClearDlqQueue("test-queue");

        var messages = _fixture.NewMessages(null, new[] { "defer-one" });
        _fixture.SendToQueue("test-queue", messages);

        long seqNumber;
        using (var ps = _fixture.CreateShell())
        {
            ps.AddCommand("Receive-SBMessage")
                .AddParameter("Queue", "test-queue")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("MaxMessages", 1)
                .AddParameter("NoComplete", true);

            ps.AddCommand("Set-SBMessage")
                .AddParameter("Queue", "test-queue")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("Defer", true);

            var result = ps.Invoke<ServiceBusReceivedMessage>();
            Assert.False(ps.HadErrors);
            seqNumber = result.Single().SequenceNumber;
        }

        ServiceBusReceivedMessage[] deferred;
        using (var ps = _fixture.CreateShell())
        {
            ps.AddCommand("Receive-SBDeferredMessage")
                .AddParameter("Queue", "test-queue")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("SequenceNumber", new[] { seqNumber });

            deferred = ps.Invoke<ServiceBusReceivedMessage>().ToArray();
            Assert.False(ps.HadErrors);
        }

        Assert.Single(deferred);
        Assert.Equal("defer-one", deferred[0].Body.ToString());
    }

    [Fact]
    public void Reads_deadletter_queue_messages_for_queue()
    {
        _fixture.ClearQueue("test-queue");
        _fixture.ClearDlqQueue("test-queue");

        var messages = _fixture.NewMessages(null, new[] { "dlq-queue" });
        _fixture.SendToQueue("test-queue", messages);

        using (var ps = _fixture.CreateShell())
        {
            ps.AddCommand("Receive-SBMessage")
                .AddParameter("Queue", "test-queue")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("MaxMessages", 1)
                .AddParameter("NoComplete", true);

            ps.AddCommand("Set-SBMessage")
                .AddParameter("Queue", "test-queue")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("DeadLetter", true)
                .AddParameter("DeadLetterReason", "integration-test");

            ps.Invoke();
            ServiceBusFixture.EnsureNoErrors(ps);
        }

        var peeked = _fixture.ReceiveDlqFromQueue("test-queue", maxMessages: 1, waitSeconds: 1, peek: true);
        Assert.Single(peeked);
        Assert.Equal("dlq-queue", peeked[0].Body.ToString());

        var received = _fixture.ReceiveDlqFromQueue("test-queue", maxMessages: 1, waitSeconds: 1);
        Assert.Single(received);

        var shouldBeEmpty = _fixture.ReceiveDlqFromQueue("test-queue", maxMessages: 1, waitSeconds: 1);
        Assert.Empty(shouldBeEmpty);
    }

    [Fact]
    public void Reads_deadletter_queue_messages_for_subscription()
    {
        _fixture.ClearSubscription("test-topic", "test-sub");
        _fixture.ClearDlqSubscription("test-topic", "test-sub");

        var messages = _fixture.NewMessages(null, new[] { "dlq-sub" });
        _fixture.SendToTopic("test-topic", messages);

        using (var ps = _fixture.CreateShell())
        {
            ps.AddCommand("Receive-SBMessage")
                .AddParameter("Topic", "test-topic")
                .AddParameter("Subscription", "test-sub")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("MaxMessages", 1)
                .AddParameter("NoComplete", true);

            ps.AddCommand("Set-SBMessage")
                .AddParameter("Topic", "test-topic")
                .AddParameter("Subscription", "test-sub")
                .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString)
                .AddParameter("DeadLetter", true)
                .AddParameter("DeadLetterReason", "integration-test-sub");

            ps.Invoke();
            ServiceBusFixture.EnsureNoErrors(ps);
        }

        var peeked = _fixture.ReceiveDlqFromSubscription("test-topic", "test-sub", maxMessages: 1, waitSeconds: 1, peek: true);
        Assert.Single(peeked);
        Assert.Equal("dlq-sub", peeked[0].Body.ToString());

        var received = _fixture.ReceiveDlqFromSubscription("test-topic", "test-sub", maxMessages: 1, waitSeconds: 1);
        Assert.Single(received);

        var shouldBeEmpty = _fixture.ReceiveDlqFromSubscription("test-topic", "test-sub", maxMessages: 1, waitSeconds: 1);
        Assert.Empty(shouldBeEmpty);
    }
    [Fact]
    public void Sends_multiple_sessions_in_parallel_when_PerSessionThreadAuto_is_set()
    {
        _fixture.ClearQueue("session-queue");

        var sessionA = "auto-sess-a";
        var sessionB = "auto-sess-b";
        var msgA = _fixture.NewMessages(sessionA, new[] { "a1", "a2", "a3", "a4", "a5" });
        var msgB = _fixture.NewMessages(sessionB, new[] { "b1", "b2", "b3", "b4", "b5" });

        _fixture.SendToQueue("session-queue", msgA.Concat(msgB).ToArray(), perSessionThreadAuto: true);

        var received = _fixture.ReceiveFromQueue("session-queue", maxMessages: 10, batchSize: 10, waitSeconds: 2);
        Assert.Equal(10, received.Length);

        var bySession = received.GroupBy(m => m.SessionId).ToDictionary(g => g.Key!, g => g.ToList());
        Assert.Equal(2, bySession.Count);
        Assert.Equal(5, bySession[sessionA].Count);
        Assert.Equal(5, bySession[sessionB].Count);
    }

    [Fact]
    public void Uses_multiple_sender_threads_when_PerSessionThread_is_specified()
    {
        _fixture.ClearQueue("session-queue");

        var sessionId = "parallel-sess";
        var payloads = Enumerable.Range(1, 16).Select(i => $"p{i}").ToArray();
        var messages = _fixture.NewMessages(sessionId, payloads);

        _fixture.SendToQueue("session-queue", messages, perSessionThread: 4);

        var received = _fixture.ReceiveFromQueue("session-queue", maxMessages: 16, batchSize: 8, waitSeconds: 2);
        Assert.Equal(16, received.Length);
        Assert.All(received, m => Assert.Equal(sessionId, m.SessionId));

        var receivedBodies = received.Select(m => m.Body.ToString()).OrderBy(x => x).ToArray();
        var expected = payloads.OrderBy(x => x).ToArray();
        Assert.Equal(expected, receivedBodies);
    }

    [Fact]
    public void Lists_topics_with_runtime_properties()
    {
        var topics = _fixture.GetTopics();
        Assert.NotEmpty(topics);

        var testTopic = topics.SingleOrDefault(t => string.Equals(t.Properties["Name"]?.Value?.ToString(), "test-topic", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(testTopic);

        var runtime = testTopic!.Properties["RuntimeProperties"]?.Value as TopicRuntimeProperties;
        Assert.NotNull(runtime);
    }

    [Fact]
    public void Lists_subscriptions_with_runtime_counts_and_supports_pipeline()
    {
        _fixture.ClearSubscription("test-topic", "test-sub");

        var msg = _fixture.NewMessages(null, new[] { "count-me" });
        _fixture.SendToTopic("test-topic", msg);

        // allow admin stats to update
        Thread.Sleep(500);

        var subs = _fixture.GetSubscriptions("test-topic");
        var sub = subs.SingleOrDefault(s => string.Equals(s.Properties["SubscriptionName"]?.Value?.ToString(), "test-sub", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(sub);

        var runtime = sub!.Properties["RuntimeProperties"]?.Value as SubscriptionRuntimeProperties;
        Assert.NotNull(runtime);
        Assert.True(runtime!.ActiveMessageCount >= 1 || runtime.TotalMessageCount >= 1);

        // pipeline variant
        using var ps = _fixture.CreateShell();
        ps.AddCommand("Get-SBTopic")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString);
        ps.AddCommand("Where-Object")
            .AddParameter("FilterScript", ScriptBlock.Create("$_.Name -eq 'test-topic'"));
        ps.AddCommand("Get-SBSubscription")
            .AddParameter("ServiceBusConnectionString", _fixture.ConnectionString);

        var pipelineSubs = ps.Invoke<PSObject>().ToArray();
        ServiceBusFixture.EnsureNoErrors(ps);

        Assert.Contains(pipelineSubs, s =>
        {
            var name = s.Properties["SubscriptionName"]?.Value?.ToString();
            return string.Equals(name, "test-sub", StringComparison.OrdinalIgnoreCase);
        });
    }
}
