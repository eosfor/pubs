using System.Text;
using System.Text.Json;
using SBPowerShell.Models;

namespace SBPowerShell.Internal.Export;

internal sealed class JsonlMessageExportWriter : IMessageExportWriter
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonlMessageExportWriter(string path, bool append)
    {
        var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
        _serializerOptions = CreateSerializerOptions();
    }

    public async ValueTask WriteAsync(ExportedSbMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, _serializerOptions);
        await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = false
        };
    }
}
