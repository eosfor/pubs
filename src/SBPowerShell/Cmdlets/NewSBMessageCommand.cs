using System.Collections;
using System.Management.Automation;
using SBPowerShell.Models;
using System.Linq;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBMessage", DefaultParameterSetName = ParameterSetByParts)]
[OutputType(typeof(PSMessage))]
public sealed class NewSBMessageCommand : PSCmdlet
{
    private const string ParameterSetByParts = "ByParts";
    private const string ParameterSetByHashTable = "ByHashTable";
    private const string ParameterSetFromPipeline = "FromPipeline";

    private readonly List<Hashtable> _pipelineBuffer = new();

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetByParts)]
    [ValidateNotNullOrEmpty]
    public string[] Body { get; set; } = Array.Empty<string>();

    [Parameter(ParameterSetName = ParameterSetByParts)]
    public string? SessionId { get; set; }

    [Parameter(ParameterSetName = ParameterSetByParts)]
    public Hashtable[]? CustomProperties { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetByHashTable)]
    public Hashtable[]? HashTable { get; set; }

    [Parameter(ParameterSetName = ParameterSetByHashTable)]
    [Parameter(ParameterSetName = ParameterSetFromPipeline)]
    public SwitchParameter NeedSessionId { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetFromPipeline)]
    public Hashtable? InputObject { get; set; }

    protected override void ProcessRecord()
    {
        switch (ParameterSetName)
        {
            case ParameterSetByParts:
                EmitByParts();
                break;

            case ParameterSetByHashTable:
                EmitFromHashtables(HashTable ?? Array.Empty<Hashtable>(), NeedSessionId);
                break;

            case ParameterSetFromPipeline:
                if (InputObject != null)
                {
                    _pipelineBuffer.Add(InputObject);
                }
                break;
        }
    }

    protected override void EndProcessing()
    {
        if (ParameterSetName == ParameterSetFromPipeline)
        {
            EmitFromHashtables(_pipelineBuffer, NeedSessionId);
        }
    }

    private void EmitByParts()
    {
        var bodies = Body ?? Array.Empty<string>();
        var properties = CustomProperties ?? Array.Empty<Hashtable>();

        if (bodies.Length == 0)
        {
            ThrowTerminatingError(BuildError("NewSBMessageEmptyBody", "Parameter -Body must contain at least one string.", ErrorCategory.InvalidArgument, Body));
        }

        List<Hashtable?> mapping;
        if (properties.Length == 0)
        {
            mapping = Enumerable.Repeat<Hashtable?>(null, bodies.Length).ToList();
        }
        else if (properties.Length == 1)
        {
            mapping = Enumerable.Repeat(properties[0], bodies.Length).Cast<Hashtable?>().ToList();
        }
        else if (properties.Length == bodies.Length)
        {
            mapping = properties.Cast<Hashtable?>().ToList();
        }
        else
        {
            ThrowTerminatingError(BuildError(
                "NewSBMessagePropertyCountMismatch",
                $"CustomProperties count ({properties.Length}) must be 0, 1 or equal to Body count ({bodies.Length}).",
                ErrorCategory.InvalidData,
                CustomProperties));
            return;
        }

        for (var i = 0; i < bodies.Length; i++)
        {
            var dict = PSMessage.FromHashtable(mapping[i]);
            var msg = new PSMessage(SessionId, dict, new[] { bodies[i] });
            WriteObject(msg);
        }
    }

    private void EmitFromHashtables(IEnumerable<Hashtable> tables, bool requireSessionId)
    {
        var messages = new List<PSMessage>();

        foreach (var table in tables)
        {
            try
            {
                messages.Add(BuildFromHashtable(table));
            }
            catch (ArgumentException ex)
            {
                ThrowTerminatingError(BuildError("NewSBMessageInvalidHashtable", ex.Message, ErrorCategory.InvalidData, table));
                return;
            }
        }

        ValidateSessionConsistency(messages, requireSessionId);

        foreach (var message in messages)
        {
            WriteObject(message);
        }
    }

    private static PSMessage BuildFromHashtable(Hashtable table)
    {
        if (table is null)
        {
            throw new ArgumentException("Hashtable is null.");
        }

        var sessionId = TryGetCaseInsensitive<string>(table, "sessionId");
        var bodyValue = TryGetCaseInsensitive<object>(table, "body");
        if (bodyValue is null)
        {
            throw new ArgumentException("Hashtable must contain key 'body' with string value.");
        }

        if (bodyValue is not string body)
        {
            throw new ArgumentException("Value for 'body' must be a string.");
        }

        var customPropsValue = TryGetCaseInsensitive<object>(table, "customProperties");
        Hashtable? customHashtable = null;
        if (customPropsValue != null)
        {
            if (customPropsValue is Hashtable ht)
            {
                customHashtable = ht;
            }
            else
            {
                throw new ArgumentException("Value for 'customProperties' must be a Hashtable when provided.");
            }
        }

        var dict = PSMessage.FromHashtable(customHashtable);
        return new PSMessage(sessionId, dict, new[] { body });
    }

    private void ValidateSessionConsistency(IReadOnlyCollection<PSMessage> messages, bool requireSessionId)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var anyWith = messages.Any(m => !string.IsNullOrEmpty(m.SessionId));
        var anyWithout = messages.Any(m => string.IsNullOrEmpty(m.SessionId));

        if (requireSessionId && anyWithout)
        {
            ThrowTerminatingError(BuildError(
                "NewSBMessageMissingSessionId",
                "All messages must contain sessionId when -NeedSessionId is specified.",
                ErrorCategory.InvalidData,
                null));
        }

        if (anyWith && anyWithout)
        {
            ThrowTerminatingError(BuildError(
                "NewSBMessageMixedSessionId",
                "Input contains a mixture of messages with and without sessionId. Provide sessionId for all or none.",
                ErrorCategory.InvalidData,
                null));
        }
    }

    private static T? TryGetCaseInsensitive<T>(Hashtable table, string key)
    {
        foreach (DictionaryEntry entry in table)
        {
            if (entry.Key is null)
            {
                continue;
            }

            if (string.Equals(entry.Key.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                return (T?)entry.Value;
            }
        }

        return default;
    }

    private ErrorRecord BuildError(string id, string message, ErrorCategory category, object? target)
    {
        return new ErrorRecord(new ArgumentException(message), id, category, target);
    }
}
