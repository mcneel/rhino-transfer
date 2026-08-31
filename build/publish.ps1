# Builds the Windows executables into artifacts\.
$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

$artifacts = "artifacts"
if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $artifacts | Out-Null

foreach ($rid in @("win-x64", "win-arm64")) {
    Write-Host "==> $rid"

    dotnet publish src\RhTransfer.Cli `
        -c Release -r $rid --self-contained `
        -p:PublishSingleFile=true `
        -o "$artifacts\$rid" --nologo -v quiet

    dotnet publish src\RhTransfer.Gui `
        -c Release -r $rid --self-contained `
        -p:PublishSingleFile=true `
        -o "$artifacts\$rid" --nologo -v quiet
}

Write-Host ""
Write-Host "Built:"
Get-ChildItem $artifacts -Recurse -Include "rh-transfer.exe", "RhinoSettingsTransfer.exe" | ForEach-Object { $_.FullName }
