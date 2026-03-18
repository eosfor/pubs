using System.Text.Json;
using SBPowerShell.Models;

namespace SBPowerShell.Internal.Export;

internal sealed class ExportCheckpointStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public ExportCheckpointStore(string path)
    {
        _path = path;
    }

    public ExportCheckpoint? TryLoad()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<ExportCheckpoint>(json, _serializerOptions);
    }

    public void Save(ExportCheckpoint checkpoint)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(checkpoint, _serializerOptions);
        File.WriteAllText(_path, json);
    }
}
