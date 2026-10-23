param(
    [Parameter(Mandatory=$true)][ValidateSet('capture','verify')][string]$Action,
    [Parameter(Mandatory=$true)][ValidateSet('source','binary')][string]$Kind,
    [Parameter(Mandatory=$true)][string]$ManifestPath,
    [string[]]$PlayerRoots = @('Builds/Players/Windows','Builds/Players/WindowsProfile')
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
function Local-Path([string]$Relative) {
    $path=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($Relative)){$Relative}else{Join-Path $root $Relative}))
    if (-not $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path leaves project: $Relative"
    }
    return $path
}
$manifest=Local-Path $ManifestPath
$builds=Join-Path $root 'Builds'
if (-not $manifest.StartsWith($builds+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence manifests must remain below ignored Builds.'
}
if ($Action -eq 'verify') {
    $saved=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if ($saved.SchemaVersion -ne 1 -or $saved.Kind -ne $Kind) { throw 'Manifest schema/kind mismatch.' }
    $PlayerRoots=@($saved.PlayerRoots)
}
$paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Add-Tree([string]$Relative) {
    $directory=Local-Path $Relative
    if (-not(Test-Path -LiteralPath $directory -PathType Container)) { throw "Required input directory is missing: $Relative" }
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked manifest input rejected: $($file.FullName)" }
        if ($file.Extension -eq '.log') { continue }
        [void]$paths.Add([IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/'))
    }
}
if ($Kind -eq 'source') {
    Add-Tree 'Assets/Game'
    Add-Tree 'ProjectSettings'
    foreach($relative in @('Packages/manifest.json','Packages/packages-lock.json')) {
        if (-not(Test-Path -LiteralPath (Local-Path $relative) -PathType Leaf)) { throw "Required package input missing: $relative" }
        [void]$paths.Add($relative)
    }
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'tools') -File | Where-Object Extension -in @('.ps1','.mjs')) {
        [void]$paths.Add('tools/'+$file.Name)
    }
}
else {
    if (-not $PlayerRoots.Count) { throw 'At least one player root is required.' }
    $assemblies=@(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Game/Runtime') -Recurse -Filter '*.asmdef' |
        ForEach-Object { (Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json).name })
    foreach($playerRoot in $PlayerRoots) {
        $directory=Local-Path $playerRoot
        if ($manifest.StartsWith($directory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Keep evidence manifests outside the player input tree.'
        }
        if (-not $directory.StartsWith((Join-Path $root 'Builds')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Player roots must stay under ignored Builds.'
        }
        Add-Tree $playerRoot
        $players=@(Get-ChildItem -LiteralPath $directory -Filter '*.exe' -File | Where-Object {
            Test-Path -LiteralPath (Join-Path $directory ($_.BaseName+'_Data')) -PathType Container
        })
        if ($players.Count -ne 1) { throw "Expected exactly one Unity player/Data pair: $playerRoot" }
        foreach($assembly in $assemblies) {
            $required=Join-Path $directory ($players[0].BaseName+'_Data/Managed/'+$assembly+'.dll')
            if (-not(Test-Path -LiteralPath $required -PathType Leaf)) { throw "Product assembly missing: $required" }
        }
        # Include the entire player tree: gameplay dependencies, engine, plugins,
        # boot configuration, scene/resource archives and required launch files.
    }
}
$files=@($paths | Sort-Object | ForEach-Object {
    $path=Local-Path $_
    [pscustomobject]@{ Path=$_; Length=(Get-Item -LiteralPath $path).Length; Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
})
if ($Action -eq 'capture') {
    if(Test-Path -LiteralPath $manifest) { throw 'Capture never overwrites a historical manifest. Choose a new path.' }
    $record=[ordered]@{SchemaVersion=1;Kind=$Kind;CapturedUtc=[DateTime]::UtcNow.ToString('O');PlayerRoots=@($PlayerRoots);Files=$files}
    New-Item -ItemType Directory -Path (Split-Path -Parent $manifest) -Force|Out-Null
    [IO.File]::WriteAllText($manifest,($record|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    Write-Output "Captured $Kind manifest: $($files.Count) files."
}
else {
    $expected=@{}
    foreach($entry in $saved.Files) {
        if($expected.ContainsKey($entry.Path)) { throw "Duplicate manifest input: $($entry.Path)" }
        $expected[$entry.Path]=$entry
    }
    $changes=[Collections.Generic.List[string]]::new()
    foreach($file in $files) {
        if(-not $expected.ContainsKey($file.Path)) {$changes.Add('unrecorded: '+$file.Path);continue}
        $entry=$expected[$file.Path]
        if($entry.Sha256 -ne $file.Sha256 -or $entry.Length -ne $file.Length) {$changes.Add('changed: '+$file.Path)}
        $expected.Remove($file.Path)
    }
    foreach($path in $expected.Keys) {$changes.Add('missing: '+$path)}
    if($changes.Count) { throw ('Manifest verification failed: '+($changes -join '; ')) }
    Write-Output "Verified $Kind manifest: $($files.Count) files, exact coverage and hashes."
}
