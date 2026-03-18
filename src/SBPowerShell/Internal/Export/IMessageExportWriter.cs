using SBPowerShell.Models;

namespace SBPowerShell.Internal.Export;

internal interface IMessageExportWriter : IAsyncDisposable
{
    ValueTask WriteAsync(ExportedSbMessage message, CancellationToken cancellationToken);
}
