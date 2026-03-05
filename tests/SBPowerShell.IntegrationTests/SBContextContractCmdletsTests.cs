using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Xunit;

namespace SBPowerShell.IntegrationTests;

[Collection("SBPowerShellIntegration")]
public sealed class SBContextContractCmdletsTests : SBCommandTestBase
{
    private static readonly string[] NonResolverCmdlets =
    [
        "New-SBMessage",
        "New-SBSessionState",
        "Close-SBSessionContext",
        "Set-SBContext",
        "Get-SBContext",
        "Clear-SBContext"
    ];

    private static readonly string[] ContextAwareCmdlets =
    [
        "Send-SBMessage",
        "Receive-SBMessage",
        "Receive-SBDLQMessage",
        "Receive-SBDeferredMessage",
        "Set-SBMessage",
        "Get-SBSession",
        "Get-SBSessionState",
        "Set-SBSessionState",
        "New-SBSessionContext",
        "Clear-SBQueue",
        "Clear-SBSubscription",
        "Get-SBTopic",
        "Get-SBSubscription",
        "Get-SBQueue",
        "New-SBQueue",
        "Set-SBQueue",
        "Remove-SBQueue",
        "New-SBTopic",
        "Set-SBTopic",
        "Remove-SBTopic",
        "New-SBSubscription",
        "Set-SBSubscription",
        "Remove-SBSubscription",
        "Get-SBRule",
        "New-SBRule",
        "Set-SBRule",
        "Remove-SBRule",
        "Send-SBScheduledMessage",
        "Remove-SBScheduledMessage",
        "Clear-SBDLQ",
        "Replay-SBDLQMessage",
        "Receive-SBTransferDLQMessage",
        "Get-SBAuthorizationRule",
        "New-SBAuthorizationRule",
        "Set-SBAuthorizationRule",
        "Remove-SBAuthorizationRule",
        "Rotate-SBKey",
        "Get-SBConnectionString",
        "Set-SBEntityStatus",
        "Export-SBTopology",
        "Import-SBTopology"
    ];

    public SBContextContractCmdletsTests(ServiceBusFixture fixture)
        : base(fixture)
    {
    }

    public static IEnumerable<object[]> ContextAwareCmdletNames()
    {
        return ContextAwareCmdlets.Select(name => new object[] { name });
    }

    [Theory]
    [MemberData(nameof(ContextAwareCmdletNames))]
    public void Context_aware_cmdlet_exposes_common_resolver_parameters(string cmdletName)
    {
        using var ps = _fixture.CreateShell();
        var info = ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand(cmdletName, CommandTypes.Cmdlet) as CmdletInfo;

        Assert.NotNull(info);
        Assert.True(info!.Parameters.ContainsKey("ServiceBusConnectionString"), $"{cmdletName} must expose ServiceBusConnectionString.");
        Assert.True(info.Parameters.ContainsKey("Context"), $"{cmdletName} must expose Context.");
        Assert.True(info.Parameters.ContainsKey("NoContext"), $"{cmdletName} must expose NoContext.");
        Assert.True(info.Parameters.ContainsKey("IgnoreCertificateChainErrors"), $"{cmdletName} must expose IgnoreCertificateChainErrors.");
    }

    [Theory]
    [MemberData(nameof(ContextAwareCmdletNames))]
    public void Context_aware_cmdlet_does_not_require_connection_string_in_any_parameter_set(string cmdletName)
    {
        using var ps = _fixture.CreateShell();
        var info = ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand(cmdletName, CommandTypes.Cmdlet) as CmdletInfo;

        Assert.NotNull(info);
        Assert.NotEmpty(info!.ParameterSets);

        foreach (var parameterSet in info.ParameterSets)
        {
            var parameter = parameterSet.Parameters
                .FirstOrDefault(p => string.Equals(p.Name, "ServiceBusConnectionString", StringComparison.OrdinalIgnoreCase));

            if (parameter is null)
            {
                continue;
            }

            Assert.False(parameter.IsMandatory, $"{cmdletName} set '{parameterSet.Name}' still requires ServiceBusConnectionString.");
        }
    }

    [Fact]
    public void Context_contract_list_covers_all_exported_resolver_cmdlets()
    {
        using var ps = _fixture.CreateShell();
        var exported = ps.Runspace.SessionStateProxy.InvokeCommand
            .GetCommands("*", CommandTypes.Cmdlet, true)
            .OfType<CmdletInfo>()
            .Where(c => string.Equals(c.ModuleName, "pubs", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .Where(name => !NonResolverCmdlets.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var contractList = ContextAwareCmdlets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(exported, contractList);
    }
}
