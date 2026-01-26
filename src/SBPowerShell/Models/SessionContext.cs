using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Models;

public sealed class SessionContext : IAsyncDisposable
{
    public SessionContext(string connectionString, string entityPath, string sessionId, ServiceBusClient client, ServiceBusSessionReceiver receiver)
    {
        ConnectionString = connectionString;
        EntityPath = entityPath;
        SessionId = sessionId;
        Client = client;
        Receiver = receiver;
    }

    public string ConnectionString { get; }
    public string EntityPath { get; }
    public string SessionId { get; }

    internal ServiceBusClient Client { get; }
    internal ServiceBusSessionReceiver Receiver { get; }

    public async ValueTask DisposeAsync()
    {
        await Receiver.DisposeAsync();
        await Client.DisposeAsync();
    }
}
