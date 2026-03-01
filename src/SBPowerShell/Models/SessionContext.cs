using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Models;

public sealed class SessionContext : IAsyncDisposable
{
    public SessionContext(
        string connectionString,
        string entityPath,
        string sessionId,
        ServiceBusClient client,
        ServiceBusSessionReceiver receiver,
        string? queueName = null,
        string? topicName = null,
        string? subscriptionName = null)
    {
        ConnectionString = connectionString;
        EntityPath = entityPath;
        SessionId = sessionId;
        Client = client;
        Receiver = receiver;
        QueueName = queueName;
        TopicName = topicName;
        SubscriptionName = subscriptionName;
    }

    public string ConnectionString { get; }
    public string EntityPath { get; }
    public string SessionId { get; }
    public string? QueueName { get; }
    public string? TopicName { get; }
    public string? SubscriptionName { get; }

    public bool IsQueue => !string.IsNullOrEmpty(QueueName);
    public bool IsSubscription => !string.IsNullOrEmpty(TopicName) && !string.IsNullOrEmpty(SubscriptionName);

    internal ServiceBusClient Client { get; }
    internal ServiceBusSessionReceiver Receiver { get; }

    public async ValueTask DisposeAsync()
    {
        await Receiver.DisposeAsync();
        await Client.DisposeAsync();
    }
}
