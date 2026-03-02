using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Internal;

internal static class SBContextValidation
{
    public static bool TryValidate(SBContext context, out string error)
    {
        error = string.Empty;

        if (context is null)
        {
            error = "Context is null.";
            return false;
        }

        var queue = Normalize(context.Queue);
        var topic = Normalize(context.Topic);
        var subscription = Normalize(context.Subscription);

        if (!string.IsNullOrEmpty(queue) && !string.IsNullOrEmpty(topic))
        {
            error = "Queue and Topic cannot be used together.";
            return false;
        }

        if (!string.IsNullOrEmpty(subscription) && string.IsNullOrEmpty(topic))
        {
            error = "Subscription requires Topic.";
            return false;
        }

        if (context.EntityMode == SBContextEntityMode.Queue && string.IsNullOrEmpty(queue))
        {
            error = "EntityMode 'Queue' requires Queue.";
            return false;
        }

        if (context.EntityMode == SBContextEntityMode.Subscription &&
            (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(subscription)))
        {
            error = "EntityMode 'Subscription' requires Topic and Subscription.";
            return false;
        }

        return true;
    }

    public static string? TryGetEntityPathFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var props = ServiceBusConnectionStringProperties.Parse(connectionString);
            return Normalize(props.EntityPath);
        }
        catch
        {
            return null;
        }
    }

    public static bool TryParseTopicSubscription(string entityPath, out string topic, out string subscription)
    {
        const string marker = "/subscriptions/";
        topic = string.Empty;
        subscription = string.Empty;

        var normalized = Normalize(entityPath);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return false;
        }

        topic = normalized[..markerIndex].Trim('/');
        subscription = normalized[(markerIndex + marker.Length)..].Trim('/');
        return !string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription);
    }

    public static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('/');
    }
}
