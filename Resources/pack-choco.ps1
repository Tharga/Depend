param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

Write-Host "== Building Chocolatey package version $Version ==" -ForegroundColor Cyan

# Resolve script root (where pack-choco.ps1 is located)
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Project root is 1 level up from Resources folder
$ProjectRoot = Split-Path -Parent $ScriptRoot

# Paths
$PublishDir = Join-Path $ProjectRoot "choco-temp\tools"
$ChocoTemp  = Join-Path $ProjectRoot "choco-temp"
$LicenseSrc = Join-Path $ProjectRoot "LICENSE"
$InstallSrc = Join-Path $ScriptRoot "choco\chocolateyInstall.ps1"
$UninstallSrc = Join-Path $ScriptRoot "choco\chocolateyUninstall.ps1"
$NuspecSrc  = Join-Path $ScriptRoot "choco\depend.nuspec"

# Clean temp folder
if (Test-Path $ChocoTemp) {
    Remove-Item $ChocoTemp -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

Write-Host "== Publishing .NET application ==" -ForegroundColor Green

dotnet publish `
    "$ProjectRoot\Tharga.Depend\Tharga.Depend.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    /p:PublishSingleFile=false `
    -o $PublishDir

Write-Host "== Copying Chocolatey files ==" -ForegroundColor Green

Copy-Item $LicenseSrc (Join-Path $ChocoTemp "LICENSE.txt")
Copy-Item $InstallSrc (Join-Path $PublishDir "chocolateyInstall.ps1")
Copy-Item $UninstallSrc (Join-Path $PublishDir "chocolateyUninstall.ps1")
Copy-Item $NuspecSrc (Join-Path $ChocoTemp "depend.nuspec")

Write-Host "== Packing with Chocolatey ==" -ForegroundColor Green

Push-Location $ChocoTemp
choco pack depend.nuspec --version $Version
Pop-Location

Write-Host "== DONE! Package built: $ChocoTemp\depend.$Version.nupkg ==" -ForegroundColor Cyan
