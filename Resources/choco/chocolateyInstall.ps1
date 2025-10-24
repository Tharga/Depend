$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exePath = Join-Path $toolsDir 'depend.exe'

if (Test-Path $exePath) {
    # Run once with no arguments to trigger PATH update
    & "$exePath" | Out-Null
} else {
    Write-Error "depend.exe not found at $exePath"
}
