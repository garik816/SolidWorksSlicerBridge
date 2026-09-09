# Windows CI regression test. These are synthetic assemblies, not vendor APIs.
# This validates deployment and real RegAsm behavior, NOT SOLIDWORKS operation.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') {
    throw 'Run this isolated registry-writing test only on a disposable GitHub Actions runner.'
}
if ($PSVersionTable.PSVersion.Major -ne 5 -or ![Environment]::Is64BitProcess) {
    throw 'This regression test requires Windows PowerShell 5.1 x64.'
}

$repository = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP ('SWSB dependency test ' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $work 'Product Files'
$api = Join-Path $env:ProgramFiles 'SOLIDWORKS Corp\SOLIDWORKS\api\redist'
if (Test-Path -LiteralPath $api) { throw 'Refusing to replace an existing SOLIDWORKS API directory.' }
$ownsApiFolder = $false
$incomplete = Join-Path $work 'Incomplete Output'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$regasm = Join-Path $framework 'RegAsm.exe'
$powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$testClassId = [Guid]::NewGuid().ToString()
$dll = Join-Path $stage 'build\SolidWorksSlicerBridge.dll'

function Invoke-NativeCapture([string]$Exe, [string[]]$ToolArguments) {
    # Drain both streams asynchronously, with a bounded wait and no PowerShell
    # native-stderr promotion. Test arguments contain no embedded quote or trailing slash.
    $quoted = foreach ($argument in $ToolArguments) {
        if ($argument.Contains('"') -or $argument.EndsWith('\')) { throw 'Unsupported fixture argument.' }
        '"' + $argument + '"'
    }
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Exe
    $info.Arguments = $quoted -join ' '
    $info.WorkingDirectory = $work
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $info
    Write-Host ("RUN: {0} {1}" -f $Exe, $info.Arguments)
    try {
        if (!$process.Start()) { throw "Could not start $Exe" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $finished = $process.WaitForExit(60000)
        if (!$finished) { $process.Kill(); $process.WaitForExit() }
        $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        Write-Host $text
        if (!$finished) { throw "Test process timed out: $Exe" }
        return [pscustomobject]@{ Code = $process.ExitCode; Text = $text }
    }
    finally { $process.Dispose() }
}

try {
    foreach ($folder in @($api, $incomplete, (Join-Path $stage 'src'), (Join-Path $stage 'Icons'))) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }
    $ownsApiFolder = $true
    Copy-Item -LiteralPath (Join-Path $repository 'Build.ps1') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repository 'Install.ps1') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repository 'Generate-Icons.ps1') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repository 'Icons\Source') -Destination (Join-Path $stage 'Icons') -Recurse

    foreach ($name in @('sldworks', 'swconst', 'swpublished')) {
        $source = Join-Path $work ($name + '.cs')
        @"
using System.Reflection;
using System.Runtime.InteropServices;
[assembly: AssemblyVersion("34.1.1.11")]
[assembly: ComVisible(false)]
namespace SolidWorks.Interop.$name {
    public abstract class DependencyMarker { }
}
"@ | Set-Content -LiteralPath $source -Encoding UTF8
        $result = Invoke-NativeCapture $csc @('/nologo', '/target:library', "/out:$api\SolidWorks.Interop.$name.dll", $source)
        if ($result.Code -ne 0) { throw "Fixture compilation failed: $name" }
    }

    $fixture = Join-Path $stage 'src\SwAddin.cs'
    @"
using System.Runtime.InteropServices;
using SolidWorks.Interop.swpublished;
[assembly: Guid("$([Guid]::NewGuid().ToString())")]
[assembly: ComVisible(false)]
[ComVisible(true)]
[Guid("$testClassId")]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class RegistrationSmokeTest : DependencyMarker {
    public int Ping() { return 42; }
}
"@ | Set-Content -LiteralPath $fixture -Encoding UTF8
    '// Empty companion file for the production build script.' |
        Set-Content -LiteralPath (Join-Path $stage 'src\SettingsForm.cs') -Encoding UTF8

    # Prove the test detects the original missing-dependency defect before testing the fix.
    $missingDll = Join-Path $incomplete 'SolidWorksSlicerBridge.dll'
    $result = Invoke-NativeCapture $csc @('/nologo', '/target:library', '/platform:x64', "/out:$missingDll", "/reference:$api\SolidWorks.Interop.swpublished.dll", $fixture)
    if ($result.Code -ne 0) { throw 'Negative-control fixture compilation failed.' }
    $result = Invoke-NativeCapture $regasm @($missingDll, '/nologo', "/regfile:$incomplete\negative.reg")
    if ($result.Code -eq 0 -or $result.Text -notmatch 'swpublished') {
        throw 'Negative control did not reproduce the missing swpublished dependency.'
    }
    Write-Host 'PASS: missing dependency produces a genuine RegAsm failure.'

    # Use the real Install.ps1 and Build.ps1 unchanged, with isolated fixture inputs.
    # The disposable runner now has fixture APIs in the standard installation path.
    $result = Invoke-NativeCapture $powershell @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $stage 'Install.ps1'), '-Quiet', '-SkipElevation')
    $installLog = Join-Path $stage 'install.log'
    if (Test-Path -LiteralPath $installLog) { Get-Content -LiteralPath $installLog | ForEach-Object { Write-Host $_ } }
    if ($result.Code -ne 0) { throw "Production installer script failed: $($result.Code)" }

    foreach ($name in @('sldworks', 'swconst', 'swpublished')) {
        $original = Join-Path $api "SolidWorks.Interop.$name.dll"
        $copied = Join-Path $stage "build\SolidWorks.Interop.$name.dll"
        if (!(Test-Path -LiteralPath $copied)) { throw "Missing private dependency: $copied" }
        if ((Get-FileHash -LiteralPath $original).Hash -ne (Get-FileHash -LiteralPath $copied).Hash) {
            throw "Dependency hash mismatch: $name"
        }
    }
    if (!(Test-Path "HKLM:\SOFTWARE\Classes\CLSID\{$testClassId}\InprocServer32")) {
        throw 'RegAsm returned success, but the fixture COM class was not registered.'
    }
    if ((Get-Content -LiteralPath $installLog -Raw) -notmatch 'RegAsm exit code: 0') {
        throw 'Successful RegAsm result missing from installer log.'
    }
    Write-Host 'PASS: all three private dependencies match the source files.'
    Write-Host 'PASS: production installer completed real RegAsm registration on PowerShell 5.1.'
}
finally {
    $log = Join-Path $stage 'install.log'
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log | ForEach-Object { Write-Host $_ } }
    if (Test-Path -LiteralPath $dll) {
        # Only the randomly generated fixture class is unregistered, never the real add-in.
        $cleanup = Invoke-NativeCapture $regasm @($dll, '/unregister', '/tlb', '/nologo')
        if ($cleanup.Code -ne 0) { Write-Warning "Fixture cleanup exit code: $($cleanup.Code)" }
    }
    if ($ownsApiFolder) { Remove-Item -LiteralPath $api -Recurse -Force }
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
