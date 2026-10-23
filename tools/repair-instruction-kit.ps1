param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceCommit,
    [string[]]$RegisterExisting = @()
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$metadataPath = Join-Path $root 'tools/project-memory/instruction-kit.json'
$kit = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$source = [string]$kit.update_check.source_repo
if ($source -notmatch '^https://github\.com/([A-Za-z0-9_-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?$') {
    throw 'Recovery requires the configured public GitHub instruction source.'
}
$sourceBase = 'https://raw.githubusercontent.com/'+$Matches[1]+'/'+$Matches[2]+'/'+$SourceCommit+'/'
$version = (Invoke-WebRequest -Uri ($sourceBase+'VERSION.md')).Content
if ($version -notmatch ('`'+[regex]::Escape([string]$kit.instruction_kit_version)+'`')) {
    throw 'Pinned accepted source version differs from the installed kit. Use the migration/update workflow.'
}
$restored = [Collections.Generic.List[object]]::new()
$manifestRelative='tools/project-memory/instruction-kit-recovery.json'
$manifestPath=Join-Path $root $manifestRelative
if (Test-Path -LiteralPath $manifestPath) {
    $previous = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($previous.Commit -ne $SourceCommit -or $previous.Version -ne $kit.instruction_kit_version) {
        throw 'A different recovery record exists; preserve it and select a new record deliberately.'
    }
    foreach ($entry in $previous.Files) { $restored.Add($entry) }
}
foreach ($relative in $RegisterExisting) {
    if ($relative -notmatch '^(patterns|templates)/[A-Za-z0-9_./-]+\.(md|json|ps1|py)$' -or $relative.Contains('..')) {
        throw "Registration is limited to named library files: $relative"
    }
    $content = (Invoke-WebRequest -Uri ($sourceBase+$relative)).Content
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($content)
    $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if ((Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash -ne $sha) {
        throw "Existing file differs from accepted source; preserved: $relative"
    }
    if (-not @($restored | Where-Object Path -eq $relative).Count) {
        $restored.Add([pscustomobject]@{Path=$relative;Sha256=$sha})
    }
}
for ($round=0;$round -lt 12;$round++) {
    $output = & (Join-Path $PSScriptRoot 'test-instruction-kit.ps1') -Json
    $status = ($output -join [Environment]::NewLine) | ConvertFrom-Json
    if (-not $status.Missing.Count) { break }
    foreach ($relative in $status.Missing) {
        if ($relative -notmatch '^(patterns|templates)/[A-Za-z0-9_./-]+\.(md|json|ps1|py)$' -or $relative.Contains('..')) {
            throw "Refusing automatic recovery of non-library path: $relative"
        }
        $destination = Join-Path $root $relative
        if (Test-Path -LiteralPath $destination) { throw "Recovery never overwrites existing content: $relative" }
        $content = (Invoke-WebRequest -Uri ($sourceBase+$relative)).Content
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        [IO.File]::WriteAllText($destination,$content,[Text.UTF8Encoding]::new($false))
        $restored.Add([pscustomobject]@{ Path=$relative; Sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() })
        if ($restored.Count -gt 64) { throw 'Recovery exceeded the bounded consumer-kit limit.' }
    }
}
& (Join-Path $PSScriptRoot 'test-instruction-kit.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Instruction recovery is incomplete.' }
if ($restored.Count) {
    [IO.File]::WriteAllText($manifestPath,(@{Version=$kit.instruction_kit_version;Source=$source;Commit=$SourceCommit;Files=@($restored)} |
        ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    $kit.copied_files = @(@($kit.copied_files) + @($restored | ForEach-Object Path) | Select-Object -Unique)
    $kit | Add-Member -NotePropertyName recovery_manifest -NotePropertyValue $manifestRelative -Force
    [IO.File]::WriteAllText($metadataPath,($kit|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
}
Write-Output "Recovered $($restored.Count) missing files from accepted version $($kit.instruction_kit_version)."
