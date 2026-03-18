using System.Management.Automation;
using SBPowerShell.Models;

namespace SBPowerShell.Internal;

internal sealed class RunspaceContextStore : IContextStore
{
    private const string VariableName = "SBContext";

    public SBContext? Get(SessionState sessionState)
    {
        return sessionState.PSVariable.GetValue(VariableName) as SBContext;
    }

    public void Set(SessionState sessionState, SBContext context)
    {
        sessionState.PSVariable.Set(VariableName, context);
    }

    public bool Clear(SessionState sessionState)
    {
        if (sessionState.PSVariable.Get(VariableName) is null)
        {
            return false;
        }

        sessionState.PSVariable.Remove(VariableName);
        return true;
    }
}
