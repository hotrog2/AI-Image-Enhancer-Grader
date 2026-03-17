[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$appProject = Join-Path $repoRoot "src\ColorGrader.App\ColorGrader.App.csproj"
$installerScript = Join-Path $repoRoot "build\ColorGrader.iss"
$publishProfile = "Release-win-x64"

$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish\$RuntimeIdentifier"
$packageDir = Join-Path $artifactsRoot "packages"
$portableZip = Join-Path $packageDir "ColorGrader-$RuntimeIdentifier-portable.zip"
$setupExe = Join-Path $packageDir "ColorGrader-$RuntimeIdentifier-setup.exe"

foreach ($path in @($publishDir, $packageDir)) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

Write-Host "Publishing ColorGrader for $RuntimeIdentifier..."
dotnet publish $appProject `
    -c $Configuration `
    -p:PublishProfile=$publishProfile `
    -p:RuntimeIdentifier=$RuntimeIdentifier `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$appExe = Join-Path $publishDir "ColorGrader.App.exe"
if (-not (Test-Path $appExe)) {
    throw "Expected published executable was not found at $appExe"
}

Write-Host "Creating portable archive..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -CompressionLevel Optimal

$isccCommand = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $isccCommand) {
    $defaultIsccPath = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    if (Test-Path $defaultIsccPath) {
        $isccCommand = Get-Item $defaultIsccPath
    }
}
if (-not $isccCommand) {
    $localIsccPath = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    if (Test-Path $localIsccPath) {
        $isccCommand = Get-Item $localIsccPath
    }
}

if (-not $isccCommand) {
    throw "Inno Setup 6 was not found. Install JRSoftware.InnoSetup so the packaging flow can build a real setup executable."
}

$isccPath = if ($isccCommand -is [System.Management.Automation.CommandInfo]) {
    $isccCommand.Source
}
else {
    $isccCommand.FullName
}

Write-Host "Building setup executable..."
& $isccPath `
    "/DMyAppVersion=$Version" `
    "/DAppPublishDir=$publishDir" `
    "/DOutputDir=$packageDir" `
    "/DOutputBaseFilename=ColorGrader-$RuntimeIdentifier-setup" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup build failed."
}

if (-not (Test-Path $setupExe)) {
    throw "Expected setup executable was not found at $setupExe"
}

Write-Host ""
Write-Host "Package flow complete."
Write-Host "Publish output:  $publishDir"
Write-Host "Portable zip:   $portableZip"
Write-Host "Setup exe:      $setupExe"
