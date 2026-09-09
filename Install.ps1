param(
    [switch]$Quiet,
    [switch]$SkipElevation
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Log = Join-Path $Root "install.log"

function Write-InstallLog([string]$Text) {
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    "$stamp  $Text" | Out-File -FilePath $Log -Append -Encoding UTF8
}

function Test-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

try {
    if (Test-Path $Log) { Remove-Item $Log -Force }
    Write-InstallLog "Install started"
    Write-InstallLog "Root: $Root"
    Write-InstallLog "OS: $([Environment]::OSVersion.VersionString)"
    Write-InstallLog "PowerShell: $($PSVersionTable.PSVersion)"

    if (!$SkipElevation -and !(Test-Administrator)) {
        $args = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", ('"' + $MyInvocation.MyCommand.Path + '"'),
            "-SkipElevation"
        )
        if ($Quiet) { $args += "-Quiet" }
        $p = Start-Process powershell.exe -Verb RunAs -ArgumentList $args -Wait -PassThru
        exit $p.ExitCode
    }

    if (!(Test-Administrator)) {
        throw "Administrator privileges are required to register the SOLIDWORKS Add-In."
    }

    if (Get-Process -Name "SLDWORKS" -ErrorAction SilentlyContinue) {
        throw "SOLIDWORKS is running. Close SOLIDWORKS 2026 and run the installer again."
    }

    Write-InstallLog "Running Build.ps1"
    & (Join-Path $Root "Build.ps1") *>&1 | ForEach-Object {
        Write-InstallLog ("BUILD: " + $_.ToString())
    }

    $dll = Join-Path $Root "build\SolidWorksSlicerBridge.dll"
    if (!(Test-Path $dll)) {
        throw "Build finished without creating $dll"
    }
    Write-InstallLog "DLL built: $dll"

    $regasm = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
    if (!(Test-Path $regasm)) { throw "RegAsm not found: $regasm" }

    Write-InstallLog "Running RegAsm"
    & $regasm $dll /codebase /tlb *>&1 | ForEach-Object {
        Write-InstallLog ("REGASM: " + $_.ToString())
    }
    if ($LASTEXITCODE -ne 0) { throw "RegAsm failed with exit code $LASTEXITCODE." }

    Write-InstallLog "Install completed successfully"

    if (!$Quiet) {
        Write-Host ""
        Write-Host "Installed SolidWorks Slicer Bridge." -ForegroundColor Green
        Write-Host "Restart SOLIDWORKS 2026. The add-in should load automatically."
        Write-Host "If needed: Tools > Add-Ins > SolidWorks Slicer Bridge."
        Read-Host "Press Enter to close"
    }
    exit 0
}
catch {
    Write-InstallLog ("ERROR: " + $_.Exception.Message)
    Write-InstallLog ("STACK: " + $_.ScriptStackTrace)
    if (!$Quiet) {
        Write-Host "Installation failed." -ForegroundColor Red
        Write-Host $_.Exception.Message
        Write-Host "Log: $Log"
        Read-Host "Press Enter to close"
    }
    exit 1
}
