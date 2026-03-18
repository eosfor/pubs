using System.Text;
using System.Text.Json;
using SBPowerShell.Models;

namespace SBPowerShell.Internal.Export;

internal sealed class JsonArrayMessageExportWriter : IMessageExportWriter
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _serializerOptions;
    private bool _hasWrittenItem;

    public JsonArrayMessageExportWriter(string path)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
        _serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        _writer.Write('[');
    }

    public async ValueTask WriteAsync(ExportedSbMessage message, CancellationToken cancellationToken)
    {
        if (_hasWrittenItem)
        {
            await _writer.WriteAsync(",".AsMemory(), cancellationToken);
        }

        await _writer.WriteLineAsync();
        var json = JsonSerializer.Serialize(message, _serializerOptions);
        await _writer.WriteAsync(json.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
        _hasWrittenItem = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hasWrittenItem)
        {
            await _writer.WriteLineAsync();
        }

        await _writer.WriteAsync("]");
        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }
}
