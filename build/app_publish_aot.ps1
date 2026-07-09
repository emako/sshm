# Publish Sshm as Native AOT self-contained single-file executables.
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

function Get-NativeAotRids {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $isArm64 = $arch -eq [System.Runtime.InteropServices.Architecture]::Arm64

    if (($PSVersionTable.PSVersion.Major -ge 6 -and $IsWindows) -or ($env:OS -eq "Windows_NT")) {
        if ($isArm64) {
            return @("win-arm64", "win-x64")
        }
        return @("win-x64")
    }

    if ($PSVersionTable.PSVersion.Major -ge 6 -and $IsLinux) {
        if ($isArm64) {
            return @("linux-arm64", "linux-x64")
        }
        return @("linux-x64", "linux-arm64")
    }

    if ($PSVersionTable.PSVersion.Major -ge 6 -and $IsMacOS) {
        if ($isArm64) {
            return @("osx-arm64", "osx-x64")
        }
        return @("osx-x64", "osx-arm64")
    }

    return @()
}

if ($Target -contains "all") {
    $Target = Get-NativeAotRids
    if ($Target.Count -eq 0) {
        throw "No Native AOT targets available on this OS."
    }
    Write-Host "Target all -> $($Target -join ', ') (Native AOT, current OS only)"
} else {
    foreach ($Runtime in $Target) {
        if ($AllTargets -notcontains $Runtime) {
            throw "Unknown target '$Runtime'. Valid: $($AllTargets -join ', '), all"
        }
    }
}

$BuildableRids = Get-NativeAotRids
$Unsupported = $Target | Where-Object { $BuildableRids -notcontains $_ }
if ($Unsupported.Count -gt 0) {
    throw @(
        "Native AOT cannot cross-compile to: $($Unsupported -join ', ')"
        "Build these targets on the matching OS (or use build/app_publish.sh in WSL/macOS/Linux)."
        "This machine can AOT publish: $($BuildableRids -join ', ')"
    ) -join [Environment]::NewLine
}

function Get-PublishOutputName {
    param([string]$Runtime)
    if ($Runtime.StartsWith("win-")) {
        return "sshm.exe"
    }
    return "sshm"
}

function Remove-UnusedPublishArtifacts {
    param([string]$OutputDir)

    # Onigwrap is pulled in by Terminal.Gui -> TextMateSharp but unused by sshm.
    $UnusedNative = @(
        "libonigwrap.dll",
        "libonigwrap.so",
        "libonigwrap.dylib"
    )

    foreach ($name in $UnusedNative) {
        $path = Join-Path $OutputDir $name
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "Removed unused artifact: $path"
        }
    }
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
        "-p:PublishAot=true",
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
    Write-Host "==> Publishing Native AOT $Runtime to: $OutputDir"
    & dotnet @PublishArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for $Runtime (exit code $LASTEXITCODE)"
    }

    $OutputName = Get-PublishOutputName -Runtime $Runtime
    $OutputPath = Join-Path $OutputDir $OutputName
    if (-not (Test-Path $OutputPath)) {
        throw "Expected output not found: $OutputPath"
    }

    Remove-UnusedPublishArtifacts -OutputDir $OutputDir

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
Write-Host ("Published {0} Native AOT target(s):" -f $Published.Count)
foreach ($Path in $Published) {
    Write-Host "  $Path"
}
