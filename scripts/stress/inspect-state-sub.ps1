param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [Parameter(Mandatory)] [string]$SessionId,
    [string]$InputTopic = "NO_SESSION",
    [string]$InputSubscription = "STATE_SUB",
    [string]$OutputTopic = "ORDERED_TOPIC",
    [string]$OutputSubscription = "SESS_SUB"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." "..")).Path
$module = Join-Path $repoRoot "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = Join-Path $repoRoot "src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}
Import-Module $module -Force

$state = Get-SBSessionState -ServiceBusConnectionString $ConnStr -SessionId $SessionId -Topic $InputTopic -Subscription $InputSubscription -AsString -ErrorAction SilentlyContinue
if ($state) {
    $parsed = $state | ConvertFrom-Json
    $lastSeen = if ($parsed.PSObject.Properties.Name -contains "LastSeenOrder") { $parsed.LastSeenOrder } else { $parsed.LastSeenOrderNum }
    $deferredCount = @($parsed.Deferred).Count
    Write-Host "State: LastSeen=$lastSeen DeferredCount=$deferredCount"
}
else {
    Write-Host "State: <empty or unavailable>"
}

$inputStats = Get-SBSubscription -ServiceBusConnectionString $ConnStr -Topic $InputTopic -Subscription $InputSubscription
$outputStats = Get-SBSubscription -ServiceBusConnectionString $ConnStr -Topic $OutputTopic -Subscription $OutputSubscription

Write-Host "Input runtime:  Active=$($inputStats.RuntimeProperties.ActiveMessageCount) Total=$($inputStats.RuntimeProperties.TotalMessageCount)"
Write-Host "Output runtime: Active=$($outputStats.RuntimeProperties.ActiveMessageCount) Total=$($outputStats.RuntimeProperties.TotalMessageCount)"

$peekOut = @(Receive-SBMessage -ServiceBusConnectionString $ConnStr -Topic $OutputTopic -Subscription $OutputSubscription -Peek -MaxMessages 50)
$forSession = @($peekOut | Where-Object { $_.SessionId -eq $SessionId })
if ($forSession.Count -gt 0) {
    $orders = $forSession | ForEach-Object { [int]$_.ApplicationProperties["order"] }
    $min = ($orders | Measure-Object -Minimum).Minimum
    $max = ($orders | Measure-Object -Maximum).Maximum
    Write-Host "Output peek for session '$SessionId': count=$($forSession.Count) minOrder=$min maxOrder=$max"
}
else {
    Write-Host "Output peek for session '$SessionId': no messages in first 50"
}
