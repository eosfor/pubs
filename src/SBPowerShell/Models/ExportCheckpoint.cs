namespace SBPowerShell.Models;

public sealed class ExportCheckpoint
{
    public string EntityKind { get; init; } = string.Empty;

    public string EntityPath { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public string OutputPath { get; init; } = string.Empty;

    public long LastSequenceNumber { get; init; }

    public int ExportedCount { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
