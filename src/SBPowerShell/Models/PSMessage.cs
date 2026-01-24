using System;
using System.Collections;
namespace SBPowerShell.Models;

/// <summary>
/// PowerShell-friendly representation of a Service Bus message template.
/// Body elements are individual Service Bus messages when sending.
/// </summary>
public sealed class PSMessage
{
    public PSMessage(string? sessionId, IReadOnlyDictionary<string, object> customProperties, IReadOnlyList<string> body)
    {
        SessionId = sessionId;
        CustomProperties = customProperties;
        Body = body;
    }

    public string? SessionId { get; }

    public IReadOnlyDictionary<string, object> CustomProperties { get; }

    public IReadOnlyList<string> Body { get; }

    public static IReadOnlyDictionary<string, object> FromHashtable(Hashtable? table)
    {
        if (table is null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var dict = new Dictionary<string, object>(table.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in table)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("CustomProperties contains an empty key.");
            }

            if (entry.Value is null)
            {
                throw new ArgumentException($"CustomProperties[{key}] is null. Application properties must have non-null values.");
            }

            dict[key] = entry.Value;
        }

        return dict;
    }
}
