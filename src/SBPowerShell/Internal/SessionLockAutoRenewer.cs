using System.Runtime.ExceptionServices;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Internal;

internal sealed class SessionLockAutoRenewer : IDisposable
{
    private static readonly TimeSpan DefaultRenewAhead = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(1);

    private readonly ServiceBusSessionReceiver _receiver;
    private readonly TimeSpan _renewAhead;
    private readonly CancellationTokenSource _cts;
    private readonly Task _loopTask;
    private Exception? _fault;

    private SessionLockAutoRenewer(ServiceBusSessionReceiver receiver, CancellationToken cancellationToken, TimeSpan? renewAhead)
    {
        _receiver = receiver;
        _renewAhead = renewAhead.GetValueOrDefault(DefaultRenewAhead);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(RunAsync, CancellationToken.None);
    }

    public static SessionLockAutoRenewer? Start(ServiceBusReceiver receiver, CancellationToken cancellationToken, TimeSpan? renewAhead = null)
    {
        return receiver is ServiceBusSessionReceiver sessionReceiver
            ? new SessionLockAutoRenewer(sessionReceiver, cancellationToken, renewAhead)
            : null;
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _loopTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            _cts.Dispose();
        }

        if (_fault is not null)
        {
            ExceptionDispatchInfo.Capture(_fault).Throw();
        }
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var nextDelay = ComputeDelay(_receiver.SessionLockedUntil);
                await Task.Delay(nextDelay, _cts.Token).ConfigureAwait(false);
                await _receiver.RenewSessionLockAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.SessionLockLost)
            {
                _fault = ex;
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                // Preserve the first renewal error to surface it to the caller.
                _fault ??= ex;
                _cts.Cancel();
            }
        }
    }

    private TimeSpan ComputeDelay(DateTimeOffset lockedUntil)
    {
        if (lockedUntil == default)
        {
            return MinDelay;
        }

        var delay = lockedUntil - DateTimeOffset.UtcNow - _renewAhead;
        return delay > MinDelay ? delay : MinDelay;
    }
}
