using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

internal static class ServiceBusSubQueuePath
{
    private const string DeadLetterSuffix = "$DeadLetterQueue";
    private const string TransferSuffix = "$Transfer";

    public static string BuildQueueEntityPath(string queue) => queue;

    public static string BuildSubscriptionEntityPath(string topic, string subscription) => $"{topic}/Subscriptions/{subscription}";

    public static string BuildDeadLetterPath(string entityPath) => $"{entityPath}/{DeadLetterSuffix}";

    public static string BuildTransferDeadLetterPath(string entityPath) => $"{entityPath}/{TransferSuffix}/{DeadLetterSuffix}";

    public static string BuildSessionPath(string entityPath, SubQueue subQueue)
    {
        return subQueue switch
        {
            SubQueue.DeadLetter => BuildDeadLetterPath(entityPath),
            SubQueue.TransferDeadLetter => BuildTransferDeadLetterPath(entityPath),
            _ => entityPath
        };
    }

    public static SubQueue ResolveSubQueue(bool transferDeadLetter)
    {
        return transferDeadLetter ? SubQueue.TransferDeadLetter : SubQueue.DeadLetter;
    }
}
