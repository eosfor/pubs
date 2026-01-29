using System.Collections;
using System.Management.Automation;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBSessionState")]
[OutputType(typeof(SessionOrderingState))]
public sealed class NewSBSessionStateCommand : PSCmdlet
{
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int LastSeenOrderNum { get; set; }

    /// <summary>
    /// Deferred entries. Accepts:
    /// - hashtable/PSObject with keys 'order' and 'seq'
    /// - two-element array [order, seq]
    /// </summary>
    [Parameter]
    public object[]? Deferred { get; set; }

    protected override void EndProcessing()
    {
        var state = new SessionOrderingState
        {
            LastSeenOrderNum = LastSeenOrderNum
        };

        foreach (var item in Deferred ?? Array.Empty<object>())
        {
            var parsed = ParseDeferred(item);
            if (parsed is not null)
            {
                state.Deferred.Add(parsed);
            }
            else
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Deferred entries must be hashtable/object with 'order' and 'seq' or two-element array."),
                    "InvalidDeferredEntry",
                    ErrorCategory.InvalidData,
                    item));
            }
        }

        WriteObject(state);
    }

    private static OrderSeq? ParseDeferred(object item)
    {
        item = item is PSObject ps ? ps.BaseObject : item;

        if (item is IDictionary dict &&
            dict.Contains("order") &&
            dict.Contains("seq"))
        {
            return new OrderSeq(Convert.ToInt32(dict["order"]), Convert.ToInt64(dict["seq"]));
        }

        if (item is object[] arr && arr.Length >= 2)
        {
            return new OrderSeq(Convert.ToInt32(arr[0]), Convert.ToInt64(arr[1]));
        }

        return null;
    }
}
