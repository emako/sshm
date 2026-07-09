# Publish Sshm as self-contained single-file executables.
param(
    [string[]]$Target = @("win-x64"),
    [string]$OutputRoot = "",
    [switch]$NoCompression
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\Sshm\Sshm.csproj"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $Root "publish"
}

$AllTargets = @("win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
$TargetList = [System.Collections.Generic.List[string]]::new()
foreach ($Part in @($Target)) {
    foreach ($Item in ($Part -split '[,\s]+')) {
        $Trimmed = $Item.Trim()
        if ($Trimmed) {
            $TargetList.Add($Trimmed)
        }
    }
}
$Target = $TargetList.ToArray()
if ($Target.Count -eq 0) {
    throw "No publish target specified."
}

if ($Target -contains "all") {
    $Target = $AllTargets
} else {
    foreach ($Runtime in $Target) {
        if ($AllTargets -notcontains $Runtime) {
            throw "Unknown target '$Runtime'. Valid: $($AllTargets -join ', '), all"
        }
    }
}

function Get-PublishOutputName {
    param([string]$Runtime)
    if ($Runtime.StartsWith("win-")) {
        return "sshm.exe"
    }
    return "sshm"
}

function Publish-SshmTarget {
    param(
        [string]$Runtime,
        [string]$OutputDir,
        [switch]$NoCompression
    )

    $PublishArgs = @(
        "publish", $Project,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=none",
        "-p:DebugSymbols=false",
        "-o", $OutputDir
    )

    if (-not $NoCompression) {
        $PublishArgs += "-p:EnableCompressionInSingleFile=true"
    }

    Write-Host ""
    Write-Host "==> Publishing $Runtime to: $OutputDir"
    & dotnet @PublishArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for $Runtime (exit code $LASTEXITCODE)"
    }

    $OutputName = Get-PublishOutputName -Runtime $Runtime
    $OutputPath = Join-Path $OutputDir $OutputName
    if (-not (Test-Path $OutputPath)) {
        throw "Expected output not found: $OutputPath"
    }

    $Size = (Get-Item $OutputPath).Length / 1MB
    Write-Host ("Done: {0} ({1:N1} MB)" -f $OutputPath, $Size)
    return ,$OutputPath
}

$Published = @()
foreach ($Runtime in $Target) {
    $OutputDir = Join-Path $OutputRoot $Runtime
    $Published += Publish-SshmTarget -Runtime $Runtime -OutputDir $OutputDir -NoCompression:$NoCompression
}

Write-Host ""
Write-Host ("Published {0} target(s):" -f $Published.Count)
foreach ($Path in $Published) {
    Write-Host "  $Path"
}
