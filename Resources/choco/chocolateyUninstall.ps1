$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exePath  = Join-Path $toolsDir "depend.exe"

if (Test-Path $exePath) {
    Write-Host "Unregistering depend by running: $exePath --unregister"
    & $exePath --unregister | Out-Null
}
else {
    Write-Warning "depend.exe not found during uninstall. Skipping unregister."
}

# Remove the shims Chocolatey created
Uninstall-BinFile -Name "depend" -Path $exePath
