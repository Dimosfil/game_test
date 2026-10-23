param(
    [string]$InstructionKitPath = "tools/project-memory/instruction-kit.json",
    [string]$SharedLibraryPath = "",
    [string]$SourceRepo = "",
    [string]$SourceCachePath = "",
    [switch]$RecordApplied,
    [switch]$VerboseOutput,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-VersionFromFile {
    param([Parameter(Mandatory = $true)][string]$VersionFile)

    if (-not (Test-Path -LiteralPath $VersionFile)) {
        return $null
    }

    $match = Select-String -Path $VersionFile -Pattern '`([0-9]{4}\.[0-9]{2}\.[0-9]{2}(?:\.[0-9]+)?)`' | Select-Object -First 1
    if ($match -and $match.Matches.Count -gt 0) {
        return $match.Matches[0].Groups[1].Value
    }

    return $null
}

function Test-UsablePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    try {
        return Test-Path -LiteralPath $Path
    }
    catch {
        return $false
    }
}

function Get-DefaultSourceCachePath {
    if ($env:LOCALAPPDATA) {
        return (Join-Path $env:LOCALAPPDATA "general-instructions\source-repo")
    }

    return (Join-Path $PWD "tools\project-memory\shared-instruction-cache")
}

function Resolve-SourceRepo {
    param(
        [object]$Kit,
        [string]$ExplicitRepo
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitRepo)) {
        return [string]$ExplicitRepo
    }
    if (($Kit.PSObject.Properties.Name -contains 'update_check') -and $Kit.update_check -and ($null -ne $Kit.update_check.PSObject.Properties["source_repo"]) -and $Kit.update_check.source_repo) {
        return [string]$Kit.update_check.source_repo
    }
    if (($Kit.PSObject.Properties.Name -contains "source") -and $Kit.source -and ([string]$Kit.source -match '\.git$|^https://github\.com/')) {
        return [string]$Kit.source
    }

    return "https://github.com/Dimosfil/general-instructions.git"
}

function Sync-SourceRepo {
    param(
        [Parameter(Mandatory = $true)][string]$RepoUrl,
        [string]$CachePath
    )

    if ([string]::IsNullOrWhiteSpace($CachePath)) {
        $CachePath = Get-DefaultSourceCachePath
    }

    if ((Test-Path -LiteralPath $CachePath) -and (Test-Path -LiteralPath (Join-Path $CachePath ".git"))) {
        Write-Detail "Refreshing shared instruction source repo at $CachePath"
        git -C $CachePath fetch --quiet --depth 1 origin main
        git -C $CachePath checkout --quiet main
        git -C $CachePath reset --quiet --hard origin/main
        return $CachePath
    }

    if (Test-Path -LiteralPath $CachePath) {
        Write-Host "Source cache path exists but is not a git checkout: $CachePath"
        exit 1
    }

    $parent = Split-Path -Parent $CachePath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }

    Write-Detail "Cloning shared instruction source repo to $CachePath"
    git clone --quiet --depth 1 --branch main $RepoUrl $CachePath
    return $CachePath
}

function Resolve-SharedLibraryPath {
    param(
        [object]$Kit,
        [string]$ExplicitPath,
        [string]$ExplicitRepo,
        [string]$CachePath
    )

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidates += [string]$ExplicitPath
    }
    if ((Test-Path -LiteralPath "VERSION.md") -and (Test-Path -LiteralPath "INDEX.md") -and (Test-Path -LiteralPath "migrations")) {
        $candidates += "."
    }
    if ($env:GENERAL_INSTRUCTIONS_HOME) {
        $candidates += [string]$env:GENERAL_INSTRUCTIONS_HOME
    }
    if (($Kit.PSObject.Properties.Name -contains 'update_check') -and $Kit.update_check -and ($null -ne $Kit.update_check.PSObject.Properties["shared_library_path"]) -and $Kit.update_check.shared_library_path) {
        $candidates += [string]$Kit.update_check.shared_library_path
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-UsablePath -Path $candidate) {
            return $candidate
        }
    }

    $repo = Resolve-SourceRepo -Kit $Kit -ExplicitRepo $ExplicitRepo
    if ($repo) {
        return Sync-SourceRepo -RepoUrl $repo -CachePath $CachePath
    }

    return $null
}

function Write-Detail {
    param([string]$Message)

    if ($VerboseOutput) {
        Write-Host $Message
    }
}

function Get-MigrationVersion {
    param([Parameter(Mandatory = $true)][string]$MigrationId)

    $match = [regex]::Match($MigrationId, '^(?<version>[0-9]{4}\.[0-9]{1,2}\.[0-9]{1,2}(?:\.[0-9]+)?)__')
    if (-not $match.Success) { return [version]"0.0" }
    try { return [version]$match.Groups['version'].Value }
    catch { return [version]"0.0" }
}

function Compare-MigrationId {
    param(
        [Parameter(Mandatory = $true)][string]$Left,
        [Parameter(Mandatory = $true)][string]$Right
    )

    $versionComparison = (Get-MigrationVersion -MigrationId $Left).CompareTo(
        (Get-MigrationVersion -MigrationId $Right)
    )
    if ($versionComparison -ne 0) { return $versionComparison }
    return [string]::CompareOrdinal($Left, $Right)
}

if (-not (Test-Path -LiteralPath $InstructionKitPath)) {
    Write-Host "No instruction kit metadata found at $InstructionKitPath."
    Write-Host "Bootstrap this project from the shared instruction library first."
    exit 1
}

$kit = Get-Content -LiteralPath $InstructionKitPath -Raw | ConvertFrom-Json

# Policy belongs to metadata, not to whether this helper edits migration files.
$updatePolicy = if ($kit.PSObject.Properties.Name -contains 'update_check') { $kit.update_check } else { $null }
function Get-UpdatePolicyBoolean {
    param([string]$Name)
    if ($null -eq $updatePolicy -or $null -eq $updatePolicy.PSObject.Properties[$Name]) { return $true }
    $value = $updatePolicy.$Name
    if ($value -isnot [bool]) { throw "Invalid update_check.${Name}: expected a JSON boolean; automatic application policy is unverified." }
    return $value
}
$updateEnabled = Get-UpdatePolicyBoolean 'enabled'
$autoApply = Get-UpdatePolicyBoolean 'auto_apply_pending_migrations'
if (-not $updateEnabled) {
    Write-Host "Instruction update check: disabled (evidence: $InstructionKitPath update_check.enabled=false)"
    Write-Host 'Pending instruction migrations: unknown (check explicitly disabled)'
    exit 0
}
if ($autoApply) {
    $policyEvidence = if ($null -ne $updatePolicy -and $null -ne $updatePolicy.PSObject.Properties['auto_apply_pending_migrations']) {
        'update_check.auto_apply_pending_migrations=true'
    } else { 'update_check.auto_apply_pending_migrations absent; default=true' }
    Write-Host "Automatic migration application: enabled (evidence: $InstructionKitPath $policyEvidence)"
} else {
    Write-Host "Automatic migration application: disabled (evidence: $InstructionKitPath update_check.auto_apply_pending_migrations=false)"
}

$sharedPath = Resolve-SharedLibraryPath -Kit $kit -ExplicitPath $SharedLibraryPath -ExplicitRepo $SourceRepo -CachePath $SourceCachePath

if (-not $sharedPath) {
    Write-Host "No usable shared instruction library path found."
    Write-Host "Set update_check.source_repo, pass -SourceRepo, set GENERAL_INSTRUCTIONS_HOME, or pass -SharedLibraryPath."
    exit 1
}

$versionFile = Join-Path $sharedPath "VERSION.md"
$migrationsPath = Join-Path $sharedPath "migrations"
$latestVersion = Get-VersionFromFile -VersionFile $versionFile

if (-not $latestVersion) {
    Write-Host "Could not read shared instruction version from $versionFile."
    exit 1
}

$installedVersion = [string]$kit.instruction_kit_version
$legacyApplied = @()
if (($kit.PSObject.Properties.Name -contains "applied_migrations") -and $kit.applied_migrations) {
    $legacyApplied = @($kit.applied_migrations | ForEach-Object { [string]$_ })
}
$appliedThrough = ""
$additionalApplied = @()
$skippedMigrations = @()
if (($kit.PSObject.Properties.Name -contains "migration_state") -and $kit.migration_state) {
    if ($kit.migration_state.applied_through) {
        $appliedThrough = [string]$kit.migration_state.applied_through
    }
    if ($kit.migration_state.additional_applied_migrations) {
        $additionalApplied = @($kit.migration_state.additional_applied_migrations | ForEach-Object { [string]$_ })
    }
    if ($kit.migration_state.skipped_migrations) {
        $skippedMigrations = @($kit.migration_state.skipped_migrations | ForEach-Object { [string]$_ })
    }
}

function Test-MigrationApplied {
    param([Parameter(Mandatory = $true)][string]$MigrationId)

    if ($skippedMigrations -contains $MigrationId) {
        return $false
    }
    if ($legacyApplied -contains $MigrationId -or $additionalApplied -contains $MigrationId) {
        return $true
    }
    if ([string]::IsNullOrWhiteSpace($appliedThrough)) { return $false }

    return (Compare-MigrationId -Left $MigrationId -Right $appliedThrough) -le 0
}

Write-Host "Instruction kit: installed=$installedVersion available=$latestVersion"

$versionComparison = -1
if ($installedVersion) {
    try {
        $versionComparison = ([version]$installedVersion).CompareTo([version]$latestVersion)
    }
    catch {
        if ($installedVersion -eq $latestVersion) {
            $versionComparison = 0
        }
    }
}

if ($versionComparison -eq 0 -and $skippedMigrations.Count -eq 0) {
    Write-Host "Pending instruction migrations: 0"
    exit 0
}

if ($versionComparison -gt 0) {
    Write-Host "Accepted source is older than the installed instruction kit."
    Write-Host "Pending instruction migrations: 0"
    exit 0
}

if (-not (Test-Path -LiteralPath $migrationsPath)) {
    throw "Pending instruction migrations: unknown; migration source is missing: $migrationsPath"
}

$pending = @(Get-ChildItem -LiteralPath $migrationsPath -Filter "*.md" |
    Where-Object { $_.Name -ne "README.md" } |
    Where-Object {
        $migrationId = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        -not (Test-MigrationApplied -MigrationId $migrationId)
    } |
    Sort-Object `
        @{ Expression = { Get-MigrationVersion -MigrationId $_.BaseName } }, `
        @{ Expression = { $_.BaseName } })

if (-not $pending) {
    Write-Host "Pending instruction migrations: 0"
    exit 0
}

Write-Host "Pending instruction migrations: $($pending.Count)"
$pending | ForEach-Object {
    $migrationId = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    Write-Host "- $migrationId"
    Write-Detail "  file: $($_.Name)"
}

if ($Apply) {
    Write-Host ""
    Write-Host "-Apply cannot execute agent-authored Markdown migrations or record them before file verification."
    Write-Host "This helper limitation does not disable the automatic application policy shown above."
    Write-Host "The agent must apply the pending migration instructions first."
    Write-Host "After verifying the file changes, run with -RecordApplied to update metadata."
    exit 1
}

if (-not $RecordApplied) {
    if ($autoApply) {
        Write-Host "Required agent action: apply pending accepted migrations now, before task-specific work; no additional confirmation is required."
        Write-Host "Migration source: $migrationsPath"
        Write-Host "Read the listed migration files in order, merge and verify their file changes, then run with -RecordApplied."
        Write-Host "An availability notice is not completion. Report a concrete blocker if application cannot proceed; never infer disabled policy from helper behavior."
    } else {
        Write-Host "Automatic application skipped by the explicit metadata choice; leave migration state unchanged."
    }
    exit 0
}

Write-Detail "Recording migration metadata only after file changes were applied and verified."
Write-Detail "If file changes are not complete, stop now and do not record migrations as applied."

$allMigrationIds = @(Get-ChildItem -LiteralPath $migrationsPath -Filter "*.md" |
    Where-Object { $_.Name -ne "README.md" } |
    ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } |
    Sort-Object `
        @{ Expression = { Get-MigrationVersion -MigrationId $_ } }, `
        @{ Expression = { $_ } })
$latestMigrationId = if ($allMigrationIds.Count -gt 0) { $allMigrationIds[-1] } else { "" }

$kit.instruction_kit_version = $latestVersion
$migrationState = [pscustomobject][ordered]@{
    schema_version = 2
    applied_through = $latestMigrationId
    additional_applied_migrations = @()
    skipped_migrations = @()
}
$kit | Add-Member -NotePropertyName migration_state -NotePropertyValue $migrationState -Force
if ($kit.PSObject.Properties.Name -contains "applied_migrations") {
    $kit.PSObject.Properties.Remove("applied_migrations")
}
$kit | Add-Member -NotePropertyName last_update_check_at -NotePropertyValue (Get-Date -Format "yyyy-MM-ddTHH:mm:ssK") -Force
$kit | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $InstructionKitPath -Encoding UTF8

Write-Host "Recorded applied migrations: $($pending.Count)"
Write-Host "Instruction kit metadata: $InstructionKitPath"
