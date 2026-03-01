using System.Security.Cryptography;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

internal sealed class AuthorizationEntityContext
{
    public AuthorizationEntityContext(string entityPath, QueueProperties queue)
    {
        EntityPath = entityPath;
        Queue = queue;
    }

    public AuthorizationEntityContext(string entityPath, TopicProperties topic)
    {
        EntityPath = entityPath;
        Topic = topic;
    }

    public string EntityPath { get; }

    public QueueProperties? Queue { get; }

    public TopicProperties? Topic { get; }

    public AuthorizationRules Rules => Queue is not null ? Queue.AuthorizationRules : Topic!.AuthorizationRules;

    public void Update(ServiceBusAdministrationClient admin)
    {
        if (Queue is not null)
        {
            admin.UpdateQueueAsync(Queue).GetAwaiter().GetResult();
            return;
        }

        admin.UpdateTopicAsync(Topic!).GetAwaiter().GetResult();
    }
}

internal static class AuthorizationRuleHelper
{
    public static AuthorizationEntityContext LoadEntity(ServiceBusAdministrationClient admin, string? queue, string? topic)
    {
        if (!string.IsNullOrWhiteSpace(queue) && !string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Specify only one entity target: Queue or Topic.");
        }

        if (!string.IsNullOrWhiteSpace(queue))
        {
            var queueProps = admin.GetQueueAsync(queue).GetAwaiter().GetResult().Value;
            return new AuthorizationEntityContext(queue, queueProps);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var topicProps = admin.GetTopicAsync(topic).GetAwaiter().GetResult().Value;
            return new AuthorizationEntityContext(topic, topicProps);
        }

        throw new ArgumentException("Specify Queue or Topic.");
    }

    public static SharedAccessAuthorizationRule GetSharedAccessRule(AuthorizationEntityContext context, string ruleName)
    {
        var rule = context.Rules
            .OfType<SharedAccessAuthorizationRule>()
            .FirstOrDefault(r => string.Equals(r.KeyName, ruleName, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
        {
            throw new InvalidOperationException($"Authorization rule '{ruleName}' was not found on entity '{context.EntityPath}'.");
        }

        return rule;
    }

    public static string BuildConnectionString(string baseConnectionString, string entityPath, string keyName, string key)
    {
        var parsed = ServiceBusConnectionStringProperties.Parse(baseConnectionString);
        var endpoint = parsed.Endpoint ?? throw new InvalidOperationException("Connection string does not contain Endpoint.");
        var host = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
        return $"Endpoint=sb://{host};SharedAccessKeyName={keyName};SharedAccessKey={key};EntityPath={entityPath}";
    }

    public static string GenerateSharedAccessKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
