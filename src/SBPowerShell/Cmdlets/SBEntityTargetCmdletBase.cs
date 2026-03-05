using System.Management.Automation;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

public abstract class SBEntityTargetCmdletBase : SBContextAwareCmdletBase
{
    protected enum ResolvedEntityKind
    {
        Queue,
        Topic,
        Subscription
    }

    protected readonly struct ResolvedEntity
    {
        public ResolvedEntity(ResolvedEntityKind kind, string source, string queue = "", string topic = "", string subscription = "")
        {
            Kind = kind;
            Source = source;
            Queue = queue;
            Topic = topic;
            Subscription = subscription;
        }

        public ResolvedEntityKind Kind { get; }
        public string Source { get; }
        public string Queue { get; }
        public string Topic { get; }
        public string Subscription { get; }

        public string EntityPath => Kind switch
        {
            ResolvedEntityKind.Queue => Queue,
            ResolvedEntityKind.Topic => Topic,
            ResolvedEntityKind.Subscription => $"{Topic}/Subscriptions/{Subscription}",
            _ => string.Empty
        };
    }

    protected ResolvedEntity ResolveQueueTarget(string? explicitQueue, SessionContext? sessionContext = null)
    {
        var resolved = TryResolveOptionalQueueTarget(explicitQueue, sessionContext);
        if (resolved is not null)
        {
            return resolved.Value;
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Queue or -Topic/-Subscription explicitly or set them in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return default;
    }

    protected ResolvedEntity? TryResolveOptionalQueueTarget(string? explicitQueue, SessionContext? sessionContext = null)
    {
        var queue = SBContextValidation.Normalize(explicitQueue);
        if (!string.IsNullOrWhiteSpace(queue))
        {
            WriteVerbose($"Resolved target from Explicit parameters: Queue='{queue}'.");
            WarnOverrideTarget("Queue");
            return new ResolvedEntity(ResolvedEntityKind.Queue, "explicit", queue: queue);
        }

        var sessionQueue = ExtractSessionQueue(sessionContext);
        if (!string.IsNullOrWhiteSpace(sessionQueue))
        {
            WriteVerbose($"Resolved target from SessionContext: Queue='{sessionQueue}'.");
            return new ResolvedEntity(ResolvedEntityKind.Queue, "session", queue: sessionQueue);
        }

        var contextEntity = ResolveFromContext();
        if (contextEntity is not null && contextEntity.Value.Kind == ResolvedEntityKind.Queue)
        {
            WriteVerbose($"Resolved target from SB context: Queue='{contextEntity.Value.Queue}'.");
            return contextEntity.Value;
        }

        return null;
    }

    protected ResolvedEntity? TryResolveOptionalTopicTarget(string? explicitTopic, SessionContext? sessionContext = null, string? resolvedConnectionString = null)
    {
        var topic = SBContextValidation.Normalize(explicitTopic);
        if (!string.IsNullOrWhiteSpace(topic))
        {
            WriteVerbose($"Resolved target from Explicit parameters: Topic='{topic}'.");
            WarnOverrideTarget("Topic");
            return new ResolvedEntity(ResolvedEntityKind.Topic, "explicit", topic: topic);
        }

        var sessionTarget = ResolveFromSession(sessionContext);
        if (sessionTarget is not null && sessionTarget.Value.Kind is ResolvedEntityKind.Topic or ResolvedEntityKind.Subscription)
        {
            WriteVerbose($"Resolved target from SessionContext: Topic='{sessionTarget.Value.Topic}'.");
            return new ResolvedEntity(ResolvedEntityKind.Topic, "session", topic: sessionTarget.Value.Topic);
        }

        var contextTarget = ResolveFromContext();
        if (contextTarget is not null && contextTarget.Value.Kind is ResolvedEntityKind.Topic or ResolvedEntityKind.Subscription)
        {
            WriteVerbose($"Resolved target from SB context: Topic='{contextTarget.Value.Topic}'.");
            return new ResolvedEntity(ResolvedEntityKind.Topic, "context", topic: contextTarget.Value.Topic);
        }

        var entityPath = SBContextValidation.TryGetEntityPathFromConnectionString(resolvedConnectionString);
        if (!string.IsNullOrWhiteSpace(entityPath) &&
            SBContextValidation.TryParseTopicSubscription(entityPath, out var parsedTopic, out _))
        {
            WriteVerbose($"Resolved target from connection string EntityPath: Topic='{parsedTopic}'.");
            return new ResolvedEntity(ResolvedEntityKind.Topic, "connection-string", topic: parsedTopic);
        }

        return null;
    }

    protected ResolvedEntity ResolveTopicTarget(string? explicitTopic, SessionContext? sessionContext = null, string? resolvedConnectionString = null)
    {
        var resolved = TryResolveOptionalTopicTarget(explicitTopic, sessionContext, resolvedConnectionString);
        if (resolved is not null)
        {
            return resolved.Value;
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Topic explicitly or set it in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return default;
    }

    protected ResolvedEntity ResolveSubscriptionTarget(
        string? explicitTopic,
        string? explicitSubscription,
        SessionContext? sessionContext = null,
        bool sessionContextPriority = false,
        string? resolvedConnectionString = null)
    {
        var resolved = ResolveQueueOrSubscriptionTarget(
            explicitQueue: null,
            explicitTopic: explicitTopic,
            explicitSubscription: explicitSubscription,
            sessionContext: sessionContext,
            sessionContextPriority: sessionContextPriority,
            resolvedConnectionString: resolvedConnectionString);

        if (resolved.Kind == ResolvedEntityKind.Subscription)
        {
            return resolved;
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Topic and -Subscription explicitly or set them in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return default;
    }

    protected ResolvedEntity? TryResolveOptionalSubscriptionTarget(
        string? explicitTopic,
        string? explicitSubscription,
        SessionContext? sessionContext = null,
        bool sessionContextPriority = false,
        string? resolvedConnectionString = null)
    {
        var topic = SBContextValidation.Normalize(explicitTopic);
        var subscription = SBContextValidation.Normalize(explicitSubscription);

        if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            ThrowResolverError(
                "InvalidContext",
                "SB context is invalid: Subscription requires Topic.",
                ErrorCategory.InvalidArgument,
                this);
        }

        if (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            WriteVerbose($"Resolved target from Explicit parameters: Topic='{topic}', Subscription='{subscription}'.");
            WarnOverrideTarget("Topic/Subscription");
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "explicit", topic: topic, subscription: subscription);
        }

        var sessionTarget = ResolveFromSession(sessionContext);
        if (sessionContextPriority && sessionTarget is not null && sessionTarget.Value.Kind == ResolvedEntityKind.Subscription)
        {
            WriteVerbose($"Resolved target from SessionContext: Topic='{sessionTarget.Value.Topic}', Subscription='{sessionTarget.Value.Subscription}'.");
            return sessionTarget.Value;
        }

        if (sessionTarget is not null && sessionTarget.Value.Kind == ResolvedEntityKind.Subscription)
        {
            WriteVerbose($"Resolved target from SessionContext: Topic='{sessionTarget.Value.Topic}', Subscription='{sessionTarget.Value.Subscription}'.");
            return sessionTarget.Value;
        }

        var contextTarget = ResolveFromContext();
        if (contextTarget is not null && contextTarget.Value.Kind == ResolvedEntityKind.Subscription)
        {
            WriteVerbose($"Resolved target from SB context: Topic='{contextTarget.Value.Topic}', Subscription='{contextTarget.Value.Subscription}'.");
            return contextTarget.Value;
        }

        var entityPath = SBContextValidation.TryGetEntityPathFromConnectionString(resolvedConnectionString);
        if (!string.IsNullOrWhiteSpace(entityPath) &&
            SBContextValidation.TryParseTopicSubscription(entityPath, out var parsedTopic, out var parsedSubscription))
        {
            WriteVerbose($"Resolved target from connection string EntityPath: Topic='{parsedTopic}', Subscription='{parsedSubscription}'.");
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "connection-string", topic: parsedTopic, subscription: parsedSubscription);
        }

        return null;
    }

    protected ResolvedEntity ResolveQueueOrTopicTarget(string? explicitQueue, string? explicitTopic, SessionContext? sessionContext = null, string? resolvedConnectionString = null)
    {
        var queue = SBContextValidation.Normalize(explicitQueue);
        var topic = SBContextValidation.Normalize(explicitTopic);

        if (!string.IsNullOrWhiteSpace(queue) && !string.IsNullOrWhiteSpace(topic))
        {
            ThrowResolverError(
                "AmbiguousEntity",
                "Target entity is ambiguous. Queue and Topic/Subscription cannot be used together.",
                ErrorCategory.InvalidArgument,
                this);
        }

        if (!string.IsNullOrWhiteSpace(queue))
        {
            WriteVerbose($"Resolved target from Explicit parameters: Queue='{queue}'.");
            WarnOverrideTarget("Queue");
            return new ResolvedEntity(ResolvedEntityKind.Queue, "explicit", queue: queue);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            WriteVerbose($"Resolved target from Explicit parameters: Topic='{topic}'.");
            WarnOverrideTarget("Topic");
            return new ResolvedEntity(ResolvedEntityKind.Topic, "explicit", topic: topic);
        }

        var sessionTarget = ResolveFromSession(sessionContext);
        if (sessionTarget is not null)
        {
            if (sessionTarget.Value.Kind == ResolvedEntityKind.Subscription)
            {
                WriteVerbose($"Resolved target from SessionContext: Topic='{sessionTarget.Value.Topic}'.");
                return new ResolvedEntity(ResolvedEntityKind.Topic, "session", topic: sessionTarget.Value.Topic);
            }

            WriteVerbose($"Resolved target from SessionContext: {sessionTarget.Value.EntityPath}.");
            return sessionTarget.Value;
        }

        var contextTarget = ResolveFromContext();
        if (contextTarget is not null)
        {
            if (contextTarget.Value.Kind == ResolvedEntityKind.Subscription)
            {
                WriteVerbose($"Resolved target from SB context: Topic='{contextTarget.Value.Topic}'.");
                return new ResolvedEntity(ResolvedEntityKind.Topic, "context", topic: contextTarget.Value.Topic);
            }

            WriteVerbose($"Resolved target from SB context: {contextTarget.Value.EntityPath}.");
            return contextTarget.Value;
        }

        var entityPath = SBContextValidation.TryGetEntityPathFromConnectionString(resolvedConnectionString);
        if (!string.IsNullOrWhiteSpace(entityPath))
        {
            if (SBContextValidation.TryParseTopicSubscription(entityPath, out var csTopic, out _))
            {
                WriteVerbose($"Resolved target from connection string EntityPath: Topic='{csTopic}'.");
                return new ResolvedEntity(ResolvedEntityKind.Topic, "connection-string", topic: csTopic);
            }

            WriteVerbose($"Resolved target from connection string EntityPath: Queue='{entityPath}'.");
            return new ResolvedEntity(ResolvedEntityKind.Queue, "connection-string", queue: entityPath);
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Queue or -Topic/-Subscription explicitly or set them in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return default;
    }

    protected ResolvedEntity ResolveQueueOrSubscriptionTarget(
        string? explicitQueue,
        string? explicitTopic,
        string? explicitSubscription,
        SessionContext? sessionContext = null,
        bool sessionContextPriority = false,
        string? resolvedConnectionString = null)
    {
        var queue = SBContextValidation.Normalize(explicitQueue);
        var topic = SBContextValidation.Normalize(explicitTopic);
        var subscription = SBContextValidation.Normalize(explicitSubscription);

        if (!string.IsNullOrWhiteSpace(queue) && (!string.IsNullOrWhiteSpace(topic) || !string.IsNullOrWhiteSpace(subscription)))
        {
            ThrowResolverError(
                "AmbiguousEntity",
                "Target entity is ambiguous. Queue and Topic/Subscription cannot be used together.",
                ErrorCategory.InvalidArgument,
                this);
        }

        if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            ThrowResolverError(
                "InvalidContext",
                "SB context is invalid: Subscription requires Topic.",
                ErrorCategory.InvalidArgument,
                this);
        }

        var explicitTarget = BuildExplicitTarget(queue, topic, subscription);
        var sessionTarget = ResolveFromSession(sessionContext);
        var contextTarget = ResolveFromContext();

        if (sessionContextPriority && sessionTarget is not null)
        {
            if (explicitTarget is not null && !AreSameTarget(explicitTarget.Value, sessionTarget.Value))
            {
                ThrowResolverError(
                    "SessionContextEntityMismatch",
                    "Provided target conflicts with SessionContext entity.",
                    ErrorCategory.InvalidArgument,
                    this);
            }

            WriteVerbose($"Resolved target from SessionContext: {sessionTarget.Value.EntityPath}.");
            return sessionTarget.Value;
        }

        if (explicitTarget is not null)
        {
            WriteVerbose(explicitTarget.Value.Kind == ResolvedEntityKind.Queue
                ? $"Resolved target from Explicit parameters: Queue='{explicitTarget.Value.Queue}'."
                : $"Resolved target from Explicit parameters: Topic='{explicitTarget.Value.Topic}', Subscription='{explicitTarget.Value.Subscription}'.");

            WarnOverrideTarget(explicitTarget.Value.Kind == ResolvedEntityKind.Queue ? "Queue" : "Topic/Subscription");
            return explicitTarget.Value;
        }

        if (sessionTarget is not null)
        {
            WriteVerbose($"Resolved target from SessionContext: {sessionTarget.Value.EntityPath}.");
            return sessionTarget.Value;
        }

        if (contextTarget is not null)
        {
            WriteVerbose(contextTarget.Value.Kind == ResolvedEntityKind.Queue
                ? $"Resolved target from SB context: Queue='{contextTarget.Value.Queue}'."
                : $"Resolved target from SB context: Topic='{contextTarget.Value.Topic}', Subscription='{contextTarget.Value.Subscription}'.");
            return contextTarget.Value;
        }

        var entityPath = SBContextValidation.TryGetEntityPathFromConnectionString(resolvedConnectionString);
        if (!string.IsNullOrWhiteSpace(entityPath))
        {
            if (SBContextValidation.TryParseTopicSubscription(entityPath, out var parsedTopic, out var parsedSubscription))
            {
                WriteVerbose($"Resolved target from connection string EntityPath: Topic='{parsedTopic}', Subscription='{parsedSubscription}'.");
                return new ResolvedEntity(ResolvedEntityKind.Subscription, "connection-string", topic: parsedTopic, subscription: parsedSubscription);
            }

            WriteVerbose($"Resolved target from connection string EntityPath: Queue='{entityPath}'.");
            return new ResolvedEntity(ResolvedEntityKind.Queue, "connection-string", queue: entityPath);
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Queue or -Topic/-Subscription explicitly or set them in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return default;
    }

    protected ResolvedEntity ResolveQueueTopicOrSubscriptionTarget(
        string? explicitQueue,
        string? explicitTopic,
        string? explicitSubscription,
        SessionContext? sessionContext = null,
        bool sessionContextPriority = false,
        string? resolvedConnectionString = null)
    {
        var queue = SBContextValidation.Normalize(explicitQueue);
        var topic = SBContextValidation.Normalize(explicitTopic);
        var subscription = SBContextValidation.Normalize(explicitSubscription);

        if (!string.IsNullOrWhiteSpace(queue) && (!string.IsNullOrWhiteSpace(topic) || !string.IsNullOrWhiteSpace(subscription)))
        {
            ThrowResolverError(
                "AmbiguousEntity",
                "Target entity is ambiguous. Queue and Topic/Subscription cannot be used together.",
                ErrorCategory.InvalidArgument,
                this);
        }

        if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            ThrowResolverError(
                "InvalidContext",
                "SB context is invalid: Subscription requires Topic.",
                ErrorCategory.InvalidArgument,
                this);
        }

        var explicitTarget = BuildExplicitTargetIncludingTopic(queue, topic, subscription);
        var sessionTarget = ResolveFromSession(sessionContext);
        var contextTarget = ResolveFromContext();

        if (sessionContextPriority && sessionTarget is not null)
        {
            if (explicitTarget is not null && !AreSameTarget(explicitTarget.Value, sessionTarget.Value))
            {
                ThrowResolverError(
                    "SessionContextEntityMismatch",
                    "Provided target conflicts with SessionContext entity.",
                    ErrorCategory.InvalidArgument,
                    this);
            }

            WriteVerbose($"Resolved target from SessionContext: {sessionTarget.Value.EntityPath}.");
            return sessionTarget.Value;
        }

        if (explicitTarget is not null)
        {
            WriteVerbose(explicitTarget.Value.Kind switch
            {
                ResolvedEntityKind.Queue => $"Resolved target from Explicit parameters: Queue='{explicitTarget.Value.Queue}'.",
                ResolvedEntityKind.Topic => $"Resolved target from Explicit parameters: Topic='{explicitTarget.Value.Topic}'.",
                _ => $"Resolved target from Explicit parameters: Topic='{explicitTarget.Value.Topic}', Subscription='{explicitTarget.Value.Subscription}'."
            });

            WarnOverrideTarget(explicitTarget.Value.Kind switch
            {
                ResolvedEntityKind.Queue => "Queue",
                ResolvedEntityKind.Topic => "Topic",
                _ => "Topic/Subscription"
            });
            return explicitTarget.Value;
        }

        if (sessionTarget is not null)
        {
            WriteVerbose($"Resolved target from SessionContext: {sessionTarget.Value.EntityPath}.");
            return sessionTarget.Value;
        }

        if (contextTarget is not null)
        {
            WriteVerbose(contextTarget.Value.Kind switch
            {
                ResolvedEntityKind.Queue => $"Resolved target from SB context: Queue='{contextTarget.Value.Queue}'.",
                ResolvedEntityKind.Topic => $"Resolved target from SB context: Topic='{contextTarget.Value.Topic}'.",
                _ => $"Resolved target from SB context: Topic='{contextTarget.Value.Topic}', Subscription='{contextTarget.Value.Subscription}'."
            });
            return contextTarget.Value;
        }

        var entityPath = SBContextValidation.TryGetEntityPathFromConnectionString(resolvedConnectionString);
        if (!string.IsNullOrWhiteSpace(entityPath))
        {
            if (SBContextValidation.TryParseTopicSubscription(entityPath, out var parsedTopic, out var parsedSubscription))
            {
                WriteVerbose($"Resolved target from connection string EntityPath: Topic='{parsedTopic}', Subscription='{parsedSubscription}'.");
                return new ResolvedEntity(ResolvedEntityKind.Subscription, "connection-string", topic: parsedTopic, subscription: parsedSubscription);
            }

            WriteVerbose($"Resolved target from connection string EntityPath: Queue='{entityPath}'.");
            return new ResolvedEntity(ResolvedEntityKind.Queue, "connection-string", queue: entityPath);
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Queue or -Topic/-Subscription explicitly or set them in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return default;
    }

    protected (string Topic, string? Subscription, string Source) ResolveTopicWithOptionalSubscription(
        string? explicitTopic,
        string? explicitSubscription,
        SessionContext? sessionContext = null,
        string? resolvedConnectionString = null)
    {
        var topic = SBContextValidation.Normalize(explicitTopic);
        var subscription = SBContextValidation.Normalize(explicitSubscription);

        if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            var fromContext = ResolveFromContext();
            if (fromContext is not null && fromContext.Value.Kind is ResolvedEntityKind.Subscription or ResolvedEntityKind.Topic)
            {
                topic = fromContext.Value.Topic;
            }
            else
            {
                var fromSession = ResolveFromSession(sessionContext);
                if (fromSession is not null && fromSession.Value.Kind is ResolvedEntityKind.Subscription or ResolvedEntityKind.Topic)
                {
                    topic = fromSession.Value.Topic;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            WriteVerbose(string.IsNullOrWhiteSpace(subscription)
                ? $"Resolved target from Explicit parameters: Topic='{topic}'."
                : $"Resolved target from Explicit parameters: Topic='{topic}', Subscription='{subscription}'.");
            WarnOverrideTarget("Topic");
            return (topic, subscription, "explicit");
        }

        var sessionTarget = ResolveFromSession(sessionContext);
        if (sessionTarget is not null && sessionTarget.Value.Kind is ResolvedEntityKind.Subscription or ResolvedEntityKind.Topic)
        {
            var resolvedSubscription = string.IsNullOrWhiteSpace(subscription)
                ? sessionTarget.Value.Subscription
                : subscription;
            WriteVerbose(string.IsNullOrWhiteSpace(resolvedSubscription)
                ? $"Resolved target from SessionContext: Topic='{sessionTarget.Value.Topic}'."
                : $"Resolved target from SessionContext: Topic='{sessionTarget.Value.Topic}', Subscription='{resolvedSubscription}'.");
            return (sessionTarget.Value.Topic, resolvedSubscription, "session");
        }

        var contextTarget = ResolveFromContext();
        if (contextTarget is not null && contextTarget.Value.Kind is ResolvedEntityKind.Subscription or ResolvedEntityKind.Topic)
        {
            var resolvedSubscription = string.IsNullOrWhiteSpace(subscription)
                ? contextTarget.Value.Subscription
                : subscription;
            WriteVerbose(string.IsNullOrWhiteSpace(resolvedSubscription)
                ? $"Resolved target from SB context: Topic='{contextTarget.Value.Topic}'."
                : $"Resolved target from SB context: Topic='{contextTarget.Value.Topic}', Subscription='{resolvedSubscription}'.");
            return (contextTarget.Value.Topic, resolvedSubscription, "context");
        }

        var entityPath = SBContextValidation.TryGetEntityPathFromConnectionString(resolvedConnectionString);
        if (!string.IsNullOrWhiteSpace(entityPath))
        {
            if (SBContextValidation.TryParseTopicSubscription(entityPath, out var parsedTopic, out var parsedSubscription))
            {
                var resolvedSubscription = string.IsNullOrWhiteSpace(subscription) ? parsedSubscription : subscription;
                WriteVerbose(string.IsNullOrWhiteSpace(resolvedSubscription)
                    ? $"Resolved target from connection string EntityPath: Topic='{parsedTopic}'."
                    : $"Resolved target from connection string EntityPath: Topic='{parsedTopic}', Subscription='{resolvedSubscription}'.");
                return (parsedTopic, resolvedSubscription, "connection-string");
            }
        }

        ThrowResolverError(
            "MissingEntity",
            "Target entity is required. Specify -Queue or -Topic/-Subscription explicitly or set them in SB context.",
            ErrorCategory.InvalidArgument,
            this);

        return (string.Empty, null, string.Empty);
    }

    private void WarnOverrideTarget(string parameterName)
    {
        if (Context is null && NoContext)
        {
            return;
        }

        var currentContext = Context ?? (!NoContext ? GetCurrentContext() : null);
        if (currentContext is not null)
        {
            WriteWarning($"Explicit parameter '{parameterName}' overrides value from SB context.");
        }
    }

    private static bool AreSameTarget(ResolvedEntity left, ResolvedEntity right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            ResolvedEntityKind.Queue => string.Equals(left.Queue, right.Queue, StringComparison.OrdinalIgnoreCase),
            ResolvedEntityKind.Topic => string.Equals(left.Topic, right.Topic, StringComparison.OrdinalIgnoreCase),
            ResolvedEntityKind.Subscription => string.Equals(left.Topic, right.Topic, StringComparison.OrdinalIgnoreCase) &&
                                               string.Equals(left.Subscription, right.Subscription, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static ResolvedEntity? BuildExplicitTarget(string? queue, string? topic, string? subscription)
    {
        if (!string.IsNullOrWhiteSpace(queue))
        {
            return new ResolvedEntity(ResolvedEntityKind.Queue, "explicit", queue: queue);
        }

        if (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "explicit", topic: topic, subscription: subscription);
        }

        return null;
    }

    private static ResolvedEntity? BuildExplicitTargetIncludingTopic(string? queue, string? topic, string? subscription)
    {
        if (!string.IsNullOrWhiteSpace(queue))
        {
            return new ResolvedEntity(ResolvedEntityKind.Queue, "explicit", queue: queue);
        }

        if (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "explicit", topic: topic, subscription: subscription);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            return new ResolvedEntity(ResolvedEntityKind.Topic, "explicit", topic: topic);
        }

        return null;
    }

    private static string? ExtractSessionQueue(SessionContext? sessionContext)
    {
        if (sessionContext is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(sessionContext.QueueName))
        {
            return SBContextValidation.Normalize(sessionContext.QueueName);
        }

        var entityPath = SBContextValidation.Normalize(sessionContext.EntityPath);
        if (string.IsNullOrWhiteSpace(entityPath))
        {
            return null;
        }

        if (SBContextValidation.TryParseTopicSubscription(entityPath, out _, out _))
        {
            return null;
        }

        return entityPath;
    }

    private static ResolvedEntity? ResolveFromSession(SessionContext? sessionContext)
    {
        if (sessionContext is null)
        {
            return null;
        }

        var queue = SBContextValidation.Normalize(sessionContext.QueueName);
        if (!string.IsNullOrWhiteSpace(queue))
        {
            return new ResolvedEntity(ResolvedEntityKind.Queue, "session", queue: queue);
        }

        var topic = SBContextValidation.Normalize(sessionContext.TopicName);
        var subscription = SBContextValidation.Normalize(sessionContext.SubscriptionName);
        if (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "session", topic: topic, subscription: subscription);
        }

        var entityPath = SBContextValidation.Normalize(sessionContext.EntityPath);
        if (string.IsNullOrWhiteSpace(entityPath))
        {
            return null;
        }

        if (SBContextValidation.TryParseTopicSubscription(entityPath, out var parsedTopic, out var parsedSubscription))
        {
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "session", topic: parsedTopic, subscription: parsedSubscription);
        }

        return new ResolvedEntity(ResolvedEntityKind.Queue, "session", queue: entityPath);
    }

    private ResolvedEntity? ResolveFromContext()
    {
        var resolvedContext = Context ?? (!NoContext ? GetCurrentContext() : null);
        if (resolvedContext is null)
        {
            return null;
        }

        EnsureValidContext(resolvedContext);

        var queue = SBContextValidation.Normalize(resolvedContext.Queue);
        if (!string.IsNullOrWhiteSpace(queue))
        {
            return new ResolvedEntity(ResolvedEntityKind.Queue, "context", queue: queue);
        }

        var topic = SBContextValidation.Normalize(resolvedContext.Topic);
        var subscription = SBContextValidation.Normalize(resolvedContext.Subscription);
        if (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription))
        {
            return new ResolvedEntity(ResolvedEntityKind.Subscription, "context", topic: topic, subscription: subscription);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            return new ResolvedEntity(ResolvedEntityKind.Topic, "context", topic: topic);
        }

        return null;
    }
}
