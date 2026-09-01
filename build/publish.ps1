# Builds the Windows command line and window executables into artifacts\.
$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

$version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$artifacts = "artifacts"

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $artifacts | Out-Null

foreach ($rid in @("win-x64")) {
    $name = "rh-transfer-$version-$rid"
    $stage = Join-Path $artifacts $name

    Write-Host "==> $rid"
    New-Item -ItemType Directory -Path $stage | Out-Null

    # Ahead of time compilation already produces one native binary, so no single file switch.
    dotnet publish src\RhTransfer.Cli `
        -c Release -r $rid `
        -o $stage --nologo -v quiet

    dotnet publish src\RhTransfer.Gui `
        -c Release -r $rid --self-contained `
        -p:PublishSingleFile=true `
        -o $stage --nologo -v quiet

    # Ahead of time compilation leaves its debug symbols beside the binary.
    Get-ChildItem $stage -Filter *.pdb | Remove-Item -Force

    Compress-Archive -Path $stage -DestinationPath (Join-Path $artifacts "$name.zip") -Force
}

Write-Host ""
Write-Host "Version $version"
Get-ChildItem $artifacts -Filter *.zip | ForEach-Object { $_.Name }
