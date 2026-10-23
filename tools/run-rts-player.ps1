param(
    [ValidateSet('smoke','profile','session','combat')][string]$Kind = 'smoke',
    [ValidateSet('baseline','optimized')][string]$ProfileMode = 'baseline',
    [ValidateSet('legacy','instanced')][string]$RenderBackend = 'legacy',
    [ValidateRange(0,30)][double]$ProfileSeconds = 0,
    [switch]$Simulation,
    [switch]$DevelopmentProfile,
    [ValidateRange(1,5)][int]$CombatTrials = 5,
    [ValidateSet(60,240)][int]$CombatTicks = 60,
    [switch]$MovingAI,
    [switch]$MainLoopBoundaryProbe,
    [switch]$MainLoopNativeWaitProbe,
    [ValidateRange(0,128)][int]$JobWorkers = 0,
    [string]$TechnologyFixture,
    [string]$EvidenceDirectory,
    [string]$PlayerDirectory,
    [int]$TimeoutSeconds = 225
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$buildsRoot = Join-Path $projectRoot 'Builds'
if (-not $PlayerDirectory) { $PlayerDirectory = if ($DevelopmentProfile) { 'WindowsProfile' } else { 'Windows' } }
$playersRoot = Join-Path $buildsRoot 'Players'
$player = [IO.Path]::GetFullPath((Join-Path $playersRoot "$PlayerDirectory/KopruluFront.exe"))
if (-not $player.StartsWith($playersRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'PlayerDirectory must remain below this project Builds/Players directory.'
}
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')) -or
    -not (Test-Path -LiteralPath $player)) { throw 'Build this Unity project Windows player before running the graphical check.' }
if ($Simulation -and $Kind -ne 'profile') { throw 'Simulation is a profile option.' }
if ($DevelopmentProfile -and $Kind -notin @('profile','session','combat')) { throw 'DevelopmentProfile is a profiling option.' }
if ($TechnologyFixture -and $Kind -ne 'smoke') { throw 'TechnologyFixture is a smoke option.' }
if ($MovingAI -and ($Kind -ne 'combat' -or $CombatTicks -ne 240)) { throw 'MovingAI requires combat and CombatTicks240.' }
if ($MainLoopBoundaryProbe -and -not $MovingAI) { throw 'MainLoopBoundaryProbe requires MovingAI combat.' }
if ($MainLoopNativeWaitProbe -and -not $MainLoopBoundaryProbe) { throw 'MainLoopNativeWaitProbe requires MainLoopBoundaryProbe.' }
if ($JobWorkers -gt 0 -and $Kind -notin @('combat','session')) { throw 'JobWorkers override requires a session/combat measurement.' }
if ([string]::IsNullOrEmpty($EvidenceDirectory)) {
    $suffix = if ($Simulation) { '-simulation' } else { '' }
    if ($DevelopmentProfile) { $suffix += '-development' }
    $EvidenceDirectory = Join-Path $buildsRoot ('Evidence/Runs/{0}-{1}{2}-{3}' -f $Kind,$ProfileMode,$suffix,(Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if (-not $EvidenceDirectory.StartsWith($buildsRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence must stay inside this project ignored Builds directory.'
}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$playerArguments = @('-rtsEvidencePath',$EvidenceDirectory,'-screen-width','1600','-screen-height','900',
    '-screen-fullscreen','0','-logFile',(Join-Path $EvidenceDirectory 'player.log'))
if ($JobWorkers -gt 0) { $playerArguments += @('-job-worker-count',$JobWorkers) }
if ($Kind -eq 'profile') {
    $playerArguments += @('-rtsProfile','-rtsProfileMode',$ProfileMode,'-rtsProfileBackend',$RenderBackend,
        '-rtsProfileSeconds',$ProfileSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))
    if ($Simulation) { $playerArguments += '-rtsProfileSimulation' }
    $resultPath = Join-Path $EvidenceDirectory 'profile.json'
} elseif ($Kind -eq 'combat') {
    $playerArguments += @('-rtsCombatProfile','-rtsCombatTrials',$CombatTrials,'-rtsCombatTicks',$CombatTicks)
    if ($MovingAI) { $playerArguments += '-rtsCombatMovingAI' }
    if ($MainLoopBoundaryProbe) { $playerArguments += '-rtsMainLoopBoundaryProbe' }
    if ($MainLoopNativeWaitProbe) { $playerArguments += '-rtsMainLoopNativeWaitProbe' }
    $resultPath = Join-Path $EvidenceDirectory 'session-profile.json'
} elseif ($Kind -eq 'session') {
    $playerArguments += '-rtsSessionProfile'
    $resultPath = Join-Path $EvidenceDirectory 'session-profile.json'
} else {
    $playerArguments += '-rtsSmoke'
    if ($TechnologyFixture) {
        $TechnologyFixture = [IO.Path]::GetFullPath($TechnologyFixture)
        if (-not $TechnologyFixture.StartsWith($buildsRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $TechnologyFixture)) { throw 'Technology fixture must be an existing generated snapshot below Builds.' }
        $playerArguments += @('-rtsTechnologyFixture',$TechnologyFixture)
    }
    $resultPath = Join-Path $EvidenceDirectory 'player-smoke.json'
}
# This is an intentionally visible graphical test, not a background helper.
# Shell defaults can inherit SW_HIDE and advance Update without rendering IMGUI.
$quotedArguments = $playerArguments | ForEach-Object { '"' + ([string]$_).Replace('"','\"') + '"' }
$startedAt = [DateTime]::UtcNow
$process = Start-Process -FilePath $player -ArgumentList $quotedArguments -WindowStyle Normal -PassThru
Write-Output "Graphical $Kind player PID $($process.Id); evidence: $EvidenceDirectory"
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    # Only the exact process launched by this invocation is owned by this watchdog.
    $process.Kill()
    throw "Graphical $Kind player exceeded $TimeoutSeconds seconds. Inspect $EvidenceDirectory"
}
$process.Refresh()
if (-not (Test-Path -LiteralPath $resultPath)) { throw "Player exit $($process.ExitCode) produced no current evidence: $resultPath" }
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding utf8 | ConvertFrom-Json
$resultStartedAt = if ($result.StartedUtc -is [DateTime]) { $result.StartedUtc.ToUniversalTime() } else {
    [DateTime]::Parse([string]$result.StartedUtc,[Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
}
if ($resultStartedAt -lt $startedAt.AddSeconds(-5)) { throw 'Stale player evidence was rejected.' }
if ($process.ExitCode -ne 0 -or $result.Status -ne 'passed') {
    throw "Graphical $Kind failed (exit $($process.ExitCode), status $($result.Status)): $($result.Error)"
}
if ($Kind -eq 'profile') {
    if ($result.Mode -ne $ProfileMode -or $result.SimulationRequested -ne [bool]$Simulation -or $result.Cases.Count -ne 3) {
        throw 'Profile configuration does not match the requested workload.'
    }
    if ($result.Backend -ne $RenderBackend -or $result.MinimumSampleSeconds -ne $ProfileSeconds) { throw 'Profile backend or duration differs from request.' }
    if ($result.Hardware.DevelopmentBuild -ne [bool]$DevelopmentProfile) { throw 'Player build type does not match the requested profile.' }
    $result.Cases | Select-Object MarineCount,SampleRenderedFrames,SampleGuiRepaints,
        @{n='FrameP95Ms';e={$_.WallFrameMs.P95}},@{n='TickP95Ms';e={if ($_.SimulationStepMs.Available) {$_.SimulationStepMs.P95} else {$null}}}
} elseif ($Kind -eq 'combat') {
    if (-not $result.CombatRequested -or $result.CombatEndTick -ne $CombatTicks -or $result.DevelopmentBuild -ne [bool]$DevelopmentProfile -or $result.CombatTrials.Count -ne 2*$CombatTrials) { throw 'Combat profile configuration mismatch.' }
    if ($result.MovingAiRequested -ne [bool]$MovingAI) { throw 'Moving AI configuration mismatch.' }
    if (@($result.CombatTrials | Where-Object {
        if ($_.PSObject.Properties['MainLoopBoundaryProbeEnabled']) { $_.MainLoopBoundaryProbeEnabled -ne [bool]$MainLoopBoundaryProbe }
        else { [bool]$MainLoopBoundaryProbe }
    }).Count -gt 0) { throw 'Main-loop boundary probe configuration mismatch.' }
    if (@($result.CombatTrials | Where-Object {
        if ($_.PSObject.Properties['MainLoopNativeWaitProbeEnabled']) { $_.MainLoopNativeWaitProbeEnabled -ne [bool]$MainLoopNativeWaitProbe }
        else { [bool]$MainLoopNativeWaitProbe }
    }).Count -gt 0) { throw 'Main-loop native-wait probe configuration mismatch.' }
    if ($result.RequestedJobWorkers -ne $JobWorkers -or ($JobWorkers -gt 0 -and $result.JobWorkers -ne $JobWorkers)) { throw 'Job worker experiment configuration mismatch.' }
    $result.CombatTrials | Select-Object SelectAll,Trial,Frames,FrameP95Ms,FrameP99Ms,FrameMaxMs,FramesOver25Ms,Meets60FpsP95,Meets60FpsP99,RealTime20Hz,EndLiveMarines
} elseif ($Kind -eq 'session') {
    if ($result.DevelopmentBuild -ne [bool]$DevelopmentProfile) { throw 'Session player build type mismatch.' }
    $result.Phases | Select-Object SelectionCount,SampleFrames,FrameP95Ms,Meets60FpsP95,ClampedFrames
} else {
    $result | Select-Object Status,Frames,RuntimeErrorCount,ReplayFinalTick
}
Write-Output "Graphical $Kind measurement completed; evidence: $resultPath"
