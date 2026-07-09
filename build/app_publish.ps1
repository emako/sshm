# Publish Sshm as a self-contained single-file executable for Windows x64.
param(
    [string]$OutputDir = "",
    [switch]$NoCompression
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\Sshm\Sshm.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $Root "publish\win-x64"
}

$Args = @(
    "publish", $Project,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-o", $OutputDir
)

if (-not $NoCompression) {
    $Args += "-p:EnableCompressionInSingleFile=true"
}

Write-Host "Publishing single-file exe to: $OutputDir"
& dotnet @Args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$Exe = Join-Path $OutputDir "Sshm.exe"
if (Test-Path $Exe) {
    $Size = (Get-Item $Exe).Length / 1MB
    Write-Host ("Done: {0} ({1:N1} MB)" -f $Exe, $Size)
} else {
    Write-Error "Expected output not found: $Exe"
    exit 1
}
