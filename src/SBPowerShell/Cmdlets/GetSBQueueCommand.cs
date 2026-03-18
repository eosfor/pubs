using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBQueue", DefaultParameterSetName = ParameterSetAll)]
[OutputType(typeof(QueueProperties))]
public sealed class GetSBQueueCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetAll = "All";
    private const string ParameterSetByName = "ByName";

    [Parameter(ParameterSetName = ParameterSetByName, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("Name", "QueueName")]
    public string? Queue { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var output = GetQueuesAsync().GetAwaiter().GetResult();
            foreach (var item in output)
            {
                WriteObject(item);
            }
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "GetSBQueueFailed", ErrorCategory.NotSpecified, Queue ?? ServiceBusConnectionString));
        }
    }

    private async Task<IReadOnlyList<object>> GetQueuesAsync()
    {
        var connectionString = ResolveConnectionString();
        var admin = CreateAdminClient(connectionString);
        var results = new List<object>();
        var targetQueue = TryResolveOptionalQueueTarget(Queue)?.Queue;

        if (!string.IsNullOrWhiteSpace(targetQueue))
        {
            var single = await ReadSingleQueueAsync(admin, targetQueue!);
            results.Add(single);
            return results;
        }

        var runtimeMap = await ReadQueueRuntimeMapAsync(admin);

        await foreach (var queue in admin.GetQueuesAsync())
        {
            runtimeMap.TryGetValue(queue.Name, out var runtime);
            results.Add(BuildQueueObject(queue, runtime));
        }

        return results;
    }

    private async Task<object> ReadSingleQueueAsync(ServiceBusAdministrationClient admin, string queueName)
    {
        QueueProperties queue;
        try
        {
            queue = (await admin.GetQueueAsync(queueName)).Value;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "QueueNotFound", ErrorCategory.ObjectNotFound, queueName));
            return new object();
        }

        QueueRuntimeProperties? runtime = null;
        try
        {
            runtime = (await admin.GetQueueRuntimePropertiesAsync(queueName)).Value;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // runtime info not critical; return without it
        }

        return BuildQueueObject(queue, runtime);
    }

    private static async Task<Dictionary<string, QueueRuntimeProperties>> ReadQueueRuntimeMapAsync(ServiceBusAdministrationClient admin)
    {
        var map = new Dictionary<string, QueueRuntimeProperties>(StringComparer.OrdinalIgnoreCase);
        await foreach (var runtime in admin.GetQueuesRuntimePropertiesAsync())
        {
            map[runtime.Name] = runtime;
        }

        return map;
    }

    private static object BuildQueueObject(QueueProperties queue, QueueRuntimeProperties? runtime)
    {
        if (runtime is null)
        {
            return queue;
        }

        var psObj = new PSObject(queue);
        psObj.Properties.Add(new PSNoteProperty("RuntimeProperties", runtime));
        return psObj;
    }
}
