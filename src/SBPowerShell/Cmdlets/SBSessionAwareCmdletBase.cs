using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

public abstract class SBSessionAwareCmdletBase : SBEntityTargetCmdletBase
{
    protected void EnsureSessionContextTargetMatchesExplicit(
        SessionContext? sessionContext,
        string? explicitQueue,
        string? explicitTopic,
        string? explicitSubscription)
    {
        if (sessionContext is null)
        {
            return;
        }

        ResolveQueueOrSubscriptionTarget(
            explicitQueue,
            explicitTopic,
            explicitSubscription,
            sessionContext,
            sessionContextPriority: true);
    }
}
