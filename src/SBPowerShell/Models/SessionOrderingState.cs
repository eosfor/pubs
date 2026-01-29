using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;

namespace SBPowerShell.Models;

public sealed record OrderSeq(int Order, long Seq);

public sealed class SessionOrderingState
{
    public int LastSeenOrderNum { get; set; }

    public List<OrderSeq> Deferred { get; set; } = new();
}

public static class SessionOrderingStateSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static BinaryData Serialize(SessionOrderingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new BinaryData(JsonSerializer.SerializeToUtf8Bytes(state, Options));
    }

    public static SessionOrderingState? Deserialize(BinaryData? data)
    {
        if (data is null || data.ToMemory().Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SessionOrderingState>(data, Options);
        }
        catch
        {
            // fall through
        }

        try
        {
            using var doc = JsonDocument.Parse(data);
            return FromJsonDocument(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static SessionOrderingState? FromJsonDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var state = new SessionOrderingState
        {
            LastSeenOrderNum = root.TryGetProperty("lastSeenOrderNum", out var last)
                ? last.GetInt32()
                : root.TryGetProperty("LastSeenOrderNum", out var lastPascal)
                    ? lastPascal.GetInt32()
                    : 0
        };

        if (root.TryGetProperty("deferred", out var deferredEl) && deferredEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in deferredEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() >= 2)
                {
                    state.Deferred.Add(new OrderSeq(item[0].GetInt32(), item[1].GetInt64()));
                }
                else if (item.ValueKind == JsonValueKind.Object &&
                         item.TryGetProperty("order", out var orderEl) &&
                         item.TryGetProperty("seq", out var seqEl))
                {
                    state.Deferred.Add(new OrderSeq(orderEl.GetInt32(), seqEl.GetInt64()));
                }
            }
        }

        return state;
    }
}
