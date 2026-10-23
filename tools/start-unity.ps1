param([int]$ReadyTimeoutSeconds = 60)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
$versionText = [System.IO.File]::ReadAllText($versionFile)
$versionMatch = [regex]::Match($versionText,'(?m)^m_EditorVersion:\s*(\S+)')
if (-not $versionMatch.Success) { throw 'Project Unity version is missing.' }
$projectVersion = $versionMatch.Groups[1].Value
$status = (& unity status --project-path $projectRoot --json) | ConvertFrom-Json
if ($status.success -and $status.data.count -eq 1) {
    Write-Output "Reusing live project Editor: $projectRoot"
    exit 0
}

$instances = (& unity pipeline list --json) | ConvertFrom-Json
if (-not $instances.success) { throw 'Could not inspect existing Unity instances.' }
$running = @($instances.data.instances | Where-Object {
    $_.isRunning -and [System.IO.Path]::GetFullPath($_.projectPath) -eq $projectRoot
})
if ($running.Count -gt 0) {
    throw 'This project already has an Editor, but Pipeline is unavailable. Diagnose startup/import/Safe Mode; this script will not close it or discard unsaved work.'
}

$installed = (& unity editors --installed --json) | ConvertFrom-Json
if (-not $installed.success) { throw 'Could not inspect installed Unity Editors.' }
$matching = @($installed.data | Where-Object version -eq $projectVersion)
if ($matching.Count -ne 1) { throw "Expected one installed Unity $projectVersion Editor, found $($matching.Count)." }
$executable = [System.IO.Path]::GetFullPath($matching[0].location)
if (-not (Test-Path -LiteralPath $executable)) { throw "Editor executable missing: $executable" }
$evidenceDirectory = Join-Path $projectRoot 'Builds/Evidence'
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$logFile = Join-Path $evidenceDirectory 'editor-session.log'
Write-Output 'Starting a local persistent Unity Editor/Pipeline for this project; it remains running until explicitly closed.'
$editorArgs = @('-batchmode','-projectPath',('"'+$projectRoot+'"'),'-logFile',('"'+$logFile+'"'))
$started = Start-Process -FilePath $executable -ArgumentList $editorArgs -WindowStyle Hidden -PassThru
Write-Output "Project Editor PID: $($started.Id)"
& unity status --project-path $projectRoot --until-ready --timeout $ReadyTimeoutSeconds --json
if ($LASTEXITCODE -ne 0) { throw "Editor did not become ready. Inspect $logFile; do not automatically reinstall Pipeline." }
