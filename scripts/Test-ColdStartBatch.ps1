[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$BatchIndex,
    [ValidateRange(1, 5)][int]$Iterations = 5,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts\cold-start-validation\batch-$BatchIndex")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $projectRoot 'src\CodexQuotaFloat\bin\Release\net10.0-windows\CodexQuotaFloat.exe'
$bootstrapPath = Join-Path $env:LOCALAPPDATA 'CodexQuotaFloat\Logs\bootstrap.log'
$instancePath = Join-Path $env:LOCALAPPDATA 'CodexQuotaFloat\instance.json'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Find-StepIndex([string[]]$Lines, [string]$Step) {
    for ($index = 0; $index -lt $Lines.Count; $index++) { if ($Lines[$index] -match "step=$Step(?:;|$)") { return $index } }
    return -1
}

function Write-IterationResult($Result) {
    $path = Join-Path $OutputDirectory ("iteration-{0:D2}.json" -f $Result.Iteration)
    $Result | ConvertTo-Json -Depth 5 | Set-Content -Path $path -Encoding utf8
    return $path
}

if (-not (Test-Path $exePath)) { throw "Release executable not found: $exePath" }
if (-not (Test-Path $bootstrapPath)) { throw "Bootstrap log not found: $bootstrapPath" }

$outputFiles = @()
$results = @()
for ($iterationIndex = 1; $iterationIndex -le $Iterations; $iterationIndex++) {
    $startedAt = [DateTimeOffset]::Now
    $primaryPid = $null
    $primaryStartTimeUtc = $null
    $shutdownPid = $null
    $roundLogStartOffset = $null
    $shutdownExitCode = $null
    $failureStage = ''
    $failureMessage = ''
    $startupSucceeded = $false
    $mainWindowCreated = $false
    $shutdownConfirmed = $false
    $primaryPidExited = $false
    $instanceJsonDeleted = $false
    $exitSequenceValid = $false
    $residualProcessFound = $false
    $startupDurationMs = $null
    $shutdownDurationMs = $null
    try {
        if (Get-Process CodexQuotaFloat -ErrorAction SilentlyContinue) { throw 'Residual CodexQuotaFloat process before iteration' }
        $roundLogStartOffset = (Get-Item -LiteralPath $bootstrapPath).Length
        $roundLogStartLineCount = @(Get-Content -Path $bootstrapPath).Count
        $roundStartTime = [DateTimeOffset]::Now
        $startupClock = [Diagnostics.Stopwatch]::StartNew()
        $primaryProcess = Start-Process -FilePath $exePath -PassThru
        $primaryPid = $primaryProcess.Id
        $primaryStartTimeUtc = $primaryProcess.StartTime.ToUniversalTime()
        $startupDeadline = [Diagnostics.Stopwatch]::StartNew()
        do {
            $newLines = @(Get-Content -Path $bootstrapPath | Select-Object -Skip $roundLogStartLineCount)
            $startupSucceeded = (($newLines -match "pid=$primaryPid.*step=PROCESS_ENTRY").Count -gt 0) -and (($newLines -match "pid=$primaryPid.*step=MUTEX_OWNED_TRUE").Count -gt 0) -and (($newLines -match "pid=$primaryPid.*step=PRIMARY_PATH_ENTER").Count -gt 0)
            $mainWindowCreated = (($newLines -match "pid=$primaryPid.*step=MAIN_WINDOW_CREATE_END").Count -gt 0)
            if (-not ($startupSucceeded -and $mainWindowCreated)) { Start-Sleep -Milliseconds 100 }
        } while (-not ($startupSucceeded -and $mainWindowCreated) -and $startupDeadline.Elapsed.TotalSeconds -lt 4)
        $startupClock.Stop(); $startupDurationMs = [int]$startupClock.ElapsedMilliseconds
        if (-not ($startupSucceeded -and $mainWindowCreated)) { $failureStage = 'Startup'; throw 'Startup lifecycle timeout' }

        $shutdownClock = [Diagnostics.Stopwatch]::StartNew()
        $shutdownProcess = Start-Process -FilePath $exePath -ArgumentList '--shutdown' -PassThru
        $shutdownPid = $shutdownProcess.Id
        $shutdownProcess.WaitForExit()
        $shutdownClock.Stop(); $shutdownDurationMs = [int]$shutdownClock.ElapsedMilliseconds; $shutdownExitCode = $shutdownProcess.ExitCode
        $shutdownConfirmed = $shutdownExitCode -eq 0
        if (-not $shutdownConfirmed) { $failureStage = 'Shutdown'; throw "Shutdown exit code: $shutdownExitCode" }

        $exitDeadline = [Diagnostics.Stopwatch]::StartNew()
        while ((Get-Process -Id $primaryPid -ErrorAction SilentlyContinue) -and $exitDeadline.Elapsed.TotalSeconds -lt 5) { Start-Sleep -Milliseconds 100 }
        $remainingPrimary = Get-Process -Id $primaryPid -ErrorAction SilentlyContinue
        $primaryPidExited = -not $remainingPrimary -or [Math]::Abs(($remainingPrimary.StartTime.ToUniversalTime() - $primaryStartTimeUtc).TotalSeconds) -ge 1
        $instanceJsonDeleted = -not (Test-Path $instancePath)
        $residualProcessFound = [bool](Get-Process CodexQuotaFloat -ErrorAction SilentlyContinue)
        if (-not ($primaryPidExited -and $instanceJsonDeleted -and -not $residualProcessFound)) { $failureStage = 'Exit'; throw 'PID, metadata, or process cleanup check failed' }

        $allRoundLines = @(Get-Content -Path $bootstrapPath | Select-Object -Skip $roundLogStartLineCount)
        $primaryLines = @($allRoundLines | Where-Object { $_ -match "pid=$primaryPid" })
        $shutdownLines = @($allRoundLines | Where-Object { $_ -match "pid=$shutdownPid" })
        if (($shutdownLines -match 'step=SHUTDOWN_CONFIRMED_EXIT').Count -lt 1) { $failureStage = 'ShutdownConfirmation'; throw 'Shutdown confirmation log missing' }
        $deleteIndex = Find-StepIndex $primaryLines 'INSTANCE_METADATA_DELETE_END'
        $mutexBeginIndex = Find-StepIndex $primaryLines 'MUTEX_RELEASE_BEGIN'
        $mutexEndIndex = Find-StepIndex $primaryLines 'MUTEX_RELEASE_END'
        $shutdownIndex = Find-StepIndex $primaryLines 'APPLICATION_SHUTDOWN'
        $exitIndex = Find-StepIndex $primaryLines 'PROCESS_EXIT'
        $exitSequenceValid = $deleteIndex -ge 0 -and $deleteIndex -lt $mutexBeginIndex -and $mutexBeginIndex -lt $mutexEndIndex -and $mutexEndIndex -lt $shutdownIndex -and $shutdownIndex -lt $exitIndex
        if (-not $exitSequenceValid) { $failureStage = 'ExitSequence'; throw "Invalid exit sequence: $deleteIndex,$mutexBeginIndex,$mutexEndIndex,$shutdownIndex,$exitIndex" }
    }
    catch {
        if (-not $failureStage) { $failureStage = 'ScriptFailure' }
        $failureMessage = $_.Exception.Message
    }
    finally {
        if ($primaryPid -and (Get-Process -Id $primaryPid -ErrorAction SilentlyContinue)) { Stop-Process -Id $primaryPid -ErrorAction SilentlyContinue }
        $result = [pscustomobject]@{ BatchIndex=$BatchIndex; Iteration=$iterationIndex; StartedAt=$startedAt; RoundLogStartOffset=$roundLogStartOffset; PrimaryPid=$primaryPid; PrimaryProcessStartTimeUtc=$primaryStartTimeUtc; ShutdownPid=$shutdownPid; StartupSucceeded=$startupSucceeded; MainWindowCreated=$mainWindowCreated; StartupDurationMs=$startupDurationMs; ShutdownExitCode=$shutdownExitCode; ShutdownConfirmed=$shutdownConfirmed; PrimaryPidExited=$primaryPidExited; InstanceJsonDeleted=$instanceJsonDeleted; ExitSequenceValid=$exitSequenceValid; ShutdownDurationMs=$shutdownDurationMs; ResidualProcessFound=$residualProcessFound; Success=([string]::IsNullOrEmpty($failureMessage)); FailureStage=$failureStage; FailureMessage=$failureMessage }
        $outputFiles += Write-IterationResult $result; $results += $result
    }
}

$passed = @($results | Where-Object Success).Count
$summary = [pscustomobject]@{ BatchIndex=$BatchIndex; Iterations=$Iterations; Passed=$passed; Failed=($Iterations-$passed); AverageStartupMs=([double]($results | Where-Object StartupDurationMs | Measure-Object StartupDurationMs -Average).Average); MaxStartupMs=($results | Where-Object StartupDurationMs | Measure-Object StartupDurationMs -Maximum).Maximum; AverageShutdownMs=([double]($results | Where-Object ShutdownDurationMs | Measure-Object ShutdownDurationMs -Average).Average); MaxShutdownMs=($results | Where-Object ShutdownDurationMs | Measure-Object ShutdownDurationMs -Maximum).Maximum; OutputFiles=$outputFiles }
$summary | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $OutputDirectory 'batch-summary.json') -Encoding utf8
$summary | ConvertTo-Json -Depth 5
