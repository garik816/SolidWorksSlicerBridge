# Real Windows IPC receivers with synthetic slicer identities and CAD API stubs.
# No installed SOLIDWORKS or third-party slicer is exercised by these tests.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $PSVersionTable.PSVersion.Major -ne 5 -or ![Environment]::Is64BitProcess) {
    throw 'Run on a disposable x64 GitHub Actions runner using Windows PowerShell 5.1.'
}
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP ('SWSB append validation ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$exe = Join-Path $work 'ExistingWindowSmoke.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$process = $null
try {
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File |
        Sort-Object Name | Select-Object -ExpandProperty FullName)
    $compilerArguments = @(
        '/nologo', '/target:exe', '/platform:x64', '/codepage:65001',
        '/debug:pdbonly', '/main:ExistingWindowSmoke', "/out:$exe",
        '/reference:System.dll', '/reference:System.Core.dll',
        '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
        '/reference:System.Web.Extensions.dll',
        (Join-Path $PSScriptRoot 'CallbackInteropSmoke.cs'),
        (Join-Path $PSScriptRoot 'ExistingWindowSmoke.cs')
    ) + $sources
    & $csc @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Append regression tests did not compile.' }
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.WorkingDirectory = $work
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    if (!$process.Start()) { throw 'Could not start append tests.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(120000)) {
        & taskkill.exe /PID $process.Id /T /F | Out-Null
        throw 'Append tests exceeded their 120 second timeout.'
    }
    $process.WaitForExit()
    Write-Host $stdout.Result
    Write-Host $stderr.Result
    if ($process.ExitCode -ne 0) { throw "Append tests failed: $($process.ExitCode)" }
}
finally {
    if ($process) { $process.Dispose() }
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
