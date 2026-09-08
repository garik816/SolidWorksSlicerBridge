param(
    [switch]$Quiet,
    [switch]$SkipElevation
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Test-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (!$SkipElevation -and !(Test-Administrator)) {
    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"' + $MyInvocation.MyCommand.Path + '"'),
        "-SkipElevation"
    )
    if ($Quiet) { $args += "-Quiet" }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $args -Wait
    exit $LASTEXITCODE
}

if (!(Test-Administrator)) {
    throw "Administrator privileges are required to register the SOLIDWORKS Add-In."
}

if (Get-Process -Name "SLDWORKS" -ErrorAction SilentlyContinue) {
    throw "SOLIDWORKS is running. Close SOLIDWORKS 2026 and run the installer again."
}

& (Join-Path $Root "Build.ps1")
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

$dll = Join-Path $Root "build\SolidWorksSlicerBridge.dll"
$regasm = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
if (!(Test-Path $regasm)) { throw "RegAsm not found: $regasm" }

& $regasm $dll /codebase /tlb
if ($LASTEXITCODE -ne 0) { throw "RegAsm failed with exit code $LASTEXITCODE." }

if (!$Quiet) {
    Write-Host ""
    Write-Host "Installed SolidWorks Slicer Bridge." -ForegroundColor Green
    Write-Host "Restart SOLIDWORKS 2026. The add-in should load automatically."
    Write-Host "If needed: Tools > Add-Ins > SolidWorks Slicer Bridge."
    Read-Host "Press Enter to close"
}
