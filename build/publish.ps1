# Builds the Windows command line and window executables into artifacts\.
# Each ships as its own archive: a user wants the window or the command line, not both.
$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

$version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$artifacts = "artifacts"

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $artifacts | Out-Null

foreach ($rid in @("win-x64")) {
    $cliName = "rh-transfer-cli-$version-$rid"
    $guiName = "rh-transfer-gui-$version-$rid"
    $cli = Join-Path $artifacts $cliName
    $gui = Join-Path $artifacts $guiName

    Write-Host "==> $rid"
    New-Item -ItemType Directory -Path $cli | Out-Null
    New-Item -ItemType Directory -Path $gui | Out-Null

    # Ahead of time compilation already produces one native binary, so no single file switch.
    dotnet publish src\RhTransfer.Cli `
        -c Release -r $rid `
        -o $cli --nologo -v quiet

    # No --self-contained here: the project decides that per platform. On Windows the .NET
    # Desktop Runtime is already present because Rhino's installer requires it.
    dotnet publish src\RhTransfer.Gui `
        -c Release -r $rid `
        -p:PublishSingleFile=true `
        -o $gui --nologo -v quiet

    # Ahead of time compilation leaves its debug symbols beside the binary.
    Get-ChildItem $cli, $gui -Filter *.pdb | Remove-Item -Force

    # Each keeps its folder, so an executable does not land loose in Downloads with nothing to
    # say which build it is.
    Compress-Archive -Path $cli -DestinationPath (Join-Path $artifacts "$cliName.zip") -Force
    Compress-Archive -Path $gui -DestinationPath (Join-Path $artifacts "$guiName.zip") -Force
}

Write-Host ""
Write-Host "Version $version"
Get-ChildItem $artifacts -Filter *.zip | ForEach-Object { $_.Name }
