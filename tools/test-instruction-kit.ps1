param([switch]$Json)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$files = @('AGENTS.md','BOOTSTRAP.md') + @(Get-ChildItem -LiteralPath (Join-Path $root 'patterns') -Recurse -File -Filter '*.md' |
    ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName) })
$missing = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) {
    $path = Join-Path $root $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { [void]$missing.Add($file); continue }
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($path),'(?:patterns|templates)/[A-Za-z0-9_./-]+\.(?:md|json|ps1|py)')) {
        $target = [IO.Path]::GetFullPath((Join-Path $root $match.Value))
        if (-not $target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
            throw "Instruction reference leaves the project: $($match.Value)"
        }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { [void]$missing.Add($match.Value) }
    }
}
$routes = Get-Content -LiteralPath (Join-Path $root 'config/gi-command-routes.json') -Raw | ConvertFrom-Json
foreach ($route in $routes.routes) {
    foreach ($path in $route.context_files) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $path) -PathType Leaf)) { [void]$missing.Add($path) }
    }
}
$result = [pscustomobject]@{ Checked=$files.Count; Routes=$routes.routes.Count; Missing=@($missing | Sort-Object); Status=if($missing.Count){'incomplete'}else{'passed'} }
if ($Json) { $result | ConvertTo-Json -Depth 4 }
elseif ($missing.Count) { Write-Output ('Missing instruction files: ' + ($result.Missing -join ', ')) }
else { Write-Output "Instruction references: $($files.Count) files / $($routes.routes.Count) routes passed." }
if ($missing.Count) { exit 1 }
exit 0
