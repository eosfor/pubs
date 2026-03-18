using System.Text;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Internal;

internal static class ServiceBusMessageExportMapper
{
    private const string DeadLetterReasonKey = "DeadLetterReason";
    private const string DeadLetterErrorDescriptionKey = "DeadLetterErrorDescription";

    public static ExportedSbMessage Map(ServiceBusReceivedMessage message)
    {
        var bodyBytes = message.Body.ToArray();
        var utf8 = TryGetUtf8(bodyBytes);
        var applicationProperties = new Dictionary<string, object?>(message.ApplicationProperties.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in message.ApplicationProperties)
        {
            applicationProperties[pair.Key] = JsonSafeValueConverter.Convert(pair.Value);
        }

        applicationProperties.TryGetValue(DeadLetterReasonKey, out var deadLetterReason);
        applicationProperties.TryGetValue(DeadLetterErrorDescriptionKey, out var deadLetterErrorDescription);

        return new ExportedSbMessage
        {
            BrokerProperties = new ExportedBrokerProperties
            {
                SequenceNumber = message.SequenceNumber,
                EnqueuedSequenceNumber = message.EnqueuedSequenceNumber,
                EnqueuedTimeUtc = message.EnqueuedTime,
                ScheduledEnqueueTimeUtc = message.ScheduledEnqueueTime,
                ExpiresAtUtc = message.ExpiresAt,
                State = message.State.ToString(),
                DeliveryCount = message.DeliveryCount,
                LockedUntilUtc = message.LockedUntil,
                LockToken = message.LockToken,
                DeadLetterSource = message.DeadLetterSource,
                DeadLetterReason = deadLetterReason?.ToString(),
                DeadLetterErrorDescription = deadLetterErrorDescription?.ToString()
            },
            MessageProperties = new ExportedMessageProperties
            {
                MessageId = message.MessageId,
                SessionId = message.SessionId,
                ReplyToSessionId = message.ReplyToSessionId,
                CorrelationId = message.CorrelationId,
                Subject = message.Subject,
                ContentType = message.ContentType,
                To = message.To,
                ReplyTo = message.ReplyTo,
                PartitionKey = message.PartitionKey,
                TransactionPartitionKey = message.TransactionPartitionKey,
                ViaPartitionKey = null,
                TimeToLive = message.TimeToLive.ToString()
            },
            ApplicationProperties = applicationProperties,
            Body = new ExportedMessageBody
            {
                Length = bodyBytes.Length,
                Base64 = Convert.ToBase64String(bodyBytes),
                Utf8 = utf8
            }
        };
    }

    private static string? TryGetUtf8(byte[] bodyBytes)
    {
        if (bodyBytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(bodyBytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
