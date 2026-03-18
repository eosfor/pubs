using System.Management.Automation;
using SBPowerShell.Internal;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Clear, "SBContext", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public sealed class ClearSBContextCommand : PSCmdlet
{
    private readonly IContextStore _contextStore = new RunspaceContextStore();

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override void EndProcessing()
    {
        var existing = _contextStore.Get(SessionState);
        if (existing is null)
        {
            if (!Force)
            {
                WriteWarning("No SB context to clear in current runspace.");
            }

            if (PassThru)
            {
                WriteObject(false);
            }

            return;
        }

        if (!ShouldProcess("SBContext", "Clear Service Bus default context"))
        {
            if (PassThru)
            {
                WriteObject(false);
            }

            return;
        }

        var removed = _contextStore.Clear(SessionState);
        if (PassThru)
        {
            WriteObject(removed);
        }
    }
}
