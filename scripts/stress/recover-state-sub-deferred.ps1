param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [Parameter(Mandatory)] [string]$SessionId,
    [string]$Topic = "NO_SESSION",
    [string]$Subscription = "STATE_SUB",
    [string]$SnapDir = "./out",
    [int]$ChunkSize = 200,
    [int]$BatchSize = 100
)

$ErrorActionPreference = "Stop"
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = "src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}
Import-Module $module -Force

New-Item -ItemType Directory -Path $SnapDir -Force | Out-Null
$snapFile = Join-Path $SnapDir "state.$SessionId.before.json"

$ctx = New-SBSessionContext -Topic $Topic -Subscription $Subscription -SessionId $SessionId -ServiceBusConnectionString $ConnStr
try {
    $stateJson = Get-SBSessionState -SessionContext $ctx -AsString
    $stateJson | Set-Content $snapFile -Encoding UTF8
    $state = $stateJson | ConvertFrom-Json

    $seqs = @(
        $state.Deferred | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains "SeqNumber") { [int64]$_.SeqNumber }
            elseif ($_.PSObject.Properties.Name -contains "seq")   { [int64]$_.seq }
            elseif ($_.PSObject.Properties.Name -contains "Seq")   { [int64]$_.Seq }
        }
    ) | Where-Object { $_ -ne $null }

    Write-Host "Deferred sequence count: $($seqs.Count)"

    if ($seqs.Count -gt 0) {
        Receive-SBDeferredMessage -SessionContext $ctx -SequenceNumber $seqs -ChunkSize $ChunkSize |
            Send-SBMessage -SessionContext $ctx -BatchSize $BatchSize -PassThru |
            Set-SBMessage -SessionContext $ctx -Complete | Out-Null
    }

    $lastSeen = if ($state.PSObject.Properties.Name -contains "LastSeenOrder") {
        [int]$state.LastSeenOrder
    }
    else {
        [int]$state.LastSeenOrderNum
    }

    if ($state.PSObject.Properties.Name -contains "LastSeenOrder") {
        $newState = @{ LastSeenOrder = $lastSeen; Deferred = @() }
    }
    else {
        $newState = @{ LastSeenOrderNum = $lastSeen; Deferred = @() }
    }

    Set-SBSessionState -SessionContext $ctx -State ($newState | ConvertTo-Json -Compress)
    Write-Host "Recovery finished for SessionId=$SessionId"
}
finally {
    Close-SBSessionContext -Context $ctx
}
