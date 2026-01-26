using System.Management.Automation;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Close, "SBSessionContext")]
public sealed class CloseSBSessionContextCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public SessionContext[] Context { get; set; } = Array.Empty<SessionContext>();

    protected override void EndProcessing()
    {
        foreach (var ctx in Context)
        {
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
