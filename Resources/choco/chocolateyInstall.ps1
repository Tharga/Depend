$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"
$exePath = Join-Path $toolsDir 'depend.exe'

Write-Host "Registering depend by running: $exePath --register"

Start-Process -FilePath $exePath -ArgumentList '--register' -Wait -NoNewWindow
