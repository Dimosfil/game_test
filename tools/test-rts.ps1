param(
    [ValidateRange(1,1800)][int]$TimeoutSeconds = 180,
    [switch]$SkipRecompile,
    [string]$Filter = 'ColonyRTS',
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The script must remain in this Unity project tools directory.'
}
$buildsRoot = Join-Path $projectRoot 'Builds'
if (-not $EvidenceDirectory) {
    $EvidenceDirectory = Join-Path $buildsRoot ('Evidence/Tests/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$evidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if (-not $evidenceDirectory.StartsWith($buildsRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Test evidence must remain below this project ignored Builds directory.'
}

function Invoke-UnityJson {
    param([string[]]$Arguments)
    $output = & unity @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Unity failed: $output" }
    $envelope = ($output -join [Environment]::NewLine) | ConvertFrom-Json
    if (-not $envelope.success) { throw ($envelope.errors | ConvertTo-Json -Depth 5) }
    if ($envelope.data.PSObject.Properties['result'] -and
        $envelope.data.result.PSObject.Properties['success'] -and -not $envelope.data.result.success) {
        throw ($envelope.data.result | ConvertTo-Json -Depth 8)
    }
    return $envelope
}
function Save-Evidence {
    param([string]$Name,[object]$Value)
    [IO.File]::WriteAllText((Join-Path $evidenceDirectory $Name),
        ($Value | ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
}

$identity = (Invoke-UnityJson -Arguments @('command','editor_status','--project-path',$projectRoot,'--json')).data.result
if ([IO.Path]::GetFullPath($identity.projectPath) -ne $projectRoot -or $identity.playMode -ne 'stopped') {
    throw 'Expected this project Editor with Play Mode stopped.'
}
$previousTest = (Invoke-UnityJson -Arguments @('command','test_status','--project-path',$projectRoot,'--json')).data.result
if ($previousTest.status -in @('running','queued','in_progress')) { throw 'Another test run already owns the Editor.' }
$sceneSetup = @((Invoke-UnityJson -Arguments @('command','list_open_scenes','--project-path',$projectRoot,'--json')).data.result.scenes)
if (-not $sceneSetup.Count -or @($sceneSetup | Where-Object { $_.isDirty -or -not $_.path }).Count) {
    throw 'Save dirty or untitled scenes before tests. No scenes or user changes were saved/discarded.'
}
# A running project player may be a performance job owned by another session.
if (@(Get-Process -Name KopruluFront -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($buildsRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
}).Count) { throw 'A project player is running. Finish that session before Editor tests; no process was stopped.' }
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
Save-Evidence 'scene-setup.json' @{ Project=$projectRoot; Scenes=$sceneSetup; CapturedUtc=[DateTime]::UtcNow.ToString('O') }
$testsMayBeRunning = $false
$testsTerminal = $false
$restoration = @{ Status='required'; Project=$projectRoot }
try {
    if (-not $SkipRecompile) {
        $compile = Invoke-UnityJson -Arguments @('recompile','--project-path',$projectRoot,'--timeout','90','--json')
        Save-Evidence 'compile.json' $compile
    }
    $null = Invoke-UnityJson -Arguments @('status','--project-path',$projectRoot,'--until-ready','--timeout','30','--json')
    Start-Sleep -Seconds 2
    $sourceManifest=Join-Path $evidenceDirectory 'source-manifest.json'
    & (Join-Path $PSScriptRoot 'rts-manifest.ps1') -Action capture -Kind source -ManifestPath $sourceManifest
    $testsMayBeRunning = $true
    $null = Invoke-UnityJson -Arguments @('command','run_tests','--project-path',$projectRoot,'--timeout',"$($TimeoutSeconds+30)",'--json','--',
        '--mode','editor','--filter',$Filter,'--async_tests','true','--timeout',"$TimeoutSeconds")
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds + 15)
    do {
        $status = Invoke-UnityJson -Arguments @('command','test_status','--project-path',$projectRoot,'--json')
        $result = $status.data.result
        if ($result.status -in @('completed','failed','cancelled','timedOut')) {
            $testsTerminal = $true
            Save-Evidence 'editmode-results.json' $status
            if ($result.status -ne 'completed') { throw "Unity tests ended: $($result.status)" }
            if ($result.summary.total -lt 1 -or $result.summary.passed -ne $result.summary.total -or
                $result.summary.failed -gt 0 -or $result.summary.skipped -gt 0 -or $result.summary.inconclusive -gt 0) {
                throw 'Not every selected RTS contract passed; inspect editmode-results.json.'
            }
            & (Join-Path $PSScriptRoot 'rts-manifest.ps1') -Action verify -Kind source -ManifestPath $sourceManifest
            Write-Output ("RTS EditMode: {0}/{1} passed, duration {2}s. Evidence: {3}" -f
                $result.summary.passed,$result.summary.total,$result.duration,$evidenceDirectory)
            break
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $testsTerminal) { throw 'RTS test status deadline exceeded; a previous result is not a pass.' }
}
catch {
    Save-Evidence 'runner-failure.json' @{ Status='failed'; Error=$_.Exception.Message; TestsMayBeRunning=$testsMayBeRunning }
    throw
}
finally {
    try {
        if ($testsMayBeRunning -and -not $testsTerminal) {
            $beforeCancel = (Invoke-UnityJson -Arguments @('command','test_status','--project-path',$projectRoot,'--json')).data.result
            if ($beforeCancel.status -notin @('completed','failed','cancelled','timedOut','idle')) {
                $null = Invoke-UnityJson -Arguments @('command','cancel_tests','--project-path',$projectRoot,'--json')
            }
            $cancelDeadline = [DateTime]::UtcNow.AddSeconds(30)
            do {
                $cancelled = (Invoke-UnityJson -Arguments @('command','test_status','--project-path',$projectRoot,'--json')).data.result
                if ($cancelled.status -in @('completed','failed','cancelled','timedOut','idle')) { $testsTerminal=$true; break }
                Start-Sleep -Seconds 1
            } while ([DateTime]::UtcNow -lt $cancelDeadline)
            if (-not $testsTerminal) { throw 'Tests still own the Editor; scene restoration is deferred. Keep scene-setup.json.' }
            Save-Evidence 'test-cancellation.json' $cancelled
        }
        $entries = @($sceneSetup | ForEach-Object {
            'new UnityEditor.SceneManagement.SceneSetup { path = ' + ($_.path | ConvertTo-Json -Compress) +
            ', isLoaded = ' + ([string][bool]$_.isLoaded).ToLowerInvariant() +
            ', isActive = ' + ([string][bool]$_.isActive).ToLowerInvariant() + ' }'
        }) -join ','
        $code = @"
for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
        throw new System.InvalidOperationException("New unsaved work is preserved; scene restoration required.");
var expected = new UnityEditor.SceneManagement.SceneSetup[] { $entries };
var before = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
bool differs = before.Length != expected.Length;
for(int i=0;i<before.Length && i<expected.Length;i++)
    differs |= before[i].path!=expected[i].path || before[i].isLoaded!=expected[i].isLoaded || before[i].isActive!=expected[i].isActive;
if(differs) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(expected);
var actual = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
if (actual.Length != expected.Length) throw new System.InvalidOperationException("Scene restoration count mismatch.");
for(int i=0;i<expected.Length;i++)
    if(actual[i].path!=expected[i].path || actual[i].isLoaded!=expected[i].isLoaded || actual[i].isActive!=expected[i].isActive)
        throw new System.InvalidOperationException("Scene restoration mismatch.");
return true;
"@
        $null = Invoke-UnityJson -Arguments @('command','eval','--project-path',$projectRoot,'--json','--','--code',$code)
        $restoration.Status = 'restored'
        $restoration.Scenes = (Invoke-UnityJson -Arguments @('command','list_open_scenes','--project-path',$projectRoot,'--json')).data.result.scenes
        Write-Output 'Editor scene setup restored and verified.'
    }
    catch {
        $restoration.Status = 'restoration-required'
        $restoration.Error = $_.Exception.Message
        throw
    }
    finally { Save-Evidence 'scene-restoration.json' $restoration }
}
