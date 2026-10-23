param(
    [Parameter(Mandatory=$true)][ValidateSet('sql','evals','code-intelligence')][string]$Node,
    [Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$python=$env:GI_PYTHON
$local=Join-Path $root 'config/agent-runtime.local.json'
if (-not $python -and (Test-Path -LiteralPath $local)) {
    $python=(Get-Content -LiteralPath $local -Raw|ConvertFrom-Json).PythonExecutable
}
if (-not $python) {
    $command=Get-Command python -ErrorAction SilentlyContinue
    if ($command) {$python=$command.Source}
}
if (-not $python -or -not(Test-Path -LiteralPath $python -PathType Leaf)) {
    throw 'Python 3.10+ is required only for memory tools. Set GI_PYTHON or config/agent-runtime.local.json; the game is unaffected.'
}
$script=switch($Node){'sql'{'build_project_memory_index.py'} 'evals'{'rag_check.py'} 'code-intelligence'{'code_intelligence.py'}}
$previousEncoding=$env:PYTHONIOENCODING
try {
    $env:PYTHONIOENCODING='utf-8'
    & $python (Join-Path $PSScriptRoot $script) @Arguments
    $result=$LASTEXITCODE
}
finally { $env:PYTHONIOENCODING=$previousEncoding }
exit $result
