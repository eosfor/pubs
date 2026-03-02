param(
    [string]$ModuleManifestPath = "./src/SBPowerShell/bin/Debug/net8.0/pubs.psd1",
    [string]$OutputFolder = "./docs/help",
    [string]$ExternalHelpOutputPath = "./src/SBPowerShell/bin/Debug/net8.0/en-US"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:SynopsisOverrides = @{
    "Receive-SBMessage" = "Receives active messages from a queue, topic subscription, or an existing session context."
    "Receive-SBDLQMessage" = "Receives messages from Service Bus dead-letter queues (queue DLQ or subscription DLQ)."
    "Receive-SBTransferDLQMessage" = "Receives messages from Service Bus transfer dead-letter queues (queue transfer DLQ or subscription transfer DLQ)."
}

$script:DescriptionOverrides = @{
    "Receive-SBMessage" = @(
        "Receives data-plane messages from Service Bus queues and subscriptions."
        "Use -MaxMessages for count-limited receive, -WaitSeconds for deadline-based receive, or no limit switches for continuous polling until cancellation."
        "In -WaitSeconds mode, internal SDK timeout/retry settings are bounded so execution time stays close to the requested deadline."
        "For session-enabled entities, the command automatically uses a session receiver or accepts -SessionContext to continue an opened session."
    ) -join " "
    "Receive-SBDLQMessage" = @(
        "Receives messages from dead-letter subqueues for queues and topic subscriptions."
        "Use -MaxMessages for count-limited receive, -WaitSeconds for deadline-based receive, or no limit switches for continuous polling until cancellation."
        "In -WaitSeconds mode, internal SDK timeout/retry settings are bounded so execution time stays close to the requested deadline."
    ) -join " "
    "Receive-SBTransferDLQMessage" = @(
        "Receives messages from transfer dead-letter subqueues for queues and topic subscriptions."
        "Use -MaxMessages for count-limited receive, -WaitSeconds for deadline-based receive, or no limit switches for continuous polling until cancellation."
        "In -WaitSeconds mode, internal SDK timeout/retry settings are bounded so execution time stays close to the requested deadline."
    ) -join " "
}

$script:CommandParameterDescriptionOverrides = @{
    "Receive-SBMessage::WaitSeconds" = "Deadline (in seconds) for bounded polling mode. Returns empty when no messages arrive before the deadline."
    "Receive-SBDLQMessage::WaitSeconds" = "Deadline (in seconds) for bounded polling mode. Returns empty when no messages arrive before the deadline."
    "Receive-SBTransferDLQMessage::WaitSeconds" = "Deadline (in seconds) for bounded polling mode. Returns empty when no messages arrive before the deadline."
}

function Get-ActionText {
    param([string]$Verb)

    switch ($Verb) {
        "Get" { return "Reads and returns" }
        "Set" { return "Updates" }
        "New" { return "Creates" }
        "Remove" { return "Removes" }
        "Send" { return "Sends" }
        "Receive" { return "Receives" }
        "Clear" { return "Clears" }
        "Export" { return "Exports" }
        "Import" { return "Imports" }
        "Close" { return "Closes" }
        "Rotate" { return "Rotates" }
        "Replay" { return "Replays" }
        default { return "Executes" }
    }
}

function Get-SynopsisText {
    param([System.Management.Automation.CommandInfo]$Command)

    if ($script:SynopsisOverrides.ContainsKey($Command.Name)) {
        return [string]$script:SynopsisOverrides[$Command.Name]
    }

    $parts = $Command.Name.Split('-', 2)
    $verb = $parts[0]
    $noun = $parts[1]
    $action = Get-ActionText -Verb $verb
    return "$action Service Bus $noun operations."
}

function Get-DescriptionText {
    param([System.Management.Automation.CommandInfo]$Command)

    if ($script:DescriptionOverrides.ContainsKey($Command.Name)) {
        return [string]$script:DescriptionOverrides[$Command.Name]
    }

    $setNames = @($Command.ParameterSets | Sort-Object Name | ForEach-Object { "'$($_.Name)'" })
    $setText = if ($setNames.Count -gt 0) { $setNames -join ", " } else { "default" }

    return @(
        "Use this cmdlet to perform Service Bus management or data-plane tasks for $($Command.Name)."
        "The command supports parameter sets: $setText."
        "Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters."
    ) -join " "
}

function Get-ParameterDescription {
    param(
        [string]$CommandName,
        [string]$ParameterName
    )

    $commandParameterKey = "$CommandName::$ParameterName"
    if ($script:CommandParameterDescriptionOverrides.ContainsKey($commandParameterKey)) {
        return [string]$script:CommandParameterDescriptionOverrides[$commandParameterKey]
    }

    $map = @{
        ServiceBusConnectionString = "Connection string for the target Service Bus namespace or emulator."
        Queue = "Queue name to target."
        Topic = "Topic name to target."
        Subscription = "Subscription name to target."
        Rule = "Rule name to target."
        Rights = "Access rights to apply to the authorization rule."
        Message = "Message objects to send or process."
        ReceivedInputObject = "Previously received Service Bus messages passed through the pipeline."
        BatchSize = "Maximum number of messages to process in a single receive batch."
        MaxMessages = "Maximum number of messages to receive or process before the command stops."
        WaitSeconds = "Maximum number of seconds to wait for messages when polling."
        Peek = "Returns messages without completing them in the broker."
        NoComplete = "Prevents automatic completion of received messages."
        NoCompleteSource = "Keeps source DLQ messages after replay."
        TransferDeadLetter = "Uses transfer dead-letter subqueue instead of the regular dead-letter subqueue."
        SequenceNumber = "Sequence number values used to target specific scheduled or deferred messages."
        SessionContext = "Session context object returned by New-SBSessionContext."
        SessionId = "Session identifier for session-enabled entities."
        State = "Session state payload to persist for the selected session."
        Body = "Message body content."
        CustomProperties = "Application properties added to the message."
        Label = "Optional message subject or label."
        CorrelationId = "Correlation identifier used in message filters or correlation matching."
        CorrelationProperty = "Hashtable of correlation properties used by correlation filters."
        SqlFilter = "SQL filter expression used by a rule or subscription creation path."
        SqlAction = "SQL action expression executed when a rule matches."
        MaxDeliveryCount = "Maximum delivery attempts before dead-lettering."
        MaxSizeInMegabytes = "Maximum entity size in megabytes."
        MaxMessageSizeInKilobytes = "Maximum message size allowed for the entity in kilobytes."
        EnablePartitioning = "Enables partitioning for the entity where supported."
        EnableBatchedOperations = "Enables server-side batched operations for the entity."
        RequiresSession = "Enables or requires session-aware processing."
        RequiresDuplicateDetection = "Enables duplicate detection."
        DuplicateDetectionHistoryTimeWindow = "Time window used for duplicate detection history."
        DefaultMessageTimeToLive = "Default time-to-live for messages in the entity."
        LockDuration = "Lock duration used for message processing."
        AutoDeleteOnIdle = "Automatically deletes the entity after being idle for this duration."
        ForwardTo = "Entity path to forward active messages to."
        ForwardDeadLetteredMessagesTo = "Entity path to forward dead-lettered messages to."
        DeadLetteringOnMessageExpiration = "Enables dead-lettering when messages expire."
        DeadLetteringOnFilterEvaluationExceptions = "Dead-letters messages when subscription filter evaluation fails."
        UserMetadata = "User-defined metadata string stored on the entity."
        Status = "Entity status to apply, such as Active or Disabled variants."
        Path = "Filesystem path to read or write topology data."
        Mode = "Import mode controlling create/update behavior."
        DestinationQueue = "Destination queue for replayed DLQ messages."
        DestinationTopic = "Destination topic for replayed DLQ messages."
        ScheduleAtUtc = "UTC timestamp when a scheduled message should be enqueued."
        KeyType = "Key slot to use, for example Primary or Secondary."
        PrimaryKey = "Primary key value to set for an authorization rule."
        SecondaryKey = "Secondary key value to set for an authorization rule."
        Force = "Bypasses confirmation prompts where supported."
        Confirm = "Controls confirmation behavior."
        Name = "Name of the target object."
        ProgressAction = "Controls how progress records are handled."
        PerSessionThread = "Number of parallel workers used per session for send operations."
        PerSessionThreadAuto = "Automatically determines per-session parallelism for send operations."
        DeadLetter = "Moves message(s) to dead-letter subqueue."
        DeadLetterReason = "Reason text stored when dead-lettering a message."
        DeadLetterErrorDescription = "Detailed dead-letter error description."
        Defer = "Defers message(s) for later retrieval by sequence number."
        Abandon = "Abandons message(s) and releases the lock."
        Complete = "Completes message(s) and removes them from the entity."
    }

    if ($map.ContainsKey($ParameterName)) {
        return [string]$map[$ParameterName]
    }

    return "Specifies the $ParameterName value for this command."
}

function Convert-ParameterValueToExample {
    param([System.Management.Automation.CommandParameterInfo]$Parameter)

    $name = $Parameter.Name
    $typeName = $Parameter.ParameterType.Name

    if ($Parameter.ParameterType -eq [System.Management.Automation.SwitchParameter]) {
        return $null
    }

    switch -Regex ($name) {
        "ServiceBusConnectionString" { return "'<connection-string>'" }
        "Queue" { return "'<queue-name>'" }
        "Topic" { return "'<topic-name>'" }
        "Subscription" { return "'<subscription-name>'" }
        "Rule" { return "'<rule-name>'" }
        "Path" { return "'<path>'" }
        "Mode" { return "'<mode>'" }
        "Status" { return "'<status>'" }
        "KeyType" { return "'Primary'" }
        "SessionId" { return "'<session-id>'" }
        "SequenceNumber" { return "@(1)" }
        "ScheduleAtUtc" { return "(Get-Date).ToUniversalTime().AddMinutes(5)" }
        "WaitSeconds" { return "5" }
        "MaxMessages" { return "10" }
        "BatchSize" { return "10" }
        "MaxDeliveryCount" { return "10" }
        "MaxSizeInMegabytes" { return "1024" }
        "MaxMessageSizeInKilobytes" { return "256" }
        "Rights" { return "@('Listen','Send')" }
        "Message" { return "<PSMessage[]>" }
        "ReceivedInputObject" { return "<ServiceBusReceivedMessage[]>" }
        "State" { return "@{ key='value' }" }
        "CustomProperties" { return "@{ key='value' }" }
        "Body" { return "@('message-body')" }
        default {
            switch ($typeName) {
                "String" { return "'<value>'" }
                "String[]" { return "@('<value>')" }
                "Int32" { return "1" }
                "Int64" { return "1" }
                "Boolean" { return "`$true" }
                "TimeSpan" { return "([TimeSpan]::FromMinutes(5))" }
                "DateTimeOffset" { return "(Get-Date).ToUniversalTime()" }
                default { return "<$typeName>" }
            }
        }
    }
}

function Get-ExampleBlock {
    param([System.Management.Automation.CommandInfo]$Command)

    $examples = [System.Collections.Generic.List[string]]::new()
    $exampleIndex = 1

    foreach ($set in $Command.ParameterSets | Sort-Object Name) {
        $required = @($set.Parameters | Where-Object { $_.IsMandatory -and $_.Name -ne "ProgressAction" } | Sort-Object Position, Name)
        if ($required.Count -eq 0) {
            continue
        }

        $parts = [System.Collections.Generic.List[string]]::new()
        $parts.Add($Command.Name)

        foreach ($param in $required) {
            $parts.Add("-$($param.Name)")
            $value = Convert-ParameterValueToExample -Parameter $param
            if ($value) {
                $parts.Add($value)
            }
        }

        $exampleCommand = ($parts -join " ")
        $examples.Add("### Example $exampleIndex ($($set.Name))")
        $examples.Add('```powershell')
        $examples.Add("PS C:\\> $exampleCommand")
        $examples.Add('```')
        $examples.Add("")
        $examples.Add("Runs $($Command.Name) using the '$($set.Name)' parameter set.")
        $examples.Add("")
        $exampleIndex++

        if ($exampleIndex -gt 2) {
            break
        }
    }

    if ($examples.Count -eq 0) {
        $examples.Add("### Example 1")
        $examples.Add('```powershell')
        $examples.Add("PS C:\\> $($Command.Name)")
        $examples.Add('```')
        $examples.Add("")
        $examples.Add("Runs $($Command.Name) with default parameters.")
    }

    return ($examples -join "`n")
}

Import-Module platyPS -Force
Import-Module $ModuleManifestPath -Force

$moduleName = [System.IO.Path]::GetFileNameWithoutExtension((Resolve-Path $ModuleManifestPath).Path)

New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
New-MarkdownHelp -Module $moduleName -OutputFolder $OutputFolder -Force -WithModulePage | Out-Null

$commands = Get-Command -Module $moduleName | Sort-Object Name

foreach ($command in $commands) {
    $path = Join-Path $OutputFolder ($command.Name + ".md")
    if (-not (Test-Path $path)) {
        continue
    }

    $content = Get-Content -Path $path -Raw

    $synopsis = Get-SynopsisText -Command $command
    $description = Get-DescriptionText -Command $command
    $examples = Get-ExampleBlock -Command $command

    if ($script:SynopsisOverrides.ContainsKey($command.Name)) {
        $content = [regex]::Replace($content, "(?ms)(## SYNOPSIS\r?\n).*?(?=\r?\n## )", "`$1$synopsis`r`n")
    }
    else {
        $content = [regex]::Replace($content, "(?ms)(## SYNOPSIS\r?\n)\{\{ Fill in the Synopsis \}\}", "`$1$synopsis")
    }

    if ($script:DescriptionOverrides.ContainsKey($command.Name)) {
        $content = [regex]::Replace($content, "(?ms)(## DESCRIPTION\r?\n).*?(?=\r?\n## )", "`$1$description`r`n")
    }
    else {
        $content = [regex]::Replace($content, "(?ms)(## DESCRIPTION\r?\n)\{\{ Fill in the Description \}\}", "`$1$description")
    }

    $content = [regex]::Replace($content, "(?ms)## EXAMPLES.*?(?=## PARAMETERS)", "## EXAMPLES`n`n$examples`n`n")

    $parameterNames = [regex]::Matches($content, "### -([A-Za-z0-9]+)") | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
    foreach ($parameterName in $parameterNames) {
        $desc = Get-ParameterDescription -CommandName $command.Name -ParameterName $parameterName
        $commandParameterKey = "$($command.Name)::$parameterName"
        if ($script:CommandParameterDescriptionOverrides.ContainsKey($commandParameterKey)) {
            $escaped = [regex]::Escape($parameterName)
            $pattern = '(?ms)(### -{0}\r?\n)(.*?)(\r?\n```yaml)' -f $escaped
            $content = [regex]::Replace(
                $content,
                $pattern,
                {
                    param($match)
                    return "$($match.Groups[1].Value)$desc$($match.Groups[3].Value)"
                })
        }
        else {
            $placeholder = "{{ Fill $parameterName Description }}"
            $content = $content.Replace($placeholder, $desc)
        }
    }

    Set-Content -Path $path -Value $content -NoNewline
}

$modulePagePath = Join-Path $OutputFolder "$moduleName.md"
if (Test-Path $modulePagePath) {
    $modulePage = Get-Content -Path $modulePagePath -Raw
    $modulePage = $modulePage.Replace("{{ Update Download Link }}", "https://github.com/eosfor/pubs")
    $modulePage = $modulePage.Replace("{{ Please enter version of help manually (X.X.X.X) format }}", "1.0.0.0")
    $modulePage = $modulePage.Replace("{{ Fill in the Description }}", "$moduleName module provides cmdlets for Azure Service Bus messaging, entity administration, DLQ operations, session state, and topology management.")

    foreach ($command in $commands) {
        $synopsis = Get-SynopsisText -Command $command
        $pattern = "(?ms)(### \[$([regex]::Escape($command.Name))\]\($([regex]::Escape($command.Name)).md\)\r?\n)\{\{ Fill in the Description \}\}"
        $modulePage = [regex]::Replace($modulePage, $pattern, "`$1$synopsis")
    }

    Set-Content -Path $modulePagePath -Value $modulePage -NoNewline
}

# Ensure there are no unresolved platyPS placeholders left.
$remaining = Get-ChildItem -Path $OutputFolder -Filter *.md | ForEach-Object {
    $hits = Select-String -Path $_.FullName -Pattern "\{\{[^}]+\}\}"
    if ($hits) {
        $_.FullName
    }
}

if ($remaining) {
    throw "Help placeholders remain in: $($remaining -join ', ')"
}

New-Item -ItemType Directory -Force -Path $ExternalHelpOutputPath | Out-Null
New-ExternalHelp -Path $OutputFolder -OutputPath $ExternalHelpOutputPath -Force | Out-Null

# Validate generated help can be resolved in PowerShell.
Import-Module $ModuleManifestPath -Force
$broken = [System.Collections.Generic.List[string]]::new()
foreach ($command in $commands) {
    $help = Get-Help $command.Name -Full
    $synopsisText = [string]$help.Synopsis
    $descriptionText = [string](($help.Description | ForEach-Object Text) -join " ")

    if ([string]::IsNullOrWhiteSpace($synopsisText) -or $synopsisText.Contains("{{")) {
        $broken.Add("$($command.Name): synopsis")
    }

    if ([string]::IsNullOrWhiteSpace($descriptionText) -or $descriptionText.Contains("{{")) {
        $broken.Add("$($command.Name): description")
    }
}

if ($broken.Count -gt 0) {
    throw "Generated help validation failed for: $($broken -join '; ')"
}

Write-Host "Generated and validated markdown + external help for $($commands.Count) commands."
