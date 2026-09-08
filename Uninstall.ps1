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

if (Get-Process -Name "SLDWORKS" -ErrorAction SilentlyContinue) {
    throw "SOLIDWORKS is running. Close SOLIDWORKS 2026 before uninstalling the Add-In."
}

$dll = Join-Path $Root "build\SolidWorksSlicerBridge.dll"
$regasm = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"

if ((Test-Path $dll) -and (Test-Path $regasm)) {
    & $regasm $dll /unregister /tlb | Out-Null
}

Remove-Item "HKLM:\SOFTWARE\SolidWorks\Addins\{D51D3347-A8E7-4892-A8BD-391203C2E8A4}" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "HKCU:\Software\SolidWorks\AddInsStartup\{D51D3347-A8E7-4892-A8BD-391203C2E8A4}" -Recurse -Force -ErrorAction SilentlyContinue

if (!$Quiet) {
    Write-Host "SolidWorks Slicer Bridge uninstalled." -ForegroundColor Green
    Read-Host "Press Enter to close"
}
