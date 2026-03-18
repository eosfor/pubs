using System.Management.Automation;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Internal.Export;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsData.Export, "SBDLQMessage", DefaultParameterSetName = ParameterSetContext)]
[OutputType(typeof(FileInfo))]
public sealed class ExportSBDLQMessageCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";
    private const int PageSize = 1000;

    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    public SBExportFormat? Format { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateRange(1, int.MaxValue)]
    public int? MaxMessages { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateRange(0, long.MaxValue)]
    public long? FromSequenceNumber { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string? CheckpointPath { get; set; }

    protected override void EndProcessing()
    {
        try
        {
            var output = ExportAsync(_cts.Token).GetAwaiter().GetResult();
            WriteObject(output);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ExportSBDLQMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }

    private async Task<FileInfo> ExportAsync(CancellationToken cancellationToken)
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveQueueOrSubscriptionTarget(
            Queue,
            Topic,
            Subscription,
            resolvedConnectionString: connectionString);

        var format = ResolveFormat(OutputPath, Format);
        ValidateCheckpointUsage(format);

        var absoluteOutputPath = ResolvePowerShellPath(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath) ?? ".");

        var checkpointStore = string.IsNullOrWhiteSpace(CheckpointPath)
            ? null
            : new ExportCheckpointStore(ResolvePowerShellPath(CheckpointPath));

        var checkpoint = checkpointStore?.TryLoad();
        ValidateCheckpoint(target, format, absoluteOutputPath, checkpoint);

        var startSequence = checkpoint is not null
            ? checkpoint.LastSequenceNumber + 1
            : FromSequenceNumber ?? 0L;

        var exportedCount = checkpoint?.ExportedCount ?? 0;
        var startedAtUtc = checkpoint?.StartedAtUtc ?? DateTimeOffset.UtcNow;
        var append = checkpoint is not null;

        await using var writer = CreateWriter(format, absoluteOutputPath, append);
        await using var client = CreateServiceBusClient(connectionString);
        await using var receiver = target.Kind == ResolvedEntityKind.Queue
            ? client.CreateReceiver(target.Queue, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter })
            : client.CreateReceiver(target.Topic, target.Subscription, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        long nextSequence = startSequence;

        while (!cancellationToken.IsCancellationRequested && (!MaxMessages.HasValue || exportedCount < MaxMessages.Value))
        {
            var take = MaxMessages.HasValue
                ? Math.Min(PageSize, MaxMessages.Value - exportedCount)
                : PageSize;

            if (take <= 0)
            {
                break;
            }

            var messages = await receiver.PeekMessagesAsync(take, nextSequence, cancellationToken);
            if (messages.Count == 0)
            {
                break;
            }

            long pageLastSequence = nextSequence - 1;
            foreach (var message in messages)
            {
                var exported = ServiceBusMessageExportMapper.Map(message);
                await writer.WriteAsync(exported, cancellationToken);
                exportedCount++;
                pageLastSequence = Math.Max(pageLastSequence, message.SequenceNumber);
            }

            nextSequence = pageLastSequence + 1;

            if (checkpointStore is not null)
            {
                checkpointStore.Save(new ExportCheckpoint
                {
                    EntityKind = target.Kind.ToString(),
                    EntityPath = ServiceBusSubQueuePath.BuildDeadLetterPath(target.EntityPath),
                    Format = format.ToString(),
                    OutputPath = absoluteOutputPath,
                    LastSequenceNumber = pageLastSequence,
                    ExportedCount = exportedCount,
                    StartedAtUtc = startedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        return new FileInfo(absoluteOutputPath);
    }

    private void ValidateCheckpointUsage(SBExportFormat format)
    {
        if (!string.IsNullOrWhiteSpace(CheckpointPath) && format != SBExportFormat.Jsonl)
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("-CheckpointPath is only supported with Jsonl exports."),
                "ExportSBDLQMessageCheckpointRequiresJsonl",
                ErrorCategory.InvalidArgument,
                this));
        }
    }

    private void ValidateCheckpoint(ResolvedEntity target, SBExportFormat format, string outputPath, ExportCheckpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            return;
        }

        if (!File.Exists(outputPath))
        {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Checkpoint exists, but the export output file does not exist."),
                "ExportSBDLQMessageMissingOutputForCheckpoint",
                ErrorCategory.InvalidData,
                outputPath));
        }

        var expectedEntityPath = ServiceBusSubQueuePath.BuildDeadLetterPath(target.EntityPath);

        if (!string.Equals(checkpoint.EntityKind, target.Kind.ToString(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(checkpoint.EntityPath, expectedEntityPath, StringComparison.OrdinalIgnoreCase))
        {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Checkpoint target does not match the requested DLQ entity."),
                "ExportSBDLQMessageCheckpointTargetMismatch",
                ErrorCategory.InvalidData,
                checkpoint));
        }

        if (!string.Equals(checkpoint.Format, format.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Checkpoint format does not match the requested export format."),
                "ExportSBDLQMessageCheckpointFormatMismatch",
                ErrorCategory.InvalidData,
                checkpoint));
        }

        if (!string.Equals(Path.GetFullPath(checkpoint.OutputPath), outputPath, StringComparison.OrdinalIgnoreCase))
        {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Checkpoint output path does not match the requested output path."),
                "ExportSBDLQMessageCheckpointOutputMismatch",
                ErrorCategory.InvalidData,
                checkpoint));
        }
    }

    private static SBExportFormat ResolveFormat(string outputPath, SBExportFormat? explicitFormat)
    {
        if (explicitFormat.HasValue)
        {
            return explicitFormat.Value;
        }

        var extension = Path.GetExtension(outputPath);
        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? SBExportFormat.Json
            : SBExportFormat.Jsonl;
    }

    private static IMessageExportWriter CreateWriter(SBExportFormat format, string path, bool append)
    {
        return format switch
        {
            SBExportFormat.Json => new JsonArrayMessageExportWriter(path),
            _ => new JsonlMessageExportWriter(path, append)
        };
    }

    private string ResolvePowerShellPath(string path)
    {
        try
        {
            return SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);
        }
        catch (PSNotSupportedException ex)
        {
            throw new ArgumentException("Output path must resolve to a filesystem path.", nameof(path), ex);
        }
        catch (System.Management.Automation.DriveNotFoundException ex)
        {
            throw new ArgumentException($"Path '{path}' could not be resolved from the current PowerShell location.", nameof(path), ex);
        }
        catch (ProviderNotFoundException ex)
        {
            throw new ArgumentException("Only filesystem paths are supported for export output and checkpoints.", nameof(path), ex);
        }
        catch (ItemNotFoundException ex)
        {
            throw new ArgumentException($"Path '{path}' could not be resolved from the current PowerShell location.", nameof(path), ex);
        }
    }
}
