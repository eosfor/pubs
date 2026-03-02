using System.Management.Automation;
using SBPowerShell.Models;

namespace SBPowerShell.Internal;

internal interface IContextStore
{
    SBContext? Get(SessionState sessionState);

    void Set(SessionState sessionState, SBContext context);

    bool Clear(SessionState sessionState);
}
