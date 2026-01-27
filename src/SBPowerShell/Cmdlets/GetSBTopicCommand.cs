using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBTopic", DefaultParameterSetName = ParameterSetAll)]
[OutputType(typeof(TopicProperties))]
public sealed class GetSBTopicCommand : PSCmdlet
{
    private const string ParameterSetAll = "All";
    private const string ParameterSetByName = "ByName";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetByName, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("Name", "TopicName")]
    public string? Topic { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var output = GetTopicsAsync().GetAwaiter().GetResult();
            foreach (var item in output)
            {
                WriteObject(item);
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "GetSBTopicFailed", ErrorCategory.NotSpecified, Topic ?? ServiceBusConnectionString));
        }
    }

    private async Task<IReadOnlyList<object>> GetTopicsAsync()
    {
        var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
        var results = new List<object>();

        if (ParameterSetName == ParameterSetByName && !string.IsNullOrWhiteSpace(Topic))
        {
            var single = await ReadSingleTopicAsync(admin, Topic!);
            results.Add(single);
            return results;
        }

        var runtimeMap = await ReadTopicRuntimeMapAsync(admin);

        await foreach (var topic in admin.GetTopicsAsync())
        {
            runtimeMap.TryGetValue(topic.Name, out var runtime);
            results.Add(BuildTopicObject(topic, runtime));
        }
        return results;
    }

    private async Task<object> ReadSingleTopicAsync(ServiceBusAdministrationClient admin, string topicName)
    {
        TopicProperties topic;
        try
        {
            topic = (await admin.GetTopicAsync(topicName)).Value;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "TopicNotFound", ErrorCategory.ObjectNotFound, topicName));
            return new object();
        }

        TopicRuntimeProperties? runtime = null;
        try
        {
            runtime = (await admin.GetTopicRuntimePropertiesAsync(topicName)).Value;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // runtime info not critical; return without it
        }

        return BuildTopicObject(topic, runtime);
    }

    private static async Task<Dictionary<string, TopicRuntimeProperties>> ReadTopicRuntimeMapAsync(ServiceBusAdministrationClient admin)
    {
        var map = new Dictionary<string, TopicRuntimeProperties>(StringComparer.OrdinalIgnoreCase);
        await foreach (var runtime in admin.GetTopicsRuntimePropertiesAsync())
        {
            map[runtime.Name] = runtime;
        }

        return map;
    }

    private static object BuildTopicObject(TopicProperties topic, TopicRuntimeProperties? runtime)
    {
        if (runtime is null)
        {
            return topic;
        }

        var psObj = new PSObject(topic);
        psObj.Properties.Add(new PSNoteProperty("RuntimeProperties", runtime));
        return psObj;
    }
}
